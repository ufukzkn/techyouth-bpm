using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Application.Dashboard;

public record DashboardTaskItemDto(
    Guid Id,
    Guid ProcessInstanceId,
    string FormName,
    ProcessTaskStatus Status,
    DateTime CreatedAt);

public record DashboardProcessItemDto(
    Guid Id,
    string FormName,
    ProcessStatus Status,
    DateTime StartedAt);

public record DashboardSummaryDto(
    int OpenTaskCount,
    int InProgressProcessCount,
    int CompletedProcessCount,
    IReadOnlyList<DashboardTaskItemDto>? RecentOpenTasks = null,
    IReadOnlyList<DashboardProcessItemDto>? RecentProcesses = null,
    int? TeamQueueCount = null);
