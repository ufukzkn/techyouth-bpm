namespace TechYouthBpm.Application.Auth;

public record CommunityDeletionImpactDto(
    Guid CommunityId,
    string CommunityName,
    bool IsActive,
    int UserCount,
    int PreservedUserCount,
    int CommunityRoleCount,
    int TeamCount,
    int FormCount,
    int WorkflowCount,
    int ProcessCount,
    int TaskCount,
    int NotificationCount,
    int SystemAuditCount,
    int ProcessStepCount);

public record PurgeCommunityRequest(
    string ConfirmationName,
    string CurrentPassword,
    string Reason);

public record CommunityPurgeResultDto(
    Guid ArchiveId,
    Guid OriginalCommunityId,
    string CommunityName,
    DateTime DeletedAt,
    CommunityDeletionImpactDto Impact);
