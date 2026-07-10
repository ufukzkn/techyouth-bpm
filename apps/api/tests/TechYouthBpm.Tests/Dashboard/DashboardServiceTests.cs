using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Dashboard;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_Applies_User_Process_And_Task_Permissions()
    {
        await using var db = TestDbFactory.Create();
        var starter = TestDbFactory.SeedUser(db, Role.User, "process-starter");
        var approver = TestDbFactory.SeedUser(db, Role.Approver, "approver");
        var inProgressProcess = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = Guid.NewGuid(),
            CommunityId = TestDbFactory.CommunityId,
            StartedByUserId = starter.Id,
            Status = ProcessStatus.InProgress,
            FormDataJson = "{}",
            StartedAt = DateTime.UtcNow
        };
        db.ProcessInstances.AddRange(
            inProgressProcess,
            new ProcessInstance
            {
                Id = Guid.NewGuid(),
                FormDefinitionId = Guid.NewGuid(),
                CommunityId = TestDbFactory.CommunityId,
                StartedByUserId = starter.Id,
                Status = ProcessStatus.Completed,
                FormDataJson = "{}",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
        db.ProcessTasks.Add(new ProcessTask
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = inProgressProcess.Id,
            AssignedRole = Role.User,
            RequiredPermission = PermissionNames.TasksAct,
            Status = ProcessTaskStatus.Open,
            AvailableActionsJson = "[]",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new DashboardService(db);

        var starterSummary = await service.GetSummaryAsync(TestDbFactory.ToDto(starter));
        var approverSummary = await service.GetSummaryAsync(TestDbFactory.ToDto(approver));

        Assert.Equal(0, starterSummary.OpenTaskCount);
        Assert.Equal(1, starterSummary.InProgressProcessCount);
        Assert.Equal(1, starterSummary.CompletedProcessCount);
        Assert.Equal(1, approverSummary.OpenTaskCount);
        Assert.Equal(1, approverSummary.InProgressProcessCount);
        Assert.Equal(1, approverSummary.CompletedProcessCount);
    }
}
