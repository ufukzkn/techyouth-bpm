using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Workflow;

public class AuditLogTests
{
    [Fact]
    public async Task Approve_Creates_Audit_Log_With_Correct_Transition()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var approver = TestDbFactory.SeedUser(db, Role.Approver);
        var (process, task) = TestDbFactory.SeedOpenApproverTask(db, admin);

        var service = new TaskService(db, new ProcessStateMachine());
        var approverDto = new UserDto(approver.Id, approver.Username, approver.DisplayName, Role.Approver);

        await service.ExecuteActionAsync(task.Id, new TaskActionRequest(WorkflowAction.Approve, "Looks good."), approverDto);

        var logs = db.AuditLogs
            .Where(log => log.ProcessInstanceId == process.Id && log.Action == WorkflowAction.Approve)
            .ToList();

        Assert.Single(logs);
        var log = logs[0];
        Assert.Equal(ProcessStatus.InProgress, log.FromStatus);
        Assert.Equal(ProcessStatus.Completed, log.ToStatus);
        Assert.Equal(approver.Id, log.UserId);
        Assert.Equal("Looks good.", log.Note);
    }

    [Fact]
    public async Task Reject_Creates_Audit_Log_With_Correct_Transition()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var approver = TestDbFactory.SeedUser(db, Role.Approver);
        var (process, task) = TestDbFactory.SeedOpenApproverTask(db, admin);

        var service = new TaskService(db, new ProcessStateMachine());
        var approverDto = new UserDto(approver.Id, approver.Username, approver.DisplayName, Role.Approver);

        await service.ExecuteActionAsync(task.Id, new TaskActionRequest(WorkflowAction.Reject, "Not acceptable."), approverDto);

        var logs = db.AuditLogs
            .Where(log => log.ProcessInstanceId == process.Id && log.Action == WorkflowAction.Reject)
            .ToList();

        Assert.Single(logs);
        var log = logs[0];
        Assert.Equal(ProcessStatus.InProgress, log.FromStatus);
        Assert.Equal(ProcessStatus.Rejected, log.ToStatus);
        Assert.Equal(approver.Id, log.UserId);
        Assert.Equal("Not acceptable.", log.Note);
    }

    [Fact]
    public async Task Start_Audit_Log_Exists_From_Seed()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var (process, _) = TestDbFactory.SeedOpenApproverTask(db, admin);

        var startLogs = db.AuditLogs
            .Where(log => log.ProcessInstanceId == process.Id && log.Action == WorkflowAction.Start)
            .ToList();

        Assert.Single(startLogs);
        var log = startLogs[0];
        Assert.Equal(ProcessStatus.Pending, log.FromStatus);
        Assert.Equal(ProcessStatus.InProgress, log.ToStatus);
        Assert.Equal(admin.Id, log.UserId);
    }

    [Fact]
    public async Task Audit_Log_Preserves_Empty_Note()
    {
        using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var (process, task) = TestDbFactory.SeedOpenApproverTask(db, admin);

        var service = new TaskService(db, new ProcessStateMachine());
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, Role.Admin);

        await service.ExecuteActionAsync(task.Id, new TaskActionRequest(WorkflowAction.Approve, null), adminDto);

        var approveLog = db.AuditLogs
            .Where(log => log.ProcessInstanceId == process.Id && log.Action == WorkflowAction.Approve)
            .Single();

        Assert.Equal(string.Empty, approveLog.Note);
    }
}
