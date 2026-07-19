namespace TechYouthBpm.Application.Health;

public interface ISystemReadinessService
{
    Task<SystemReadinessReport> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed record SystemReadinessCheck(string Name, bool IsHealthy);

public sealed record SystemReadinessReport(IReadOnlyList<SystemReadinessCheck> Checks)
{
    public bool IsReady => Checks.Count > 0 && Checks.All(check => check.IsHealthy);
}
