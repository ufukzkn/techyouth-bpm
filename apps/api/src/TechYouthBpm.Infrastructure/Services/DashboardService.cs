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
        IReadOnlyList<DashboardTaskItemDto> recentOpenTasks = [];
        if (user.HasPermission(PermissionNames.TasksView))
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
        }

        if (!user.HasPermission(PermissionNames.ProcessesView))
        {
            return new DashboardSummaryDto(openTaskCount, 0, 0, recentOpenTasks, []);
        }

        var processQuery = workflowVisibilityService.ApplyProcessScope(
            db.ProcessInstances.AsNoTracking(),
            user,
            scope);

        var inProgressProcessCount = await processQuery.CountAsync(
            process => process.Status == ProcessStatus.InProgress
                || process.Status == ProcessStatus.Escalated,
            cancellationToken);
        var completedProcessCount = await processQuery.CountAsync(
            process => process.Status == ProcessStatus.Completed,
            cancellationToken);
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
            inProgressProcessCount,
            completedProcessCount,
            recentOpenTasks,
            recentProcesses);
    }
}
