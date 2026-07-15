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
        assignmentType is TaskAssignmentType.Team
            or TaskAssignmentType.CommunityRole
            or TaskAssignmentType.TeamAndCommunityRole;

    public async Task<bool> CanSeeAsync(ProcessTask task, UserDto user, CancellationToken cancellationToken)
    {
        if (user.IsSuperAdmin())
        {
            return true;
        }

        if (task.AssignmentType is null)
        {
            return task.ProcessInstance?.CommunityId == user.CommunityId
                && user.HasPermission(task.RequiredPermission);
        }

        if (!IsCandidatePool(task.AssignmentType))
        {
            return task.AssignedUserId == user.Id
                && await IsActiveInCommunityAsync(user.Id, task.ProcessInstance!.CommunityId, cancellationToken);
        }

        return await IsEligibleCandidateAsync(task, user.Id, cancellationToken);
    }

    public async Task<bool> CanExecuteAsync(ProcessTask task, UserDto user, CancellationToken cancellationToken)
    {
        if (user.IsSuperAdmin())
        {
            return true;
        }

        if (task.AssignmentType is null)
        {
            return task.ProcessInstance?.CommunityId == user.CommunityId
                && user.HasPermission(task.RequiredPermission);
        }

        if (!IsCandidatePool(task.AssignmentType))
        {
            return task.AssignedUserId == user.Id
                && await IsActiveInCommunityAsync(user.Id, task.ProcessInstance!.CommunityId, cancellationToken);
        }

        return task.ClaimedByUserId == user.Id
            && await IsEligibleCandidateAsync(task, user.Id, cancellationToken);
    }

    public Task<bool> IsEligibleCandidateAsync(ProcessTask task, Guid userId, CancellationToken cancellationToken)
    {
        if (!IsCandidatePool(task.AssignmentType) || task.ProcessInstance is null)
        {
            return Task.FromResult(false);
        }

        return CandidateQuery(
                task.ProcessInstance.CommunityId,
                task.AssignmentType!.Value,
                task.CandidateTeamId,
                task.CandidateCommunityRoleId)
            .AnyAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ResolveCandidateUserIdsAsync(
        Guid communityId,
        TaskAssignmentDto assignment,
        CancellationToken cancellationToken)
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
                assignment.CommunityRoleId)
            .Select(user => user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private IQueryable<User> CandidateQuery(
        Guid communityId,
        TaskAssignmentType assignmentType,
        Guid? teamId,
        Guid? communityRoleId)
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
