using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Infrastructure.Services;

public sealed class TaskAccessPolicy
{
    public const string TeamLeadRequiredError = "This action can only be performed by a team lead. Contact your team lead.";

    public static bool IsCandidatePool(TaskAssignmentType? assignmentType) =>
        assignmentType is TaskAssignmentType.Team
            or TaskAssignmentType.CommunityRole
            or TaskAssignmentType.TeamAndCommunityRole;

    public IQueryable<ProcessTask> ApplyPersonalTaskScope(IQueryable<ProcessTask> query, UserDto user)
    {
        if (user.IsSuperAdmin())
        {
            return query;
        }

        if (user.CommunityId is not { } communityId)
        {
            return query.Where(task =>
                task.AssignedUserId == user.Id
                || task.ClaimedByUserId == user.Id
                || task.CompletedByUserId == user.Id);
        }

        var communityQuery = query.Where(task => task.ProcessInstance != null
            && task.ProcessInstance.CommunityId == communityId);
        if (user.HasPermission(PermissionNames.TasksManageAll))
        {
            return communityQuery;
        }

        var teamIds = (user.Teams ?? []).Select(team => team.Id).ToArray();
        var roleId = user.CommunityRoleId;
        var canView = user.HasPermission(PermissionNames.TasksView);
        var canAct = user.HasPermission(PermissionNames.TasksAct);
        return communityQuery.Where(task =>
            task.AssignedUserId == user.Id
            || task.ClaimedByUserId == user.Id
            || task.CompletedByUserId == user.Id
            || (task.Status == ProcessTaskStatus.Open
                && task.ClaimedByUserId == null
                && task.AssignmentType == null
                && canView
                && canAct)
            || (task.Status == ProcessTaskStatus.Open
                && task.ClaimedByUserId == null
                && canAct
                && task.AssignmentType == TaskAssignmentType.Team
                && teamIds.Contains(task.CandidateTeamId ?? Guid.Empty))
            || (task.Status == ProcessTaskStatus.Open
                && task.ClaimedByUserId == null
                && canAct
                && task.AssignmentType == TaskAssignmentType.CommunityRole
                && task.CandidateCommunityRoleId == roleId)
            || (task.Status == ProcessTaskStatus.Open
                && task.ClaimedByUserId == null
                && canAct
                && task.AssignmentType == TaskAssignmentType.TeamAndCommunityRole
                && teamIds.Contains(task.CandidateTeamId ?? Guid.Empty)
                && task.CandidateCommunityRoleId == roleId));
    }

    public IQueryable<ProcessInstance> ApplyPersonalProcessScope(IQueryable<ProcessInstance> query, UserDto user)
    {
        if (user.IsSuperAdmin())
        {
            return query;
        }

        if (user.CommunityId is not { } communityId)
        {
            return query.Where(process => process.StartedByUserId == user.Id
                || process.Tasks.Any(task =>
                    task.AssignedUserId == user.Id
                    || task.ClaimedByUserId == user.Id
                    || task.CompletedByUserId == user.Id));
        }

        var communityQuery = query.Where(process => process.CommunityId == communityId);
        if (user.HasPermission(PermissionNames.TasksManageAll))
        {
            return communityQuery.Where(process => process.StartedByUserId == user.Id || process.Tasks.Any());
        }

        var visibleTaskIds = ApplyPersonalTaskScope(
            query.SelectMany(process => process.Tasks),
            user).Select(task => task.Id);
        return communityQuery.Where(process =>
            process.StartedByUserId == user.Id
            || process.Tasks.Any(task => visibleTaskIds.Contains(task.Id)));
    }

