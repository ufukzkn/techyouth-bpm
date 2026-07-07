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
        var approverDto = new UserDto(approver.Id, approver.Username, approver.DisplayName, Role.Approver);

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
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, Role.Admin);

        var result = await service.ExecuteActionAsync(task.Id, new TaskActionRequest(WorkflowAction.Reject, "Admin rejected."), adminDto);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProcessStatus.Rejected, result.Value!.Status);
    }

    [Fact]
    public async Task Closed_Task_Returns_Already_Closed_Error()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var approver = TestDbFactory.SeedUser(db, Role.Approver);
        var (_, task) = TestDbFactory.SeedOpenApproverTask(db, admin);

        var service = new TaskService(db, new ProcessStateMachine());
        var approverDto = new UserDto(approver.Id, approver.Username, approver.DisplayName, Role.Approver);

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
}
