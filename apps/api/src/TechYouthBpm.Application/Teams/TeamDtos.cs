namespace TechYouthBpm.Application.Teams;

public sealed class TeamSearchRequest
{
    public Guid? CommunityId { get; init; }
    public string? Query { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed class TeamMemberSearchRequest
{
    public string? Query { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed class UnassignedTeamMemberSearchRequest
{
    public Guid? CommunityId { get; init; }
    public string? Query { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public record CreateTeamRequest(Guid CommunityId, string Name, string Description);

public record UpdateTeamRequest(string Name, string Description, bool IsActive);

public record AddTeamMemberRequest(Guid UserId, bool IsLead = false);

public record UpdateTeamMemberRequest(bool IsLead);

public record TeamDto(
    Guid Id,
    Guid CommunityId,
    string CommunityName,
    string Name,
    string Description,
    bool IsActive,
    int MemberCount,
    int LeadCount,
    Guid? CreatedByUserId,
    string CreatedByDisplayName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record TeamPageDto(
    IReadOnlyList<TeamDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int UnassignedCount);

public record TeamMemberDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string Email,
    string CommunityRoleName,
    bool IsLead,
    DateTime JoinedAt);

public record TeamMemberPageDto(
    IReadOnlyList<TeamMemberDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record UserTeamMembershipDto(
    Guid TeamId,
    string TeamName,
    bool TeamIsActive,
    bool IsLead,
    DateTime JoinedAt);

public record TeamRosterMemberDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string CommunityRoleName,
    bool IsLead);

public record TeamRosterPageDto(
    IReadOnlyList<TeamRosterMemberDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record TeamCandidateDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string Email,
    string CommunityRoleName,
    int ActiveTeamCount);

public record TeamCandidatePageDto(
    IReadOnlyList<TeamCandidateDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
