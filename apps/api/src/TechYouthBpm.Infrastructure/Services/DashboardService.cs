using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Dashboard;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class DashboardService(
    AppDbContext db,
    IWorkflowVisibilityService workflowVisibilityService) : IDashboardService
{
    public DashboardService(AppDbContext db)
        : this(db, new WorkflowVisibilityService())
    {
    }

    public Task<DashboardSummaryDto> GetSummaryAsync(
        UserDto user,
        CancellationToken cancellationToken = default) =>
        GetSummaryAsync(user, WorkflowVisibilityScope.Personal, cancellationToken);

    public async Task<DashboardSummaryDto> GetSummaryAsync(
        UserDto user,
        WorkflowVisibilityScope scope,
        CancellationToken cancellationToken = default)
    {
        var openTaskCount = 0;
        int? teamQueueCount = null;
        IReadOnlyList<DashboardTaskItemDto> recentOpenTasks = [];
        if (user.HasPermission(PermissionNames.TasksView)
            || user.HasPermission(PermissionNames.TasksManageAll))
        {
            var taskQuery = workflowVisibilityService.ApplyTaskScope(
                db.ProcessTasks
                .AsNoTracking()
                .Where(task => task.Status == ProcessTaskStatus.Open
                    || task.Status == ProcessTaskStatus.Claimed),
                user,
                scope);

            openTaskCount = await taskQuery.CountAsync(cancellationToken);
            recentOpenTasks = await taskQuery
                .OrderByDescending(task => task.CreatedAt)
                .Take(4)
                .Select(task => new DashboardTaskItemDto(
                    task.Id,
                    task.ProcessInstanceId,
                    task.ProcessInstance != null && task.ProcessInstance.FormDefinition != null
                        ? task.ProcessInstance.FormDefinition.Name
                        : "Unknown form",
                    task.Status,
                    task.CreatedAt))
                .ToListAsync(cancellationToken);

            if (scope == WorkflowVisibilityScope.Personal
                && user.CommunityId is { } communityId
                && (user.Teams?.Count ?? 0) > 0)
            {
                var teamIds = user.Teams!.Select(team => team.Id).ToArray();
                teamQueueCount = await db.ProcessTasks
                    .AsNoTracking()
                    .CountAsync(task =>
                        task.ProcessInstance != null
                        && task.ProcessInstance.CommunityId == communityId
                        && (task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed)
                        && task.CandidateTeamId.HasValue
                        && teamIds.Contains(task.CandidateTeamId.Value),
                        cancellationToken);
            }
        }

        if (!user.HasPermission(PermissionNames.ProcessesView))
        {
            return new DashboardSummaryDto(openTaskCount, 0, 0, recentOpenTasks, [], teamQueueCount);
        }

        var processQuery = workflowVisibilityService.ApplyProcessScope(
            db.ProcessInstances.AsNoTracking(),
            user,
            scope);

        var processCounts = await processQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                InProgress = group.Count(process => process.Status == ProcessStatus.InProgress
                    || process.Status == ProcessStatus.Escalated),
                Completed = group.Count(process => process.Status == ProcessStatus.Completed),
            })
            .SingleOrDefaultAsync(cancellationToken);
        var recentProcesses = await processQuery
            .OrderByDescending(process => process.StartedAt)
            .Take(4)
            .Select(process => new DashboardProcessItemDto(
                process.Id,
                process.FormDefinition != null ? process.FormDefinition.Name : "Unknown form",
                process.Status,
                process.StartedAt))
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto(
            openTaskCount,
            processCounts?.InProgress ?? 0,
            processCounts?.Completed ?? 0,
            recentOpenTasks,
            recentProcesses,
            teamQueueCount);
    }
}
