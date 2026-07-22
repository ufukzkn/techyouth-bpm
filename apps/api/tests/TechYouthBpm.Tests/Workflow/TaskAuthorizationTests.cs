using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Workflow;

public class TaskAuthorizationTests
{
    [Fact]
    public async Task User_Cannot_Execute_Approver_Task()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var (_, task) = TestDbFactory.SeedOpenApproverTask(db, admin);

        var service = new TaskService(db, new ProcessStateMachine());
        var regularUser = new UserDto(Guid.NewGuid(), "user1", "Test User", Role.User);

        var result = await service.ExecuteActionAsync(task.Id, new TaskActionRequest(WorkflowAction.Approve, "test"), regularUser);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("cannot execute", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Approver_Can_Execute_Approver_Task()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var approver = TestDbFactory.SeedUser(db, Role.Approver);
        var (_, task) = TestDbFactory.SeedOpenApproverTask(db, admin);

        var service = new TaskService(db, new ProcessStateMachine());
        var approverDto = TestDbFactory.ToDto(approver);

        var result = await service.ExecuteActionAsync(task.Id, new TaskActionRequest(WorkflowAction.Approve, "Approved."), approverDto);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProcessStatus.Completed, result.Value!.Status);
    }

    [Fact]
    public async Task Admin_Can_Execute_Any_Task()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var (_, task) = TestDbFactory.SeedOpenApproverTask(db, admin);

        var service = new TaskService(db, new ProcessStateMachine());
        var adminDto = TestDbFactory.ToDto(admin);

        var result = await service.ExecuteActionAsync(task.Id, new TaskActionRequest(WorkflowAction.Reject, "Admin rejected."), adminDto);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProcessStatus.Rejected, result.Value!.Status);
    }

    [Fact]
    public async Task ManageAll_Can_Execute_Unclaimed_Task_Assigned_To_Another_User()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "manage-all-admin");
        var assignedUser = TestDbFactory.SeedUser(db, Role.Approver, "direct-assignee");
        var (_, task) = TestDbFactory.SeedOpenApproverTask(db, admin);
        task.AssignmentType = TaskAssignmentType.SpecificUser;
        task.AssignedUserId = assignedUser.Id;
        await db.SaveChangesAsync();

        var result = await new TaskService(db, new ProcessStateMachine()).ExecuteActionAsync(
            task.Id,
            new TaskActionRequest(WorkflowAction.Approve, "Community admin approved."),
            TestDbFactory.CommunityAdminDto(admin));

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        Assert.Equal(ProcessStatus.Completed, result.Value!.Status);
    }

    [Fact]
    public async Task ManageAll_Can_Claim_Candidate_Task_Without_Target_Role_Or_Team()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "manage-all-claim-admin");
        var (_, task) = TestDbFactory.SeedOpenApproverTask(db, admin);
        var team = DynamicWorkflowTestBuilder.SeedTeam(db, "Restricted claim team");
        task.AssignmentType = TaskAssignmentType.TeamAndCommunityRole;
        task.CandidateTeamId = team.Id;
        task.CandidateCommunityRoleId = TestDbFactory.ApproverCommunityRoleId;
        task.RequiresTeamLead = true;
        await db.SaveChangesAsync();

        var result = await new TaskService(db, new ProcessStateMachine()).ClaimAsync(
            task.Id,
            new ClaimTaskRequest(task.ClaimVersion),
            TestDbFactory.CommunityAdminDto(admin));

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        Assert.Equal(ProcessTaskStatus.Claimed, result.Value!.Status);
        Assert.Equal(admin.Id, result.Value.ClaimedByUserId);
    }

    [Fact]
    public async Task Closed_Task_Returns_Already_Closed_Error()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var approver = TestDbFactory.SeedUser(db, Role.Approver);
        var (_, task) = TestDbFactory.SeedOpenApproverTask(db, admin);

        var service = new TaskService(db, new ProcessStateMachine());
        var approverDto = TestDbFactory.ToDto(approver);

        // First action should succeed
        var first = await service.ExecuteActionAsync(task.Id, new TaskActionRequest(WorkflowAction.Approve, "Approved."), approverDto);
        Assert.True(first.IsSuccess);

        // Second action on the same task should fail
        var second = await service.ExecuteActionAsync(task.Id, new TaskActionRequest(WorkflowAction.Approve, "Again."), approverDto);
        Assert.False(second.IsSuccess);
        Assert.Contains(second.Errors, e => e.Contains("already closed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Nonexistent_Task_Returns_Not_Found_Error()
    {
        using var db = TestDbFactory.Create();
        var service = new TaskService(db, new ProcessStateMachine());
        var userDto = new UserDto(Guid.NewGuid(), "test", "Test", Role.Approver);

        var result = await service.ExecuteActionAsync(Guid.NewGuid(), new TaskActionRequest(WorkflowAction.Approve, "test"), userDto);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteActionAsync_Rejects_Task_When_Community_Is_Deactivated()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.Admin, "starter");
        var approver = TestDbFactory.SeedUser(db, Role.Approver, "approver");
        var (_, task) = TestDbFactory.SeedOpenApproverTask(db, starter);
        db.Communities.Single(community => community.Id == TestDbFactory.CommunityId).IsActive = false;
        await db.SaveChangesAsync();
        var service = new TaskService(db, new ProcessStateMachine());

        var result = await service.ExecuteActionAsync(
            task.Id,
            new TaskActionRequest(WorkflowAction.Approve, "Should be blocked."),
            TestDbFactory.ToDto(approver));

        Assert.False(result.IsSuccess);
        Assert.Contains("community is not active", result.Errors.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProcessTaskStatus.Open, db.ProcessTasks.Single(item => item.Id == task.Id).Status);
    }
}
