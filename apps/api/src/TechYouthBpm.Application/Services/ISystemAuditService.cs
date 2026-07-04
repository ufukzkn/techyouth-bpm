using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface ISystemAuditService
{
    Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default);

    Task LogAsync(
        UserDto actor,
        string action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SystemAuditLogDto>>> ListAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default);
}
