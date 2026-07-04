using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class SystemAuditService(AppDbContext db) : ISystemAuditService
{
    public async Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default)
    {
        db.SystemAuditLogs.Add(new SystemAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task LogAsync(
        UserDto actor,
        string action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default) =>
        LogAsync(actor.Id, action, entityType, entityId, description, cancellationToken);

    public async Task<Result<IReadOnlyList<SystemAuditLogDto>>> ListAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.Role != Role.Admin)
        {
            return Result<IReadOnlyList<SystemAuditLogDto>>.Failure("Only Admin users can view system audit logs.");
        }

        var logs = await db.SystemAuditLogs
            .Include(log => log.ActorUser)
            .OrderByDescending(log => log.CreatedAt)
            .Take(200)
            .Select(log => new SystemAuditLogDto(
                log.Id,
                log.ActorUserId,
                log.ActorUser != null ? log.ActorUser.DisplayName : "System",
                log.ActorUser != null ? log.ActorUser.Username : "system",
                log.Action,
                log.EntityType,
                log.EntityId,
                log.Description,
                log.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SystemAuditLogDto>>.Success(logs);
    }
}
