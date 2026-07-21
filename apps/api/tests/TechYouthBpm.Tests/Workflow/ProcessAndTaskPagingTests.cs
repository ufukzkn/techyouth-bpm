using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Workflow;

public class ProcessAndTaskPagingTests
{
    [Fact]
    public async Task Process_List_Applies_Server_Side_Paging_And_Preserves_Total_Count()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "paging-admin");
        var processes = Enumerable.Range(0, 13)
            .Select(_ => TestDbFactory.SeedOpenApproverTask(db, admin).Process)
            .OrderBy(process => process.StartedAt)
            .ToArray();
        var service = new ProcessService(
            db,
            new FormService(db),
            new ProcessStateMachine(),
            new SystemAuditService(db));

        var result = await service.ListAsync(
            new ProcessListRequest(Page: 2, PageSize: 5, SortBy: "startedAt", SortDirection: "asc"),
            TestDbFactory.CommunityAdminDto(admin));

        Assert.Equal(13, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(processes.Skip(5).Take(5).Select(process => process.Id), result.Items.Select(process => process.Id));
    }

    [Fact]
    public async Task Task_List_Sorts_Deadlines_With_Null_Values_Last()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.Admin, "deadline-starter");
        var approver = TestDbFactory.SeedUser(db, Role.Approver, "deadline-approver");
        var early = TestDbFactory.SeedOpenApproverTask(db, starter).Task;
        var late = TestDbFactory.SeedOpenApproverTask(db, starter).Task;
        var noDeadline = TestDbFactory.SeedOpenApproverTask(db, starter).Task;
        early.DueAt = DateTime.UtcNow.AddHours(2);
        late.DueAt = DateTime.UtcNow.AddDays(2);
        noDeadline.DueAt = null;
        await db.SaveChangesAsync();
        var service = new TaskService(db, new ProcessStateMachine());

        var firstPage = await service.ListMyTasksAsync(
            new TaskListRequest(Page: 1, PageSize: 2, SortBy: "dueAt", SortDirection: "asc"),
            TestDbFactory.ToDto(approver));
        var secondPage = await service.ListMyTasksAsync(
            new TaskListRequest(Page: 2, PageSize: 2, SortBy: "dueAt", SortDirection: "asc"),
            TestDbFactory.ToDto(approver));

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal([early.Id, late.Id], firstPage.Items.Select(task => task.Id));
        Assert.Equal(noDeadline.Id, Assert.Single(secondPage.Items).Id);
    }

    [Fact]
    public async Task Process_List_Sorts_By_Nearest_Open_Task_Deadline_With_No_Deadline_Last()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "process-deadline-admin");
        var early = TestDbFactory.SeedOpenApproverTask(db, admin);
        var late = TestDbFactory.SeedOpenApproverTask(db, admin);
        var noDeadline = TestDbFactory.SeedOpenApproverTask(db, admin);
        early.Task.DueAt = DateTime.UtcNow.AddHours(1);
        late.Task.DueAt = DateTime.UtcNow.AddDays(1);
        noDeadline.Task.DueAt = null;
        await db.SaveChangesAsync();
        var service = new ProcessService(
            db,
            new FormService(db),
            new ProcessStateMachine(),
            new SystemAuditService(db));

        var result = await service.ListAsync(
            new ProcessListRequest(PageSize: 10, SortBy: "dueAt", SortDirection: "asc"),
            TestDbFactory.CommunityAdminDto(admin));

        Assert.Equal(
            [early.Process.Id, late.Process.Id, noDeadline.Process.Id],
            result.Items.Select(process => process.Id));
    }

    [Fact]
    public async Task Task_List_Filters_Exact_Task_Without_Loading_An_Unrelated_Process()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.Admin, "deep-link-starter");
        var approver = TestDbFactory.SeedUser(db, Role.Approver, "deep-link-approver");
        _ = TestDbFactory.SeedOpenApproverTask(db, starter);
        var expected = TestDbFactory.SeedOpenApproverTask(db, starter).Task;

        var result = await new TaskService(db, new ProcessStateMachine()).ListMyTasksAsync(
            new TaskListRequest(TaskId: expected.Id),
            TestDbFactory.ToDto(approver));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(expected.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Active_Task_List_Hides_A_Task_Claimed_By_Another_User()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.Admin, "claim-starter");
        var currentUser = TestDbFactory.SeedUser(db, Role.Approver, "claim-current");
        var otherUser = TestDbFactory.SeedUser(db, Role.Approver, "claim-other");
        var visible = TestDbFactory.SeedOpenApproverTask(db, starter).Task;
        var hidden = TestDbFactory.SeedOpenApproverTask(db, starter).Task;
        hidden.Status = ProcessTaskStatus.Claimed;
        hidden.ClaimedByUserId = otherUser.Id;
        await db.SaveChangesAsync();

        var result = await new TaskService(db, new ProcessStateMachine()).ListMyTasksAsync(
            new TaskListRequest(View: "active", PageSize: 10),
            TestDbFactory.ToDto(currentUser));

        Assert.Contains(result.Items, task => task.Id == visible.Id);
        Assert.DoesNotContain(result.Items, task => task.Id == hidden.Id);
    }

    [Fact]
    public async Task History_Task_List_Returns_Only_Current_Actors_Completion_Details()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.Admin, "history-starter");
        var currentUser = TestDbFactory.SeedUser(db, Role.Approver, "history-current");
        var otherUser = TestDbFactory.SeedUser(db, Role.Approver, "history-other");
        var own = TestDbFactory.SeedOpenApproverTask(db, starter).Task;
        var other = TestDbFactory.SeedOpenApproverTask(db, starter).Task;
        own.Status = ProcessTaskStatus.Completed;
        own.CompletedByUserId = currentUser.Id;
        own.CompletedAt = DateTime.UtcNow;
        own.CompletedAction = WorkflowAction.Approve;
        own.CompletionNote = "Bütçe uygun.";
        other.Status = ProcessTaskStatus.Completed;
        other.CompletedByUserId = otherUser.Id;
        other.CompletedAt = DateTime.UtcNow.AddMinutes(-1);
        other.CompletedAction = WorkflowAction.Reject;
        await db.SaveChangesAsync();

        var result = await new TaskService(db, new ProcessStateMachine()).ListMyTasksAsync(
            new TaskListRequest(View: "history", PageSize: 10),
            TestDbFactory.ToDto(currentUser));

        var item = Assert.Single(result.Items);
        Assert.Equal(own.Id, item.Id);
        Assert.Equal(WorkflowAction.Approve, item.CompletedAction);
        Assert.Equal("Bütçe uygun.", item.CompletionNote);
        Assert.Equal(currentUser.DisplayName, item.CompletedByUserDisplayName);
    }
}
