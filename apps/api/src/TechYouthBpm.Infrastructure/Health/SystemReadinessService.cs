using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TechYouthBpm.Application.Health;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Health;

public sealed class SystemReadinessService(
    AppDbContext db,
    ILogger<SystemReadinessService> logger) : ISystemReadinessService
{
    public async Task<SystemReadinessReport> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return UnavailableReport();
            }

            var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            var activeSuperAdminCount = await db.Users
                .AsNoTracking()
                .CountAsync(
                    user => user.Role == Role.SuperAdmin && user.Status == UserStatus.Active,
                    cancellationToken);

            return new SystemReadinessReport([
                new SystemReadinessCheck("database", true),
                new SystemReadinessCheck("migrations", !pendingMigrations.Any()),
                new SystemReadinessCheck("superadmin", activeSuperAdminCount == 1),
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "System readiness evaluation failed.");
            return UnavailableReport();
        }
    }

    private static SystemReadinessReport UnavailableReport() => new([
        new SystemReadinessCheck("database", false),
        new SystemReadinessCheck("migrations", false),
        new SystemReadinessCheck("superadmin", false),
    ]);
}
