using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Teams;

namespace TechYouthBpm.Application.Services;

public interface ITeamService
{
    Task<Result<TeamPageDto>> ListAsync(TeamSearchRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> GetAsync(Guid teamId, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> CreateAsync(CreateTeamRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> UpdateAsync(Guid teamId, UpdateTeamRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<UserTeamMembershipDto>>> ListUserMembershipsAsync(Guid userId, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<TeamMemberPageDto>> ListMembersAsync(Guid teamId, TeamMemberSearchRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<TeamRosterPageDto>> ListRosterAsync(Guid teamId, TeamMemberSearchRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<ProcessTaskDto>>> ListMemberTasksAsync(Guid teamId, Guid userId, TeamMemberTaskSearchRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<TeamCandidatePageDto>> ListCandidatesAsync(Guid teamId, TeamMemberSearchRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<TeamCandidatePageDto>> ListUnassignedAsync(UnassignedTeamMemberSearchRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<TeamMemberDto>> AddMemberAsync(Guid teamId, AddTeamMemberRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result<TeamMemberDto>> UpdateMemberAsync(Guid teamId, Guid userId, UpdateTeamMemberRequest request, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result> RemoveMemberAsync(Guid teamId, Guid userId, UserDto currentUser, CancellationToken cancellationToken = default);
}
