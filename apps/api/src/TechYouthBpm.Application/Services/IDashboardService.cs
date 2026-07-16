using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Dashboard;
using TechYouthBpm.Application.Processes;

namespace TechYouthBpm.Application.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(UserDto user, CancellationToken cancellationToken = default);
    Task<DashboardSummaryDto> GetSummaryAsync(
        UserDto user,
        WorkflowVisibilityScope scope,
        CancellationToken cancellationToken = default);
}
