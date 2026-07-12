using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;

namespace TechYouthBpm.Tests;

internal sealed class FailingSystemAuditService : ISystemAuditService
{
    private static InvalidOperationException Failure() => new("Simulated audit persistence failure.");

    public Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default) => Task.FromException(Failure());

    public Task LogAsync(
        UserDto actor,
        string action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default) => Task.FromException(Failure());

    public Task<Result<PagedResult<SystemAuditLogDto>>> ListAsync(
        UserDto currentUser,
        SystemAuditSearchRequest request,
        CancellationToken cancellationToken = default) => Task.FromException<Result<PagedResult<SystemAuditLogDto>>>(Failure());

    public Task<Result<SystemAuditCategoryCountsDto>> CountByCategoryAsync(
        UserDto currentUser,
        string? query = null,
        CancellationToken cancellationToken = default) => Task.FromException<Result<SystemAuditCategoryCountsDto>>(Failure());
}
