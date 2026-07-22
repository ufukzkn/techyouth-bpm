using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Workflow;

public class ProcessDefinitionServiceTests
{
    [Fact]
    public async Task Runnable_List_Uses_Process_Start_Permission_And_Excludes_Drafts()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "runnable-admin");
        var published = await DynamicWorkflowTestBuilder.CreatePublishedAsync(
            db,
            admin,
            new TaskAssignmentDto(TaskAssignmentType.ProcessStarter));
        var service = new ProcessDefinitionService(db, new ProcessGraphValidator(db), new SystemAuditService(db));
        await service.CreateAsync(
            new CreateProcessDefinitionRequest("Draft only workflow", "It must not be runnable."),
            TestDbFactory.CommunityAdminDto(admin));
        var starter = UserWithPermissions(admin, PermissionNames.ProcessesStart);

        var editorList = await service.ListAsync(starter);
        var runnableList = await service.ListRunnableAsync(starter);

        Assert.Empty(editorList);
        var runnable = Assert.Single(runnableList);
        Assert.Equal(published.Id, runnable.ProcessDefinitionVersionId);
        Assert.Equal(published.FormDefinitionVersionId, runnable.FormDefinitionVersionId);
    }

    [Fact]
    public async Task Draft_RoundTrips_Visual_Layout_Descriptions_Labels_And_Swimlane_Team()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var user = TestDbFactory.CommunityAdminDto(admin);
        var team = DynamicWorkflowTestBuilder.SeedTeam(db, "Visual Finance");
        var form = await new FormService(db).CreateAsync(
            new CreateFormRequest(
                "Visual Form",
                "Visual test",
                [new CreateFormFieldRequest("amount", "Amount", FieldType.Number, true, 1, [], [])]),
            user);
        var formVersionId = form.Value!.LatestPublishedVersionId!.Value;
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto(
                    "finance-lane",
                    ProcessNodeType.TeamSwimlane,
                    "Finance lane",
                    ParentKey: null,
                    PositionX: 12.5,
                    PositionY: 32.25,
                    Width: 1040,
                    Height: 220,
                    Description: "Finance work area",
                    TeamId: team.Id),
                new ProcessNodeDto(
                    "start",
                    ProcessNodeType.Start,
                    "Start",
                    formVersionId,
                    ParentKey: "finance-lane",
                    PositionX: 48.5,
                    PositionY: 74.75,
                    Width: 160,
                    Height: 72,
                    Description: "Start details"),
                new ProcessNodeDto(
                    "approval",
                    ProcessNodeType.UserTask,
                    "Approval",
                    Actions: [WorkflowAction.Approve],
                    Assignment: new TaskAssignmentDto(TaskAssignmentType.ProcessStarter),
                    ParentKey: "finance-lane",
                    PositionX: 310,
                    PositionY: 72,
                    Width: 220,
                    Height: 96,
                    Description: "Approval details"),
                new ProcessNodeDto(
                    "completed",
                    ProcessNodeType.CompletedEnd,
                    "Completed",
                    ParentKey: "finance-lane",
                    PositionX: 650,
                    PositionY: 74,
                    Width: 160,
                    Height: 72)
            ],
            [
                new ProcessEdgeDto("start", "approval", Order: 0, Label: "Submit request"),
                new ProcessEdgeDto("approval", "completed", WorkflowAction.Approve, Order: 1, Label: "Approve request")
            ]);
        var service = new ProcessDefinitionService(db, new ProcessGraphValidator(db), new SystemAuditService(db));
        var definition = await service.CreateAsync(
            new CreateProcessDefinitionRequest("Visual Workflow", "Round trip test"),
            user);

        var saved = await service.CreateVersionAsync(
            definition.Value!.Id,
            new CreateProcessDefinitionVersionRequest(formVersionId, graph),
            user);

        Assert.True(saved.IsSuccess, string.Join(" | ", saved.Errors));
        db.ChangeTracker.Clear();
        var reloaded = await service.GetAsync(definition.Value.Id, user);
        var persistedGraph = Assert.Single(reloaded!.Versions).Graph;
        var lane = persistedGraph.Nodes.Single(node => node.Key == "finance-lane");
        Assert.Equal(team.Id, lane.TeamId);
        Assert.Equal("Finance work area", lane.Description);
        Assert.Equal(12.5, lane.PositionX);
        Assert.Equal(1040, lane.Width);
        var start = persistedGraph.Nodes.Single(node => node.Key == "start");
        Assert.Equal("finance-lane", start.ParentKey);
        Assert.Equal(74.75, start.PositionY);
        Assert.Equal("Start details", start.Description);
        Assert.Equal("Submit request", persistedGraph.Edges.Single(edge => edge.Source == "start").Label);

        var published = await service.PublishVersionAsync(definition.Value.Id, saved.Value!.Id, user);
        Assert.True(published.IsSuccess, string.Join(" | ", published.Errors));
    }

    [Fact]
    public void Validation_Rejects_Invalid_Canvas_Geometry()
    {
        using var db = TestDbFactory.Create();
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start, "Start", PositionX: double.NaN),
                new ProcessNodeDto("end", ProcessNodeType.CompletedEnd, "End", Width: -10)
            ],
            [new ProcessEdgeDto("start", "end")]);

        var result = new ProcessGraphValidator(db).ValidateStructure(graph);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("finite canvas", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("width", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(525601)]
    public void Validation_Rejects_UserTask_Sla_Outside_Allowed_Range(int slaDurationMinutes)
    {
        using var db = TestDbFactory.Create();
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start),
                new ProcessNodeDto(
                    "approval",
                    ProcessNodeType.UserTask,
                    Actions: [WorkflowAction.Approve],
                    Assignment: new(TaskAssignmentType.ProcessStarter),
                    SlaDurationMinutes: slaDurationMinutes),
                new ProcessNodeDto("end", ProcessNodeType.CompletedEnd)
            ],
            [
                new ProcessEdgeDto("start", "approval"),
                new ProcessEdgeDto("approval", "end", WorkflowAction.Approve)
            ]);

        var result = new ProcessGraphValidator(db).ValidateStructure(graph);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("SLA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_Rejects_TeamLead_Restriction_For_NonTeam_Assignment()
    {
        using var db = TestDbFactory.Create();
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start),
                new ProcessNodeDto(
                    "approval",
                    ProcessNodeType.UserTask,
                    Actions: [WorkflowAction.Approve],
                    Assignment: new(TaskAssignmentType.ProcessStarter),
                    RequiresTeamLead: true),
                new ProcessNodeDto("end", ProcessNodeType.CompletedEnd)
            ],
            [
                new ProcessEdgeDto("start", "approval"),
                new ProcessEdgeDto("approval", "end", WorkflowAction.Approve)
            ]);

        var result = new ProcessGraphValidator(db).ValidateStructure(graph);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("team lead only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Publish_Rejects_TeamLead_Task_Without_Eligible_Lead()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "lead-validation-admin");
        var user = TestDbFactory.CommunityAdminDto(admin);
        var team = DynamicWorkflowTestBuilder.SeedTeam(db, "No Lead Team");
        var form = await new FormService(db).CreateAsync(
            new CreateFormRequest(
                "Lead validation form",
                "",
                [new CreateFormFieldRequest("amount", "Amount", FieldType.Number, true, 0, [], [])]),
            user);
        var formVersionId = form.Value!.LatestPublishedVersionId!.Value;
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start, FormDefinitionVersionId: formVersionId),
                new ProcessNodeDto(
                    "approval",
                    ProcessNodeType.UserTask,
                    Actions: [WorkflowAction.Approve],
                    Assignment: new(TaskAssignmentType.Team, TeamId: team.Id),
                    RequiresTeamLead: true),
                new ProcessNodeDto("end", ProcessNodeType.CompletedEnd)
            ],
            [
                new ProcessEdgeDto("start", "approval"),
                new ProcessEdgeDto("approval", "end", WorkflowAction.Approve)
            ]);

        var result = await new ProcessGraphValidator(db).ValidateForPublishAsync(
            graph,
            TestDbFactory.CommunityId,
            formVersionId);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error =>
            error.Contains(PermissionNames.TasksAct, StringComparison.Ordinal)
            && error.Contains("sorumlusu bulunmuyor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_Rejects_Condition_Path_Outside_Namespaced_Variables()
    {
        using var db = TestDbFactory.Create();
        using var value = System.Text.Json.JsonDocument.Parse("10");
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start, "Start"),
                new ProcessNodeDto("gateway", ProcessNodeType.ExclusiveGateway, "Route"),
                new ProcessNodeDto("completed", ProcessNodeType.CompletedEnd, "Completed"),
                new ProcessNodeDto("rejected", ProcessNodeType.RejectedEnd, "Rejected")
            ],
            [
                new ProcessEdgeDto("start", "gateway"),
                new ProcessEdgeDto(
                    "gateway",
                    "completed",
                    Condition: new ProcessConditionDto(
                        "global.amount",
                        GraphConditionOperator.GreaterThan,
                        value.RootElement.Clone())),
                new ProcessEdgeDto("gateway", "rejected", IsDefault: true)
            ]);

        var result = new ProcessGraphValidator(db).ValidateStructure(graph);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("start.*", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_Rejects_Automatic_Cycles()
    {
        using var db = TestDbFactory.Create();
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start),
                new ProcessNodeDto("first", ProcessNodeType.UserTask, Actions: [WorkflowAction.Approve], Assignment: new(TaskAssignmentType.ProcessStarter)),
                new ProcessNodeDto("second", ProcessNodeType.UserTask, Actions: [WorkflowAction.Approve], Assignment: new(TaskAssignmentType.ProcessStarter)),
                new ProcessNodeDto("end", ProcessNodeType.CompletedEnd)
            ],
            [
                new ProcessEdgeDto("start", "first"),
                new ProcessEdgeDto("first", "second", WorkflowAction.Approve),
                new ProcessEdgeDto("second", "first", WorkflowAction.Approve)
            ]);

        var result = new ProcessGraphValidator(db).ValidateStructure(graph);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("cannot contain cycles", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_Rejects_SendBack_To_A_Forward_Task()
    {
        using var db = TestDbFactory.Create();
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start),
                new ProcessNodeDto("first", ProcessNodeType.UserTask, Actions: [WorkflowAction.Approve, WorkflowAction.SendBack], Assignment: new(TaskAssignmentType.ProcessStarter)),
                new ProcessNodeDto("second", ProcessNodeType.UserTask, Actions: [WorkflowAction.Approve], Assignment: new(TaskAssignmentType.ProcessStarter)),
                new ProcessNodeDto("end", ProcessNodeType.CompletedEnd)
            ],
            [
                new ProcessEdgeDto("start", "first"),
                new ProcessEdgeDto("first", "second", WorkflowAction.Approve),
                new ProcessEdgeDto("first", "second", WorkflowAction.SendBack),
                new ProcessEdgeDto("second", "end", WorkflowAction.Approve)
            ]);

        var result = new ProcessGraphValidator(db).ValidateStructure(graph);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("earlier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Workflow_Create_And_Update_Permissions_Are_Independent()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var createOnly = UserWithPermissions(admin, PermissionNames.WorkflowsCreate, PermissionNames.WorkflowsView);
        var service = new ProcessDefinitionService(db, new ProcessGraphValidator(db), new SystemAuditService(db));

        var created = await service.CreateAsync(
            new CreateProcessDefinitionRequest("Permission Workflow", "Original"),
            createOnly);
        var deniedUpdate = await service.UpdateAsync(
            created.Value!.Id,
            new UpdateProcessDefinitionRequest("Permission Workflow", "Changed"),
            createOnly);
        var updateOnly = UserWithPermissions(admin, PermissionNames.WorkflowsUpdate, PermissionNames.WorkflowsView);
        var deniedCreate = await service.CreateAsync(
            new CreateProcessDefinitionRequest("Another Workflow", "Denied"),
            updateOnly);

        Assert.True(created.IsSuccess, string.Join(" | ", created.Errors));
        Assert.False(deniedUpdate.IsSuccess);
        Assert.False(deniedCreate.IsSuccess);
    }

    [Fact]
    public async Task Publish_Rejects_Gateway_Condition_That_Reads_A_Future_Task_Form()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "future-condition-admin");
        var user = TestDbFactory.CommunityAdminDto(admin);
        var startForm = await new FormService(db).CreateAsync(
            new CreateFormRequest(
                "Start condition form",
                "",
                [new CreateFormFieldRequest("amount", "Amount", FieldType.Number, true, 0, [], [])]),
            user);
        var taskForm = await new FormService(db).CreateAsync(
            new CreateFormRequest(
                "Future task form",
                "",
                [new CreateFormFieldRequest("decision", "Decision", FieldType.Text, true, 0, [], [])]),
            user);
        using var expected = JsonDocument.Parse("\"approved\"");
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start, FormDefinitionVersionId: startForm.Value!.LatestPublishedVersionId),
                new ProcessNodeDto("gateway", ProcessNodeType.ExclusiveGateway),
                new ProcessNodeDto(
                    "futureTask",
                    ProcessNodeType.UserTask,
                    FormDefinitionVersionId: taskForm.Value!.LatestPublishedVersionId,
                    Actions: [WorkflowAction.Approve],
                    Assignment: new(TaskAssignmentType.ProcessStarter)),
                new ProcessNodeDto("completed", ProcessNodeType.CompletedEnd),
                new ProcessNodeDto("rejected", ProcessNodeType.RejectedEnd)
            ],
            [
                new ProcessEdgeDto("start", "gateway"),
                new ProcessEdgeDto(
                    "gateway",
                    "futureTask",
                    Condition: new("steps.futureTask.decision", GraphConditionOperator.Equals, expected.RootElement.Clone())),
                new ProcessEdgeDto("gateway", "rejected", IsDefault: true),
                new ProcessEdgeDto("futureTask", "completed", WorkflowAction.Approve)
            ]);

        var result = await new ProcessGraphValidator(db).ValidateForPublishAsync(
            graph,
            TestDbFactory.CommunityId,
            startForm.Value.LatestPublishedVersionId!.Value);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("does not execute before", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Publish_Rejects_Condition_Value_Whose_Type_Does_Not_Match_The_Form_Field()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "typed-condition-admin");
        var user = TestDbFactory.CommunityAdminDto(admin);
        var form = await new FormService(db).CreateAsync(
            new CreateFormRequest(
                "Typed condition form",
                "",
                [new CreateFormFieldRequest("amount", "Amount", FieldType.Number, true, 0, [], [])]),
            user);
        using var invalidValue = JsonDocument.Parse("\"100\"");
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start, FormDefinitionVersionId: form.Value!.LatestPublishedVersionId),
                new ProcessNodeDto("gateway", ProcessNodeType.ExclusiveGateway),
                new ProcessNodeDto("completed", ProcessNodeType.CompletedEnd),
                new ProcessNodeDto("rejected", ProcessNodeType.RejectedEnd)
            ],
            [
                new ProcessEdgeDto("start", "gateway"),
                new ProcessEdgeDto(
                    "gateway",
                    "completed",
                    Condition: new("start.amount", GraphConditionOperator.GreaterThan, invalidValue.RootElement.Clone())),
                new ProcessEdgeDto("gateway", "rejected", IsDefault: true)
            ]);

        var result = await new ProcessGraphValidator(db).ValidateForPublishAsync(
            graph,
            TestDbFactory.CommunityId,
            form.Value.LatestPublishedVersionId!.Value);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("does not match", StringComparison.OrdinalIgnoreCase));
    }

    private static UserDto UserWithPermissions(TechYouthBpm.Domain.Entities.User user, params string[] permissions) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email,
            user.Role,
            user.Status,
            user.IsEmailVerified,
            CommunityId: TestDbFactory.CommunityId,
            CommunityName: "Test Community",
            CommunityRoleId: TestDbFactory.AdminCommunityRoleId,
            CommunityRoleName: "Admin",
            Permissions: permissions);
}