    public TaskAccessDecision Evaluate(ProcessTask task, UserDto? user)
    {
        if (user is null)
        {
            return new TaskAccessDecision(true, false, null, null);
        }

        if (task.Status is ProcessTaskStatus.Completed or ProcessTaskStatus.Cancelled)
        {
            return Denied(TaskActionDenialReasonCodes.TaskClosed);
        }

        var communityId = task.ProcessInstance?.CommunityId;
        if (!user.IsSuperAdmin() && (communityId is null || user.CommunityId != communityId))
        {
            return Denied(TaskActionDenialReasonCodes.CommunityMismatch);
        }

        if (task.ClaimedByUserId is { } claimantId && claimantId != user.Id && !user.IsSuperAdmin())
        {
            return Denied(TaskActionDenialReasonCodes.ClaimedByAnotherUser);
        }

        var managesCommunityTasks = !user.IsSuperAdmin()
            && communityId == user.CommunityId
            && user.HasPermission(PermissionNames.TasksManageAll);
        if (!user.IsSuperAdmin()
            && !managesCommunityTasks
            && !user.HasPermission(task.RequiredPermission))
        {
            return Denied(TaskActionDenialReasonCodes.PermissionRequired);
        }

        if (!IsCandidatePool(task.AssignmentType))
        {
            var directlyAssigned = task.AssignmentType is null
                || user.IsSuperAdmin()
                || managesCommunityTasks
                || task.AssignedUserId == user.Id;
            return directlyAssigned
                ? new TaskAccessDecision(true, false, null, null)
                : Denied(TaskActionDenialReasonCodes.AssignedToAnotherUser);
        }

        if (!user.IsSuperAdmin() && !managesCommunityTasks)
        {
            var teams = user.Teams ?? [];
            if (task.AssignmentType is TaskAssignmentType.Team or TaskAssignmentType.TeamAndCommunityRole
                && (task.CandidateTeamId is not { } teamId || teams.All(team => team.Id != teamId)))
            {
                return Denied(TaskActionDenialReasonCodes.TeamMembershipRequired);
            }

            if (task.AssignmentType is TaskAssignmentType.CommunityRole or TaskAssignmentType.TeamAndCommunityRole
                && task.CandidateCommunityRoleId != user.CommunityRoleId)
            {
                return Denied(TaskActionDenialReasonCodes.CommunityRoleRequired);
            }

            if (task.RequiresTeamLead
                && (task.CandidateTeamId is not { } leadTeamId
                    || teams.All(team => team.Id != leadTeamId || !team.IsLead)))
            {
                return Denied(TaskActionDenialReasonCodes.TeamLeadRequired);
            }
        }

        if (task.ClaimedByUserId is { } currentClaimantId)
        {
            return currentClaimantId == user.Id || user.IsSuperAdmin()
                ? new TaskAccessDecision(true, false, null, null)
                : Denied(TaskActionDenialReasonCodes.ClaimedByAnotherUser);
        }

        return user.IsSuperAdmin()
            ? new TaskAccessDecision(true, true, null, null)
            : new TaskAccessDecision(false, true, null, null);
    }

    public bool CanSee(ProcessTask task, UserDto user)
    {
        if (user.IsSuperAdmin())
        {
            return true;
        }

        if (task.ProcessInstance?.CommunityId is not { } communityId || user.CommunityId != communityId)
        {
            return false;
        }

        if (user.HasPermission(PermissionNames.TasksManageAll)
            || task.AssignedUserId == user.Id
            || task.ClaimedByUserId == user.Id
            || task.CompletedByUserId == user.Id)
        {
            return true;
        }

        if (task.Status != ProcessTaskStatus.Open || task.ClaimedByUserId is not null)
        {
            return false;
        }

        if (!user.HasPermission(PermissionNames.TasksAct))
        {
            return false;
        }

        if (task.AssignmentType is null)
        {
            return user.HasPermission(PermissionNames.TasksView);
        }

        var teams = user.Teams ?? [];
        var teamMatches = task.AssignmentType is not (TaskAssignmentType.Team or TaskAssignmentType.TeamAndCommunityRole)
            || (task.CandidateTeamId is { } teamId && teams.Any(team => team.Id == teamId));
        var roleMatches = task.AssignmentType is not (TaskAssignmentType.CommunityRole or TaskAssignmentType.TeamAndCommunityRole)
            || task.CandidateCommunityRoleId == user.CommunityRoleId;
        return teamMatches && roleMatches;
    }

    public static string ErrorFor(TaskAccessDecision access) => access.ActionDenialReasonCode switch
    {
        TaskActionDenialReasonCodes.TeamLeadRequired => TeamLeadRequiredError,
        TaskActionDenialReasonCodes.ClaimedByAnotherUser => "Task is claimed by another user.",
        TaskActionDenialReasonCodes.CommunityMismatch => "Current user cannot execute this task because it belongs to another community.",
        TaskActionDenialReasonCodes.PermissionRequired => "Current user does not have the required task permission.",
        TaskActionDenialReasonCodes.TeamMembershipRequired => "Current user is not a member of the assigned team.",
        TaskActionDenialReasonCodes.CommunityRoleRequired => "Current user does not have the assigned community role.",
        TaskActionDenialReasonCodes.AssignedToAnotherUser => "Task is assigned to another user.",
        TaskActionDenialReasonCodes.TaskClosed => "Task is already closed.",
        _ => "Current user cannot execute this task."
    };

    private static TaskAccessDecision Denied(string reasonCode) => new(false, false, reasonCode, reasonCode);
}

public sealed record TaskAccessDecision(
    bool CanAct,
    bool CanClaim,
    string? ActionDenialReasonCode,
    string? ClaimDenialReasonCode);
