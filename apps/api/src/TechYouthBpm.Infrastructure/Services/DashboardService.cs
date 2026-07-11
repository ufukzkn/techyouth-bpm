using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Dashboard;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class DashboardService(AppDbContext db) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(UserDto user, CancellationToken cancellationToken = default)
    {
        var openTaskCount = 0;
        if (user.HasPermission(PermissionNames.TasksView))
        {
            var taskQuery = db.ProcessTasks
                .AsNoTracking()
                .Where(task => task.Status == ProcessTaskStatus.Open);

            if (!user.IsSuperAdmin())
            {
                var permissions = user.Permissions ?? [];
                taskQuery = user.CommunityId is null
                    ? taskQuery.Where(_ => false)
                    : taskQuery.Where(task => task.ProcessInstance != null
                        && task.ProcessInstance.CommunityId == user.CommunityId
                        && permissions.Contains(task.RequiredPermission));
            }

            openTaskCount = await taskQuery.CountAsync(cancellationToken);
        }

        if (!user.HasPermission(PermissionNames.ProcessesView))
        {
            return new DashboardSummaryDto(openTaskCount, 0, 0);
        }

        var processQuery = db.ProcessInstances.AsNoTracking();
        if (!user.IsSuperAdmin() && user.CommunityId is not null)
        {
            processQuery = processQuery.Where(process => process.CommunityId == user.CommunityId);
        }

        if (!user.HasPermission(PermissionNames.TasksView))
        {
            processQuery = processQuery.Where(process => process.StartedByUserId == user.Id);
        }

        var inProgressProcessCount = await processQuery.CountAsync(
            process => process.Status == ProcessStatus.InProgress,
            cancellationToken);
        var completedProcessCount = await processQuery.CountAsync(
            process => process.Status == ProcessStatus.Completed,
            cancellationToken);

        return new DashboardSummaryDto(openTaskCount, inProgressProcessCount, completedProcessCount);
    }
}
