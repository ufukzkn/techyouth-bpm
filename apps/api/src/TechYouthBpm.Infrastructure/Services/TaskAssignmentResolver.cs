using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

internal sealed class TaskAssignmentResolver(AppDbContext db)
{
    public static bool IsCandidatePool(TaskAssignmentType? assignmentType) =>
        TaskAccessPolicy.IsCandidatePool(assignmentType);

    public async Task<IReadOnlyList<Guid>> ResolveCandidateUserIdsAsync(
        Guid communityId,
        TaskAssignmentDto assignment,
        CancellationToken cancellationToken,
        bool requireTeamLead = false)
    {
        if (assignment.Type == TaskAssignmentType.ProcessStarter)
        {
            return [];
        }

        if (assignment.Type == TaskAssignmentType.SpecificUser)
        {
            return assignment.UserId is { } userId
                && await IsActiveInCommunityAsync(userId, communityId, cancellationToken)
                    ? [userId]
                    : [];
        }

        return await CandidateQuery(
                communityId,
                assignment.Type,
                assignment.TeamId,
                assignment.CommunityRoleId,
                requireTeamLead)
            .Select(user => user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private IQueryable<User> CandidateQuery(
        Guid communityId,
        TaskAssignmentType assignmentType,
        Guid? teamId,
        Guid? communityRoleId,
        bool requireTeamLead = false)
    {
        var query = db.Users.Where(user =>
            user.Status == UserStatus.Active
            && user.CommunityMemberships.Any(membership =>
                membership.IsActive
                && membership.CommunityId == communityId
                && membership.Community != null
                && membership.Community.IsActive
                && membership.CommunityRole != null
                && membership.CommunityRole.Permissions.Any(permission => permission.Permission == PermissionNames.TasksAct)));

        if (assignmentType is TaskAssignmentType.Team or TaskAssignmentType.TeamAndCommunityRole)
        {
            query = query.Where(user => user.TeamMemberships.Any(membership =>
                membership.IsActive
                && membership.TeamId == teamId
                && (!requireTeamLead || membership.IsLead)
                && membership.Team != null
                && membership.Team.IsActive
                && membership.Team.CommunityId == communityId));
        }

        if (assignmentType is TaskAssignmentType.CommunityRole or TaskAssignmentType.TeamAndCommunityRole)
        {
            query = query.Where(user => user.CommunityMemberships.Any(membership =>
                membership.IsActive
                && membership.CommunityId == communityId
                && membership.CommunityRoleId == communityRoleId));
        }

        return query;
    }

    private Task<bool> IsActiveInCommunityAsync(Guid userId, Guid communityId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user =>
            user.Id == userId
            && user.Status == UserStatus.Active
            && user.CommunityMemberships.Any(membership =>
                membership.IsActive
                && membership.CommunityId == communityId
                && membership.Community != null
                && membership.Community.IsActive),
            cancellationToken);
}
