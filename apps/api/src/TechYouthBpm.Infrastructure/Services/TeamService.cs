using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Application.Teams;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class TeamService(
    AppDbContext db,
    ISystemAuditService auditService,
    INotificationService notificationService,
    ISessionValidationCache? sessionCache = null) : ITeamService
{
    public async Task<Result<TeamPageDto>> ListAsync(
        TeamSearchRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveScope(currentUser, request.CommunityId, PermissionNames.TeamsView, out var communityId))
        {
            return Result<TeamPageDto>.Failure("Current user cannot view teams in this community.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var query = db.Teams.AsNoTracking();
        if (communityId is not null)
        {
            query = query.Where(team => team.CommunityId == communityId);
        }
        if (request.IsActive is not null)
        {
            query = query.Where(team => team.IsActive == request.IsActive);
        }
        var search = request.Query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(team => team.Name.Contains(search) || team.Description.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(team => team.Name)
            .ThenBy(team => team.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(team => new TeamDto(
                team.Id,
                team.CommunityId,
                team.Community != null ? team.Community.Name : string.Empty,
                team.Name,
                team.Description,
                team.IsActive,
                team.Memberships.Count(membership => membership.IsActive
                    && membership.User != null
                    && membership.User.Status == UserStatus.Active
                    && membership.User.CommunityMemberships.Any(communityMembership =>
                        communityMembership.IsActive && communityMembership.CommunityId == team.CommunityId)),
                team.Memberships.Count(membership => membership.IsActive
                    && membership.IsLead
                    && membership.User != null
                    && membership.User.Status == UserStatus.Active
                    && membership.User.CommunityMemberships.Any(communityMembership =>
                        communityMembership.IsActive && communityMembership.CommunityId == team.CommunityId)),
                team.CreatedByUserId,
                team.CreatedByUser != null ? team.CreatedByUser.DisplayName : string.Empty,
                team.CreatedAt,
                team.UpdatedAt))
            .ToListAsync(cancellationToken);
        var unassignedCount = await CountUnassignedAsync(communityId, cancellationToken);

        return Result<TeamPageDto>.Success(new TeamPageDto(items, page, pageSize, totalCount, unassignedCount));
    }

    public async Task<Result<TeamDto>> GetAsync(Guid teamId, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null)
        {
            return Result<TeamDto>.Failure("Team was not found.");
        }
        if (!CanAccessCommunity(currentUser, team.CommunityId, PermissionNames.TeamsView))
        {
            return Result<TeamDto>.Failure("Current user cannot view this team.");
        }

        return Result<TeamDto>.Success(await GetTeamDtoAsync(teamId, cancellationToken));
    }

    public async Task<Result<TeamDto>> CreateAsync(
        CreateTeamRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessCommunity(currentUser, request.CommunityId, PermissionNames.TeamsManage))
        {
            return Result<TeamDto>.Failure("Current user cannot create teams in this community.");
        }
        if (!await db.Communities.AnyAsync(community => community.Id == request.CommunityId, cancellationToken))
        {
            return Result<TeamDto>.Failure("Community was not found.");
        }

        var errors = ValidateTeam(request.Name, request.Description);
        if (errors.Count > 0)
        {
            return Result<TeamDto>.Failure(errors);
        }
        var normalizedName = NormalizeName(request.Name);
        if (await db.Teams.AnyAsync(team => team.CommunityId == request.CommunityId && team.NormalizedName == normalizedName, cancellationToken))
        {
            return Result<TeamDto>.Failure("Team name is already used in this community.");
        }

        var now = DateTime.UtcNow;
        var team = new Team
        {
            Id = Guid.NewGuid(),
            CommunityId = request.CommunityId,
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = true,
            CreatedByUserId = currentUser.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Teams.Add(team);
        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(currentUser, "Team.Created", "Team", team.Id.ToString(), $"Team '{team.Name}' was created.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<TeamDto>.Success(await GetTeamDtoAsync(team.Id, cancellationToken));
    }

    public async Task<Result<TeamDto>> UpdateAsync(
        Guid teamId,
        UpdateTeamRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null)
        {
            return Result<TeamDto>.Failure("Team was not found.");
        }
        if (!CanAccessCommunity(currentUser, team.CommunityId, PermissionNames.TeamsManage))
        {
            return Result<TeamDto>.Failure("Current user cannot update this team.");
        }

        var errors = ValidateTeam(request.Name, request.Description);
        if (errors.Count > 0)
        {
            return Result<TeamDto>.Failure(errors);
        }
        var normalizedName = NormalizeName(request.Name);
        if (await db.Teams.AnyAsync(item => item.Id != teamId
                && item.CommunityId == team.CommunityId
                && item.NormalizedName == normalizedName,
            cancellationToken))
        {
            return Result<TeamDto>.Failure("Team name is already used in this community.");
        }

        var wasActive = team.IsActive;
        team.Name = request.Name.Trim();
        team.NormalizedName = normalizedName;
        team.Description = request.Description?.Trim() ?? string.Empty;
        team.IsActive = request.IsActive;
        team.UpdatedAt = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        sessionCache?.InvalidateCommunity(team.CommunityId);
        await auditService.LogAsync(
            currentUser,
            wasActive == team.IsActive ? "Team.Updated" : team.IsActive ? "Team.Activated" : "Team.Deactivated",
            "Team",
            team.Id.ToString(),
            $"Team '{team.Name}' was updated. Active: {wasActive} -> {team.IsActive}.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<TeamDto>.Success(await GetTeamDtoAsync(team.Id, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<UserTeamMembershipDto>>> ListUserMembershipsAsync(
        Guid userId,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var target = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.Id,
                CommunityId = user.CommunityMemberships
                    .Where(membership => membership.IsActive)
                    .Select(membership => (Guid?)membership.CommunityId)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return Result<IReadOnlyList<UserTeamMembershipDto>>.Failure("User was not found.");
        }

        var canView = currentUser.Id == userId
            || currentUser.IsSuperAdmin()
            || (target.CommunityId is not null
                && currentUser.CommunityId == target.CommunityId
                && currentUser.HasPermission(PermissionNames.TeamsManage));
        if (!canView)
        {
            return Result<IReadOnlyList<UserTeamMembershipDto>>.Failure("Current user cannot view these team memberships.");
        }

        var memberships = await db.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.IsActive && membership.Team != null)
            .OrderBy(membership => membership.Team!.Name)
            .Select(membership => new UserTeamMembershipDto(
                membership.TeamId,
                membership.Team!.Name,
                membership.Team.IsActive,
                membership.IsLead,
                membership.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<UserTeamMembershipDto>>.Success(memberships);
    }

    public async Task<Result<TeamMemberPageDto>> ListMembersAsync(
        Guid teamId,
        TeamMemberSearchRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null)
        {
            return Result<TeamMemberPageDto>.Failure("Team was not found.");
        }
        if (!CanAccessCommunity(currentUser, team.CommunityId, PermissionNames.TeamsView))
        {
            return Result<TeamMemberPageDto>.Failure("Current user cannot view team members.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var query = db.TeamMemberships.AsNoTracking()
            .Where(membership => membership.TeamId == teamId
                && membership.IsActive
                && membership.User != null
                && membership.User.Status == UserStatus.Active
                && membership.User.CommunityMemberships.Any(communityMembership =>
                    communityMembership.IsActive && communityMembership.CommunityId == team.CommunityId));
        var search = request.Query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(membership => membership.User!.Username.Contains(search)
                || membership.User.DisplayName.Contains(search)
                || membership.User.Email.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(membership => membership.IsLead)
            .ThenBy(membership => membership.User!.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(membership => new TeamMemberDto(
                membership.UserId,
                membership.User!.Username,
                membership.User.DisplayName,
                membership.User.Email,
                membership.User.CommunityMemberships
                    .Where(communityMembership => communityMembership.IsActive && communityMembership.CommunityId == team.CommunityId)
                    .Select(communityMembership => communityMembership.CommunityRole != null ? communityMembership.CommunityRole.Name : string.Empty)
                    .FirstOrDefault() ?? string.Empty,
                membership.IsLead,
                membership.CreatedAt,
                db.ProcessTasks.Count(task =>
                    (task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed)
                    && (task.AssignedUserId == membership.UserId
                        || task.ClaimedByUserId == membership.UserId))))
            .ToListAsync(cancellationToken);

        return Result<TeamMemberPageDto>.Success(new TeamMemberPageDto(items, page, pageSize, totalCount));
    }

    public async Task<Result<TeamRosterPageDto>> ListRosterAsync(
        Guid teamId,
        TeamMemberSearchRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var team = await db.Teams
            .AsNoTracking()
            .Select(item => new { item.Id, item.CommunityId, item.IsActive })
            .SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null)
        {
            return Result<TeamRosterPageDto>.Failure("Team was not found.");
        }

        var hasCommunityPermission = CanAccessCommunity(currentUser, team.CommunityId, PermissionNames.TeamsView);
        var isActiveMember = team.IsActive
            && currentUser.CommunityId == team.CommunityId
            && await db.TeamMemberships.AsNoTracking().AnyAsync(
                membership => membership.TeamId == teamId
                    && membership.UserId == currentUser.Id
                    && membership.IsActive,
                cancellationToken);
        if (!hasCommunityPermission && !isActiveMember)
        {
            return Result<TeamRosterPageDto>.Failure("Current user cannot view this team roster.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var query = db.TeamMemberships.AsNoTracking()
            .Where(membership => membership.TeamId == teamId
                && membership.IsActive
                && membership.User != null
                && membership.User.Status == UserStatus.Active
                && membership.User.CommunityMemberships.Any(communityMembership =>
                    communityMembership.IsActive && communityMembership.CommunityId == team.CommunityId));
        var search = request.Query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(membership => membership.User!.Username.Contains(search)
                || membership.User.DisplayName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(membership => membership.IsLead)
            .ThenBy(membership => membership.User!.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(membership => new TeamRosterMemberDto(
                membership.UserId,
                membership.User!.Username,
                membership.User.DisplayName,
                membership.User.CommunityMemberships
                    .Where(communityMembership => communityMembership.IsActive
                        && communityMembership.CommunityId == team.CommunityId)
                    .Select(communityMembership => communityMembership.CommunityRole != null
                        ? communityMembership.CommunityRole.Name
                        : string.Empty)
                    .FirstOrDefault() ?? string.Empty,
                membership.IsLead,
                db.ProcessTasks.Count(task =>
                    (task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed)
                    && (task.AssignedUserId == membership.UserId
                        || task.ClaimedByUserId == membership.UserId))))
            .ToListAsync(cancellationToken);

        return Result<TeamRosterPageDto>.Success(new TeamRosterPageDto(items, page, pageSize, totalCount));
    }

    public async Task<Result<PagedResult<ProcessTaskDto>>> ListMemberTasksAsync(
        Guid teamId,
        Guid userId,
        TeamMemberTaskSearchRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var team = await db.Teams
            .AsNoTracking()
            .Select(item => new { item.Id, item.CommunityId })
            .SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null)
        {
            return Result<PagedResult<ProcessTaskDto>>.Failure("Team was not found.");
        }

        var targetIsMember = await db.TeamMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.TeamId == teamId
                && membership.UserId == userId
                && membership.IsActive,
                cancellationToken);
        if (!targetIsMember)
        {
            return Result<PagedResult<ProcessTaskDto>>.Failure("Active team membership was not found.");
        }

        var isTeamLead = currentUser.CommunityId == team.CommunityId
            && await db.TeamMemberships
                .AsNoTracking()
                .AnyAsync(membership =>
                    membership.TeamId == teamId
                    && membership.UserId == currentUser.Id
                    && membership.IsActive
                    && membership.IsLead,
                    cancellationToken);
        var canViewDetails = currentUser.IsSuperAdmin()
            || (currentUser.CommunityId == team.CommunityId
                && currentUser.HasPermission(PermissionNames.TeamsManage))
            || isTeamLead;
        if (!canViewDetails)
        {
            return Result<PagedResult<ProcessTaskDto>>.Failure(
                "Only a team lead or team manager can view member task details.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var query = TeamMemberTaskQuery()
            .AsNoTracking()
            .Where(task =>
                task.ProcessInstance != null
                && task.ProcessInstance.CommunityId == team.CommunityId
                && (task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed)
                && (task.AssignedUserId == userId || task.ClaimedByUserId == userId));
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(task => task.DueAt == null)
            .ThenBy(task => task.DueAt)
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<ProcessTaskDto>>.Success(new PagedResult<ProcessTaskDto>(
            items.Select(task => task.ToDto(currentUser)).ToArray(),
            page,
            pageSize,
            totalCount));
    }

    public async Task<Result<TeamCandidatePageDto>> ListCandidatesAsync(
        Guid teamId,
        TeamMemberSearchRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null)
        {
            return Result<TeamCandidatePageDto>.Failure("Team was not found.");
        }
        if (!CanAccessCommunity(currentUser, team.CommunityId, PermissionNames.TeamsManage))
        {
            return Result<TeamCandidatePageDto>.Failure("Current user cannot list team candidates.");
        }

        var query = CandidateQuery(team.CommunityId)
            .Where(membership => !db.TeamMemberships.Any(teamMembership =>
                teamMembership.TeamId == teamId
                && teamMembership.UserId == membership.UserId
                && teamMembership.IsActive));
        return Result<TeamCandidatePageDto>.Success(await CandidatePageAsync(query, request.Query, request.Page, request.PageSize, cancellationToken));
    }

    public async Task<Result<TeamCandidatePageDto>> ListUnassignedAsync(
        UnassignedTeamMemberSearchRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveScope(currentUser, request.CommunityId, PermissionNames.TeamsView, out var communityId))
        {
            return Result<TeamCandidatePageDto>.Failure("Current user cannot view unassigned team members.");
        }
        if (communityId is null)
        {
            return Result<TeamCandidatePageDto>.Failure("A community must be selected to view unassigned members.");
        }

        var query = CandidateQuery(communityId.Value)
            .Where(membership => !db.TeamMemberships.Any(teamMembership =>
                teamMembership.UserId == membership.UserId
                && teamMembership.IsActive
                && teamMembership.Team != null
                && teamMembership.Team.IsActive
                && teamMembership.Team.CommunityId == communityId.Value));
        return Result<TeamCandidatePageDto>.Success(await CandidatePageAsync(query, request.Query, request.Page, request.PageSize, cancellationToken));
    }

    public async Task<Result<TeamMemberDto>> AddMemberAsync(
        Guid teamId,
        AddTeamMemberRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.SingleOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null)
        {
            return Result<TeamMemberDto>.Failure("Team was not found.");
        }
        if (!CanAccessCommunity(currentUser, team.CommunityId, PermissionNames.TeamsManage))
        {
            return Result<TeamMemberDto>.Failure("Current user cannot add team members.");
        }
        if (!team.IsActive)
        {
            return Result<TeamMemberDto>.Failure("Members cannot be added to an inactive team.");
        }

        var user = await EligibleUserQuery(team.CommunityId).SingleOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<TeamMemberDto>.Failure("Only active approved users from the same community can join this team.");
        }
        var membership = await db.TeamMemberships.SingleOrDefaultAsync(
            item => item.TeamId == teamId && item.UserId == request.UserId,
            cancellationToken);
        if (membership?.IsActive == true)
        {
            return Result<TeamMemberDto>.Failure("User is already an active member of this team.");
        }

        var now = DateTime.UtcNow;
        if (membership is null)
        {
            membership = new TeamMembership
            {
                Id = Guid.NewGuid(),
                TeamId = teamId,
                UserId = user.Id,
                IsLead = request.IsLead,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.TeamMemberships.Add(membership);
        }
        else
        {
            membership.IsLead = request.IsLead;
            membership.IsActive = true;
            membership.UpdatedAt = now;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        sessionCache?.InvalidateUser(user.Id);
        await notificationService.CreateAsync(new CreateNotificationRequest(
            user.Id,
            "Team.MembershipAdded",
            "Takim uyeliginiz guncellendi",
            $"{team.Name} takimina eklendiniz.",
            "Team",
            team.Id.ToString()), cancellationToken);
        await auditService.LogAsync(currentUser, "Team.MemberAdded", "User", user.Id.ToString(), $"User '{user.Username}' was added to team '{team.Name}'.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<TeamMemberDto>.Success(ToMemberDto(user, membership, team.CommunityId));
    }

    public async Task<Result<TeamMemberDto>> UpdateMemberAsync(
        Guid teamId,
        Guid userId,
        UpdateTeamMemberRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var membership = await db.TeamMemberships
            .Include(item => item.Team)
            .Include(item => item.User)
            .ThenInclude(user => user!.CommunityMemberships)
            .ThenInclude(communityMembership => communityMembership.CommunityRole)
            .SingleOrDefaultAsync(item => item.TeamId == teamId && item.UserId == userId && item.IsActive, cancellationToken);
        if (membership?.Team is null || membership.User is null)
        {
            return Result<TeamMemberDto>.Failure("Active team membership was not found.");
        }
        if (!CanAccessCommunity(currentUser, membership.Team.CommunityId, PermissionNames.TeamsManage))
        {
            return Result<TeamMemberDto>.Failure("Current user cannot update team members.");
        }

        var wasLead = membership.IsLead;
        membership.IsLead = request.IsLead;
        membership.UpdatedAt = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        sessionCache?.InvalidateUser(membership.UserId);
        if (wasLead != membership.IsLead)
        {
            await notificationService.CreateAsync(new CreateNotificationRequest(
                membership.UserId,
                "Team.LeadershipUpdated",
                "Takim sorumlulugunuz guncellendi",
                membership.IsLead
                    ? $"{membership.Team.Name} takimi icin sorumlu olarak belirlendiniz."
                    : $"{membership.Team.Name} takimi icin sorumlu goreviniz kaldirildi.",
                "Team",
                membership.TeamId.ToString()), cancellationToken);
        }
        await auditService.LogAsync(currentUser, "Team.MemberUpdated", "User", membership.UserId.ToString(), $"User '{membership.User.Username}' lead state in team '{membership.Team.Name}' changed from {wasLead} to {membership.IsLead}.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<TeamMemberDto>.Success(ToMemberDto(membership.User, membership, membership.Team.CommunityId));
    }

    public async Task<Result> RemoveMemberAsync(
        Guid teamId,
        Guid userId,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var membership = await db.TeamMemberships
            .Include(item => item.Team)
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.TeamId == teamId && item.UserId == userId && item.IsActive, cancellationToken);
        if (membership?.Team is null || membership.User is null)
        {
            return Result.Failure("Active team membership was not found.");
        }
        if (!CanAccessCommunity(currentUser, membership.Team.CommunityId, PermissionNames.TeamsManage))
        {
            return Result.Failure("Current user cannot remove team members.");
        }

        membership.IsActive = false;
        membership.IsLead = false;
        membership.UpdatedAt = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        sessionCache?.InvalidateUser(membership.UserId);
        await notificationService.CreateAsync(new CreateNotificationRequest(
            membership.UserId,
            "Team.MembershipRemoved",
            "Takim uyeliginiz guncellendi",
            $"{membership.Team.Name} takimindaki uyeliginiz kaldirildi.",
            "Team",
            membership.TeamId.ToString()), cancellationToken);
        await auditService.LogAsync(currentUser, "Team.MemberRemoved", "User", membership.UserId.ToString(), $"User '{membership.User.Username}' was removed from team '{membership.Team.Name}'.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<UserCommunityMembership> CandidateQuery(Guid communityId) =>
        db.UserCommunityMemberships.AsNoTracking()
            .Where(membership => membership.CommunityId == communityId
                && membership.IsActive
                && membership.User != null
                && membership.User.Status == UserStatus.Active);

    private IQueryable<User> EligibleUserQuery(Guid communityId) =>
        db.Users
            .Include(user => user.CommunityMemberships)
            .ThenInclude(membership => membership.CommunityRole)
            .Where(user => user.Status == UserStatus.Active
                && user.CommunityMemberships.Any(membership => membership.IsActive && membership.CommunityId == communityId));

    private async Task<TeamCandidatePageDto> CandidatePageAsync(
        IQueryable<UserCommunityMembership> query,
        string? searchQuery,
        int requestedPage,
        int requestedPageSize,
        CancellationToken cancellationToken)
    {
        var search = searchQuery?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(membership => membership.User!.Username.Contains(search)
                || membership.User.DisplayName.Contains(search)
                || membership.User.Email.Contains(search));
        }
        var page = Math.Max(1, requestedPage);
        var pageSize = Math.Clamp(requestedPageSize, 1, 50);
        var totalCount = await query.Select(membership => membership.UserId).Distinct().CountAsync(cancellationToken);
        var items = await query
            .OrderBy(membership => membership.User!.DisplayName)
            .ThenBy(membership => membership.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(membership => new TeamCandidateDto(
                membership.UserId,
                membership.User!.Username,
                membership.User.DisplayName,
                membership.User.Email,
                membership.CommunityRole != null ? membership.CommunityRole.Name : string.Empty,
                db.TeamMemberships.Count(teamMembership =>
                    teamMembership.UserId == membership.UserId
                    && teamMembership.IsActive
                    && teamMembership.Team != null
                    && teamMembership.Team.IsActive)))
            .ToListAsync(cancellationToken);
        return new TeamCandidatePageDto(items, page, pageSize, totalCount);
    }

    private async Task<int> CountUnassignedAsync(Guid? communityId, CancellationToken cancellationToken)
    {
        var query = db.UserCommunityMemberships.AsNoTracking()
            .Where(membership => membership.IsActive
                && membership.User != null
                && membership.User.Status == UserStatus.Active);
        if (communityId is not null)
        {
            query = query.Where(membership => membership.CommunityId == communityId);
        }
        return await query
            .Where(membership => !db.TeamMemberships.Any(teamMembership =>
                teamMembership.UserId == membership.UserId
                && teamMembership.IsActive
                && teamMembership.Team != null
                && teamMembership.Team.IsActive
                && teamMembership.Team.CommunityId == membership.CommunityId))
            .Select(membership => membership.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private async Task<TeamDto> GetTeamDtoAsync(Guid teamId, CancellationToken cancellationToken) =>
        await db.Teams.AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => new TeamDto(
                team.Id,
                team.CommunityId,
                team.Community != null ? team.Community.Name : string.Empty,
                team.Name,
                team.Description,
                team.IsActive,
                team.Memberships.Count(membership => membership.IsActive
                    && membership.User != null
                    && membership.User.Status == UserStatus.Active),
                team.Memberships.Count(membership => membership.IsActive
                    && membership.IsLead
                    && membership.User != null
                    && membership.User.Status == UserStatus.Active),
                team.CreatedByUserId,
                team.CreatedByUser != null ? team.CreatedByUser.DisplayName : string.Empty,
                team.CreatedAt,
                team.UpdatedAt))
            .SingleAsync(cancellationToken);

    private IQueryable<ProcessTask> TeamMemberTaskQuery() =>
        db.ProcessTasks
            .Include(task => task.AssignedCommunityRole)
            .Include(task => task.AssignedUser)
            .Include(task => task.CandidateTeam)
            .Include(task => task.CandidateCommunityRole)
            .Include(task => task.ClaimedByUser)
            .Include(task => task.CompletedByUser)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.FormDefinition)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.Community)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.ProcessDefinitionVersion)
            .ThenInclude(version => version!.ProcessDefinition)
            .AsSplitQuery();

    private static TeamMemberDto ToMemberDto(User user, TeamMembership membership, Guid communityId)
    {
        var roleName = user.CommunityMemberships
            .FirstOrDefault(item => item.IsActive && item.CommunityId == communityId)
            ?.CommunityRole?.Name ?? string.Empty;
        return new TeamMemberDto(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email,
            roleName,
            membership.IsLead,
            membership.CreatedAt);
    }

    private static bool TryResolveScope(
        UserDto currentUser,
        Guid? requestedCommunityId,
        string permission,
        out Guid? communityId)
    {
        if (currentUser.IsSuperAdmin())
        {
            communityId = requestedCommunityId;
            return true;
        }
        if (!currentUser.HasPermission(permission) || currentUser.CommunityId is null)
        {
            communityId = null;
            return false;
        }
        if (requestedCommunityId is not null && requestedCommunityId != currentUser.CommunityId)
        {
            communityId = null;
            return false;
        }
        communityId = currentUser.CommunityId;
        return true;
    }

    private static bool CanAccessCommunity(UserDto currentUser, Guid communityId, string permission) =>
        currentUser.IsSuperAdmin()
        || (currentUser.CommunityId == communityId && currentUser.HasPermission(permission));

    private static List<string> ValidateTeam(string? name, string? description)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) errors.Add("Team name is required.");
        else if (name.Trim().Length > 80) errors.Add("Team name cannot exceed 80 characters.");
        if ((description?.Trim().Length ?? 0) > 400) errors.Add("Team description cannot exceed 400 characters.");
        return errors;
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}
