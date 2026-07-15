using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Workflow;

public class DynamicWorkflowRuntimeTests
{
    [Fact]
    public async Task Competing_Claim_Snapshots_Allow_Only_One_Winner()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"techyouth-claim-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        Guid taskId;
        Guid claimVersion;
        TechYouthBpm.Application.Auth.UserDto firstCandidate;
        TechYouthBpm.Application.Auth.UserDto secondCandidate;

        try
        {
            await using (var setupDb = new AppDbContext(options))
            {
                await setupDb.Database.EnsureCreatedAsync();
                var admin = TestDbFactory.SeedUser(setupDb, Role.Admin, "claim-race-admin");
                var first = TestDbFactory.SeedUser(setupDb, Role.Approver, "claim-race-first");
                var second = TestDbFactory.SeedUser(setupDb, Role.Approver, "claim-race-second");
                var team = DynamicWorkflowTestBuilder.SeedTeam(setupDb, "Concurrent Claim Team");
                setupDb.TeamMemberships.AddRange(
                    TeamMember(team.Id, first.Id),
                    TeamMember(team.Id, second.Id));
                await setupDb.SaveChangesAsync();
                var version = await DynamicWorkflowTestBuilder.CreatePublishedAsync(
                    setupDb,
                    admin,
                    new TaskAssignmentDto(
                        TaskAssignmentType.TeamAndCommunityRole,
                        TeamId: team.Id,
                        CommunityRoleId: TestDbFactory.ApproverCommunityRoleId));
                using var data = JsonDocument.Parse("{\"amount\":100}");
                var started = await new ProcessService(
                        setupDb,
                        new FormService(setupDb),
                        new ProcessStateMachine(),
                        new SystemAuditService(setupDb))
                    .StartVersionAsync(
                        new StartProcessVersionRequest(version.Id, data.RootElement.Clone()),
                        TestDbFactory.CommunityAdminDto(admin));
                var task = Assert.Single(started.Value!.Tasks);
                taskId = task.Id;
                Assert.NotNull(task.ClaimVersion);
                claimVersion = task.ClaimVersion.Value;
                firstCandidate = TestDbFactory.ToDto(first);
                secondCandidate = TestDbFactory.ToDto(second);
            }

            await using var firstDb = new AppDbContext(options);
            await using var secondDb = new AppDbContext(options);
            await LoadClaimSnapshotAsync(firstDb, taskId);
            await LoadClaimSnapshotAsync(secondDb, taskId);

            var firstResult = await new TaskService(firstDb, new ProcessStateMachine()).ClaimAsync(
                taskId,
                new ClaimTaskRequest(claimVersion),
                firstCandidate);
            var secondResult = await new TaskService(secondDb, new ProcessStateMachine()).ClaimAsync(
                taskId,
                new ClaimTaskRequest(claimVersion),
                secondCandidate);

            Assert.True(firstResult.IsSuccess, string.Join(" | ", firstResult.Errors));
            Assert.False(secondResult.IsSuccess);
            Assert.Contains(secondResult.Errors, error => error.Contains("another user", StringComparison.OrdinalIgnoreCase));
            await using var verificationDb = new AppDbContext(options);
            var persisted = await verificationDb.ProcessTasks.AsNoTracking().SingleAsync(task => task.Id == taskId);
            Assert.Equal(firstCandidate.Id, persisted.ClaimedByUserId);
            Assert.Equal(ProcessTaskStatus.Claimed, persisted.Status);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Start_Rolls_Back_When_TeamAndRole_Pool_Has_No_Eligible_TasksAct_User()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "no-candidate-admin");
        var team = DynamicWorkflowTestBuilder.SeedTeam(db);
        var version = await DynamicWorkflowTestBuilder.CreatePublishedAsync(
            db,
            admin,
            new TaskAssignmentDto(
                TaskAssignmentType.TeamAndCommunityRole,
                TeamId: team.Id,
                CommunityRoleId: TestDbFactory.ApproverCommunityRoleId));
        var service = new ProcessService(
            db,
            new FormService(db),
            new ProcessStateMachine(),
            new SystemAuditService(db));
        using var data = JsonDocument.Parse("{\"amount\":125000}");

