using Microsoft.Extensions.Diagnostics.HealthChecks;
using TechYouthBpm.Application.Health;

namespace TechYouthBpm.Api.Health;

public sealed class SystemReadinessHealthCheck(
    ISystemReadinessService readinessService) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var report = await readinessService.CheckAsync(cancellationToken);
        var data = report.Checks.ToDictionary(
            check => check.Name,
            check => (object)check.IsHealthy,
            StringComparer.OrdinalIgnoreCase);

        return report.IsReady
            ? HealthCheckResult.Healthy(data: data)
            : HealthCheckResult.Unhealthy("System readiness checks failed.", data: data);
    }
}
