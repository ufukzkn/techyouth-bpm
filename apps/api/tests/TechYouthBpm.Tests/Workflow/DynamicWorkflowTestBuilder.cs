using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Workflow;

internal static class DynamicWorkflowTestBuilder
{
    public static Team SeedTeam(AppDbContext db, string name = "Finance")
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            CommunityId = TestDbFactory.CommunityId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Description = "Dynamic workflow test team",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Teams.Add(team);
        db.SaveChanges();
        return team;
    }

    public static async Task<ProcessDefinitionVersionDto> CreatePublishedAsync(
        AppDbContext db,
        User admin,
        TaskAssignmentDto assignment,
        TaskPriority priority = TaskPriority.Normal,
        IReadOnlyList<WorkflowAction>? actions = null,
        int? slaDurationMinutes = null)
    {
        var user = TestDbFactory.CommunityAdminDto(admin);
        var form = await new FormService(db).CreateAsync(
            new CreateFormRequest(
                $"Dynamic Form {Guid.NewGuid():N}",
                "Dynamic workflow start form",
                [new CreateFormFieldRequest("amount", "Amount", FieldType.Number, true, 1, [], [])]),
            user);
        if (!form.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(" | ", form.Errors));
        }

        var formVersionId = form.Value!.LatestPublishedVersionId!.Value;
        var graph = new ProcessGraphDto(
            "1.0",
            [
                new ProcessNodeDto(
                    "start",
                    ProcessNodeType.Start,
                    "Start",
                    formVersionId,
                    PositionX: 40,
                    PositionY: 80,
                    Width: 160,
                    Height: 72,
                    Description: "Start description"),
                new ProcessNodeDto(
                    "approval",
                    ProcessNodeType.UserTask,
                    "Approval",
                    Priority: priority,
                    Actions: actions ?? [WorkflowAction.Approve, WorkflowAction.Reject],
                    Assignment: assignment,
                    PositionX: 300,
                    PositionY: 70,
                    Width: 220,
                    Height: 96,
                    Description: "Approval description",
                    SlaDurationMinutes: slaDurationMinutes),
                new ProcessNodeDto(
                    "completed",
                    ProcessNodeType.CompletedEnd,
                    "Completed",
                    PositionX: 640,
                    PositionY: 30,
                    Width: 160,
                    Height: 72),
                new ProcessNodeDto(
                    "rejected",
                    ProcessNodeType.RejectedEnd,
                    "Rejected",
                    PositionX: 640,
                    PositionY: 150,
                    Width: 160,
                    Height: 72)
            ],
            [
                new ProcessEdgeDto("start", "approval", Order: 0, Label: "Submit"),
                new ProcessEdgeDto("approval", "completed", WorkflowAction.Approve, Order: 1, Label: "Approve"),
                new ProcessEdgeDto("approval", "rejected", WorkflowAction.Reject, Order: 2, Label: "Reject")
            ]);
        var service = new ProcessDefinitionService(
            db,
            new ProcessGraphValidator(db),
            new SystemAuditService(db));
        var definition = await service.CreateAsync(
            new CreateProcessDefinitionRequest($"Dynamic Process {Guid.NewGuid():N}", "Runtime test"),
            user);
        var version = await service.CreateVersionAsync(
            definition.Value!.Id,
            new CreateProcessDefinitionVersionRequest(formVersionId, graph),
            user);
        var published = await service.PublishVersionAsync(definition.Value.Id, version.Value!.Id, user);
        if (!published.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(" | ", published.Errors));
        }

        return published.Value!;
    }

    public static async Task<ProcessDefinitionVersionDto> CreatePublishedGraphAsync(
        AppDbContext db,
        User admin,
        Func<Guid, ProcessGraphDto> graphFactory)
    {
        var user = TestDbFactory.CommunityAdminDto(admin);
        var form = await new FormService(db).CreateAsync(
            new CreateFormRequest(
                $"Graph Form {Guid.NewGuid():N}",
                "Custom graph start form",
                [new CreateFormFieldRequest("amount", "Amount", FieldType.Number, true, 1, [], [])]),
            user);
        if (!form.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(" | ", form.Errors));
        }

        var formVersionId = form.Value!.LatestPublishedVersionId!.Value;
        var service = new ProcessDefinitionService(
            db,
            new ProcessGraphValidator(db),
            new SystemAuditService(db));
        var definition = await service.CreateAsync(
            new CreateProcessDefinitionRequest($"Graph Process {Guid.NewGuid():N}", "Runtime graph test"),
            user);
        var version = await service.CreateVersionAsync(
            definition.Value!.Id,
            new CreateProcessDefinitionVersionRequest(formVersionId, graphFactory(formVersionId)),
            user);
        var published = await service.PublishVersionAsync(definition.Value.Id, version.Value!.Id, user);
        if (!published.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(" | ", published.Errors));
        }

        return published.Value!;
    }
}
