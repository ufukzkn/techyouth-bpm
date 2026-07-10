using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Dashboard;

namespace TechYouthBpm.Application.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(UserDto user, CancellationToken cancellationToken = default);
}