        var result = await service.StartVersionAsync(
            new StartProcessVersionRequest(version.Id, data.RootElement.Clone()),
            TestDbFactory.CommunityAdminDto(admin));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("no eligible candidate", StringComparison.OrdinalIgnoreCase));
        db.ChangeTracker.Clear();
        Assert.False(await db.ProcessInstances.AnyAsync(process => process.ProcessDefinitionVersionId == version.Id));
        Assert.False(await db.ProcessTasks.AnyAsync(task => task.ProcessInstance!.ProcessDefinitionVersionId == version.Id));
        Assert.False(await db.ProcessStepExecutions.AnyAsync(step => step.ProcessInstance!.ProcessDefinitionVersionId == version.Id));
        Assert.False(await db.Notifications.AnyAsync(notification => notification.EntityType == "ProcessTask"));
        Assert.False(await db.SystemAuditLogs.AnyAsync(log =>
            log.Action == "Process.Started" && log.Description.Contains(version.VersionNumber.ToString())));
    }

    [Fact]
    public async Task Eligible_Intersection_Can_Claim_And_Complete_Critical_Task()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "dynamic-admin");
        var approver = TestDbFactory.SeedUser(db, Role.Approver, "dynamic-approver");
        var team = DynamicWorkflowTestBuilder.SeedTeam(db);
        db.TeamMemberships.Add(new TeamMembership
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            UserId = approver.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var version = await DynamicWorkflowTestBuilder.CreatePublishedAsync(
            db,
            admin,
            new TaskAssignmentDto(
                TaskAssignmentType.TeamAndCommunityRole,
                TeamId: team.Id,
                CommunityRoleId: TestDbFactory.ApproverCommunityRoleId),
            TaskPriority.Critical);
        var processService = new ProcessService(
            db,
            new FormService(db),
            new ProcessStateMachine(),
            new SystemAuditService(db));
        using var data = JsonDocument.Parse("{\"amount\":250000}");

        var started = await processService.StartVersionAsync(
            new StartProcessVersionRequest(version.Id, data.RootElement.Clone()),
            TestDbFactory.CommunityAdminDto(admin));

        Assert.True(started.IsSuccess, string.Join(" | ", started.Errors));
        var openTask = Assert.Single(started.Value!.Tasks);
        Assert.Equal(TaskPriority.Critical, openTask.Priority);
        Assert.Equal(TaskAssignmentType.TeamAndCommunityRole, openTask.AssignmentType);
        var taskService = new TaskService(db, new ProcessStateMachine());
        var claimed = await taskService.ClaimAsync(
            openTask.Id,
            new ClaimTaskRequest(openTask.ClaimVersion),
            TestDbFactory.ToDto(approver));

        Assert.True(claimed.IsSuccess, string.Join(" | ", claimed.Errors));
        Assert.Equal(ProcessTaskStatus.Claimed, claimed.Value!.Status);
        Assert.Equal(approver.Id, claimed.Value.ClaimedByUserId);
        Assert.NotEqual(openTask.ClaimVersion, claimed.Value.ClaimVersion);

        var completed = await taskService.ExecuteActionAsync(
            openTask.Id,
            new TaskActionRequest(WorkflowAction.Approve, "Approved."),
            TestDbFactory.ToDto(approver));

        Assert.True(completed.IsSuccess, string.Join(" | ", completed.Errors));
        Assert.Equal(ProcessStatus.Completed, completed.Value!.Status);
        Assert.Equal("completed", completed.Value.CurrentNodeKey);
        Assert.Contains(completed.Value.StepExecutions!, step =>
            step.NodeKey == "approval"
            && step.Attempt == 1
            && step.Status == ProcessStepStatus.Completed);
    }

    [Fact]
    public async Task Action_Rolls_Back_When_Next_Task_Has_No_Eligible_Candidate()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "action-rollback-admin");
        var version = await DynamicWorkflowTestBuilder.CreatePublishedGraphAsync(
            db,
            admin,
            formVersionId => new ProcessGraphDto(
                "1.0",
                [
                    Node("start", ProcessNodeType.Start, formVersionId: formVersionId),
                    Node(
                        "starterReview",
                        ProcessNodeType.UserTask,
                        actions: [WorkflowAction.Approve],
                        assignment: new TaskAssignmentDto(TaskAssignmentType.ProcessStarter)),
                    Node(
                        "unstaffedApproval",
                        ProcessNodeType.UserTask,
                        actions: [WorkflowAction.Approve],
                        assignment: new TaskAssignmentDto(
                            TaskAssignmentType.CommunityRole,
                            CommunityRoleId: TestDbFactory.ApproverCommunityRoleId)),
                    Node("completed", ProcessNodeType.CompletedEnd)
                ],
                [
                    new ProcessEdgeDto("start", "starterReview"),
                    new ProcessEdgeDto("starterReview", "unstaffedApproval", WorkflowAction.Approve),
                    new ProcessEdgeDto("unstaffedApproval", "completed", WorkflowAction.Approve)
                ]));
        var processService = new ProcessService(
            db,
            new FormService(db),
            new ProcessStateMachine(),
            new SystemAuditService(db));
        using var data = JsonDocument.Parse("{\"amount\":100}");
        var started = await processService.StartVersionAsync(
            new StartProcessVersionRequest(version.Id, data.RootElement.Clone()),
            TestDbFactory.CommunityAdminDto(admin));
        var firstTask = Assert.Single(started.Value!.Tasks);
        var taskService = new TaskService(db, new ProcessStateMachine());

        var action = await taskService.ExecuteActionAsync(
            firstTask.Id,
            new TaskActionRequest(WorkflowAction.Approve, "Route onward"),
            TestDbFactory.CommunityAdminDto(admin));

        Assert.False(action.IsSuccess);
        Assert.Contains(action.Errors, error => error.Contains("no eligible candidate", StringComparison.OrdinalIgnoreCase));
        db.ChangeTracker.Clear();
        var persistedTask = await db.ProcessTasks.SingleAsync(task => task.Id == firstTask.Id);
        Assert.Equal(ProcessTaskStatus.Open, persistedTask.Status);
        Assert.Null(persistedTask.CompletedAt);
        Assert.False(await db.ProcessTasks.AnyAsync(task => task.NodeKey == "unstaffedApproval"));
        var activeStep = await db.ProcessStepExecutions.SingleAsync(step => step.NodeKey == "starterReview");
        Assert.Equal(ProcessStepStatus.Active, activeStep.Status);
    }

    [Fact]
    public async Task SendBack_Creates_New_UserTask_Attempt_Without_Reopening_History()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "sendback-admin");
        var version = await DynamicWorkflowTestBuilder.CreatePublishedGraphAsync(
            db,
            admin,
            formVersionId => new ProcessGraphDto(
                "1.0",
                [
                    Node("start", ProcessNodeType.Start, formVersionId: formVersionId),
                    Node(
                        "draftReview",
                        ProcessNodeType.UserTask,
                        actions: [WorkflowAction.Approve],
                        assignment: new TaskAssignmentDto(TaskAssignmentType.ProcessStarter)),
                    Node(
                        "finalReview",
                        ProcessNodeType.UserTask,
                        actions: [WorkflowAction.Approve, WorkflowAction.SendBack],
                        assignment: new TaskAssignmentDto(TaskAssignmentType.ProcessStarter)),
                    Node("completed", ProcessNodeType.CompletedEnd)
                ],
                [
                    new ProcessEdgeDto("start", "draftReview"),
                    new ProcessEdgeDto("draftReview", "finalReview", WorkflowAction.Approve),
                    new ProcessEdgeDto("finalReview", "completed", WorkflowAction.Approve),
                    new ProcessEdgeDto("finalReview", "draftReview", WorkflowAction.SendBack)
                ]));
        var user = TestDbFactory.CommunityAdminDto(admin);
        var processService = new ProcessService(
            db,
            new FormService(db),
            new ProcessStateMachine(),
            new SystemAuditService(db));
        using var data = JsonDocument.Parse("{\"amount\":100}");
        var started = await processService.StartVersionAsync(
            new StartProcessVersionRequest(version.Id, data.RootElement.Clone()),
            user);
        var taskService = new TaskService(db, new ProcessStateMachine());
        var firstAttempt = Assert.Single(started.Value!.Tasks);
        var finalReview = await taskService.ExecuteActionAsync(
            firstAttempt.Id,
            new TaskActionRequest(WorkflowAction.Approve, null),
            user);
        var secondTask = finalReview.Value!.Tasks.Single(task => task.Status == ProcessTaskStatus.Open);

        var sentBack = await taskService.ExecuteActionAsync(
            secondTask.Id,
            new TaskActionRequest(WorkflowAction.SendBack, "Please revise"),
            user);

        Assert.True(sentBack.IsSuccess, string.Join(" | ", sentBack.Errors));
        Assert.Equal(ProcessStatus.InProgress, sentBack.Value!.Status);
        var reopenedNode = sentBack.Value.Tasks.Single(task => task.Status == ProcessTaskStatus.Open);
        Assert.Equal("draftReview", reopenedNode.NodeKey);
        Assert.Equal(2, reopenedNode.Attempt);
        Assert.Contains(sentBack.Value.Tasks, task =>
            task.NodeKey == "draftReview"
            && task.Attempt == 1
            && task.Status == ProcessTaskStatus.Completed);
        Assert.True(sentBack.Value.Variables.TryGetProperty("steps", out var steps));
        Assert.False(steps.TryGetProperty("draftReview", out _));
        Assert.False(steps.TryGetProperty("finalReview", out _));
    }

    [Fact]
    public async Task Complete_Action_Can_Finish_An_Operational_Task()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "complete-action-admin");
        var version = await DynamicWorkflowTestBuilder.CreatePublishedGraphAsync(
            db,
            admin,
            formVersionId => new ProcessGraphDto(
                "1.0",
                [
                    Node("start", ProcessNodeType.Start, formVersionId: formVersionId),
                    Node(
                        "operation",
                        ProcessNodeType.UserTask,
                        actions: [WorkflowAction.Complete],
                        assignment: new TaskAssignmentDto(TaskAssignmentType.ProcessStarter)),
                    Node("completed", ProcessNodeType.CompletedEnd)
                ],
                [
                    new ProcessEdgeDto("start", "operation"),
                    new ProcessEdgeDto("operation", "completed", WorkflowAction.Complete)
                ]));
        var user = TestDbFactory.CommunityAdminDto(admin);
        var processService = new ProcessService(db, new FormService(db), new ProcessStateMachine(), new SystemAuditService(db));
        using var data = JsonDocument.Parse("{\"amount\":100}");
        var started = await processService.StartVersionAsync(new(version.Id, data.RootElement.Clone()), user);
        var task = Assert.Single(started.Value!.Tasks);

        var completed = await new TaskService(db, new ProcessStateMachine()).ExecuteActionAsync(
            task.Id,
            new TaskActionRequest(WorkflowAction.Complete, "Operation finished"),
            user);

        Assert.True(completed.IsSuccess, string.Join(" | ", completed.Errors));
        Assert.Equal(ProcessStatus.Completed, completed.Value!.Status);
        Assert.Contains(completed.Value.AuditLogs, log => log.Action == WorkflowAction.Complete);
    }

    private static ProcessNodeDto Node(
        string key,
        ProcessNodeType type,
        Guid? formVersionId = null,
        IReadOnlyList<WorkflowAction>? actions = null,
        TaskAssignmentDto? assignment = null) =>
        new(
            key,
            type,
            key,
            formVersionId,
            Actions: actions,
            Assignment: assignment,
            PositionX: type == ProcessNodeType.Start ? 40 : 300,
            PositionY: 80,
            Width: 180,
            Height: 80);

    private static TeamMembership TeamMember(Guid teamId, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        TeamId = teamId,
        UserId = userId,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Task<ProcessTask> LoadClaimSnapshotAsync(AppDbContext db, Guid taskId) =>
        db.ProcessTasks
            .Include(task => task.AssignedCommunityRole)
            .Include(task => task.ProcessInstance)
            .SingleAsync(task => task.Id == taskId);
}
