using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface ICommunityDeletionService
{
    Task<Result<CommunityDeletionImpactDto>> GetImpactAsync(
        Guid communityId,
        UserDto currentUser,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityPurgeResultDto>> PurgeAsync(
        Guid communityId,
        PurgeCommunityRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default);
}
