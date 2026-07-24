using TechYouthBpm.Application.Audit;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface IAuditArchiveService
{
    Task<Result<IReadOnlyList<CommunityDeletionArchiveDto>>> ListArchivesAsync(
        UserDto currentUser,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ArchivedAuditEventDto>>> ListEventsAsync(
        Guid archiveId,
        UserDto currentUser,
        SystemAuditSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SystemAuditCategoryCountsDto>> CountEventsByCategoryAsync(
        Guid archiveId,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
}
