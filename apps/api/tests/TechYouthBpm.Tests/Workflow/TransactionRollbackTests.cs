using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Workflow;

public class TransactionRollbackTests
{
    [Fact]
    public async Task Form_Update_Rolls_Back_Metadata_And_Fields_When_Audit_Fails()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var adminDto = TestDbFactory.ToDto(admin);
        var created = await new FormService(db).CreateAsync(
            FormRequest("Original Form", "originalField"),
            adminDto);
        var service = new FormService(db, new FailingSystemAuditService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(
            created.Value!.Id,
            FormRequest("Changed Form", "changedField"),
            adminDto));

        db.ChangeTracker.Clear();
        var persisted = await db.FormDefinitions
            .Include(form => form.Fields)
            .SingleAsync(form => form.Id == created.Value!.Id);
        Assert.Equal("Original Form", persisted.Name);
        Assert.Single(persisted.Fields);
        Assert.Equal("originalField", persisted.Fields.Single().Key);
    }

    [Fact]
    public async Task Process_Start_Rolls_Back_Process_Task_Notification_And_Audit_When_System_Audit_Fails()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var adminDto = TestDbFactory.ToDto(admin);
        var form = await new FormService(db).CreateAsync(
            FormRequest("Atomic Process Form", "requestTitle"),
            adminDto);
        var service = new ProcessService(
            db,
            new FormService(db),
            new ProcessStateMachine(),
            new FailingSystemAuditService());
        using var data = JsonDocument.Parse("{\"requestTitle\":\"Atomic request\"}");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(
            new StartProcessRequest(form.Value!.Id, data.RootElement.Clone()),
            adminDto));

        db.ChangeTracker.Clear();
        Assert.False(await db.ProcessInstances.AnyAsync(process => process.FormDefinitionId == form.Value!.Id));
        Assert.False(await db.ProcessTasks.AnyAsync());
        Assert.False(await db.Notifications.AnyAsync(notification => notification.EntityType == "ProcessInstance"));
        Assert.False(await db.SystemAuditLogs.AnyAsync(log => log.Action == "Process.Started"));
    }

    [Fact]
    public async Task Task_Action_Rolls_Back_Task_Process_Audit_And_Notification_When_System_Audit_Fails()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.Admin, "transaction-starter");
        var approver = TestDbFactory.SeedUser(db, Role.Approver, "transaction-approver");
        var (process, task) = TestDbFactory.SeedOpenApproverTask(db, starter);
        var service = new TaskService(db, new ProcessStateMachine(), new FailingSystemAuditService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteActionAsync(
            task.Id,
            new TaskActionRequest(WorkflowAction.Approve, "Should roll back."),
            TestDbFactory.ToDto(approver)));

        db.ChangeTracker.Clear();
        var persistedTask = await db.ProcessTasks.SingleAsync(item => item.Id == task.Id);
        var persistedProcess = await db.ProcessInstances.SingleAsync(item => item.Id == process.Id);
        Assert.Equal(ProcessTaskStatus.Open, persistedTask.Status);
        Assert.Null(persistedTask.CompletedAt);
        Assert.Equal(ProcessStatus.InProgress, persistedProcess.Status);
        Assert.DoesNotContain(
            await db.AuditLogs.Where(log => log.ProcessInstanceId == process.Id).ToListAsync(),
            log => log.Action == WorkflowAction.Approve);
        Assert.False(await db.Notifications.AnyAsync(notification => notification.EntityId == process.Id.ToString()));
    }

    private static CreateFormRequest FormRequest(string name, string fieldKey) =>
        new(
            name,
            "Transaction test form",
            [new CreateFormFieldRequest(fieldKey, "Test field", FieldType.Text, true, 1, [], [])]);
}
