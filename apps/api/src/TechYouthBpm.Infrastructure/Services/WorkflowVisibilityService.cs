using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Infrastructure.Services;

public class WorkflowVisibilityService : IWorkflowVisibilityService
{
    public Result<WorkflowVisibilityScope> ResolveScope(string? requestedScope, UserDto user)
    {
        var normalized = requestedScope?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized is "personal" or "startedbyme" or "assignedtome")
        {
            return Result<WorkflowVisibilityScope>.Success(WorkflowVisibilityScope.Personal);
        }

        if (normalized == "visible")
        {
            return Result<WorkflowVisibilityScope>.Success(user.IsSuperAdmin()
                ? WorkflowVisibilityScope.Global
                : user.HasPermission(PermissionNames.ProcessesViewAll)
                    ? WorkflowVisibilityScope.Community
                    : WorkflowVisibilityScope.Personal);
        }

        if (normalized == "community")
        {
            return user.CommunityId is not null
                && user.HasPermission(PermissionNames.ProcessesViewAll)
                    ? Result<WorkflowVisibilityScope>.Success(WorkflowVisibilityScope.Community)
                    : Result<WorkflowVisibilityScope>.Failure("Current user cannot access the community workflow scope.");
        }

        if (normalized == "global")
        {
            return user.IsSuperAdmin()
                ? Result<WorkflowVisibilityScope>.Success(WorkflowVisibilityScope.Global)
                : Result<WorkflowVisibilityScope>.Failure("Current user cannot access the global workflow scope.");
        }

        return Result<WorkflowVisibilityScope>.Failure("Workflow scope is invalid.");
    }

    public IQueryable<ProcessInstance> ApplyProcessScope(
        IQueryable<ProcessInstance> query,
        UserDto user,
        WorkflowVisibilityScope scope)
    {
        if (scope == WorkflowVisibilityScope.Global)
        {
            return query;
        }

        if (user.CommunityId is not { } communityId)
        {
            return scope == WorkflowVisibilityScope.Personal
                ? query.Where(process => process.StartedByUserId == user.Id
                    || process.Tasks.Any(task =>
                        task.AssignedUserId == user.Id
                        || task.ClaimedByUserId == user.Id
                        || task.CompletedByUserId == user.Id))
                : query.Where(_ => false);
        }

        var communityQuery = query.Where(process => process.CommunityId == communityId);
        if (scope == WorkflowVisibilityScope.Community)
        {
            return communityQuery;
        }

        var teamIds = (user.Teams ?? []).Select(team => team.Id).ToArray();
        var roleId = user.CommunityRoleId;
        var canViewTasks = user.HasPermission(PermissionNames.TasksView);
        var canAct = user.HasPermission(PermissionNames.TasksAct);
        return communityQuery.Where(process =>
            process.StartedByUserId == user.Id
            || process.Tasks.Any(task =>
                task.AssignedUserId == user.Id
                || task.ClaimedByUserId == user.Id
                || task.CompletedByUserId == user.Id
                || (task.Status == ProcessTaskStatus.Open
                    && task.ClaimedByUserId == null
                    && task.AssignmentType == null
                    && canViewTasks
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
                    && task.CandidateCommunityRoleId == roleId)));
    }

    public IQueryable<ProcessTask> ApplyTaskScope(
        IQueryable<ProcessTask> query,
        UserDto user,
        WorkflowVisibilityScope scope)
    {
        if (scope == WorkflowVisibilityScope.Global)
        {
            return query;
        }

        if (user.CommunityId is not { } communityId)
        {
            return scope == WorkflowVisibilityScope.Personal
                ? query.Where(task => task.AssignedUserId == user.Id
                    || task.ClaimedByUserId == user.Id
                    || task.CompletedByUserId == user.Id)
                : query.Where(_ => false);
        }

        var communityQuery = query.Where(task => task.ProcessInstance != null
            && task.ProcessInstance.CommunityId == communityId);
        if (scope == WorkflowVisibilityScope.Community)
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

    public bool CanViewProcess(ProcessInstance process, UserDto user)
    {
        if (user.IsSuperAdmin())
        {
            return true;
        }

        if (process.CommunityId != user.CommunityId
            || (!user.HasPermission(PermissionNames.ProcessesView)
                && !user.HasPermission(PermissionNames.ProcessesViewAll)))
        {
            return false;
        }

        if (user.HasPermission(PermissionNames.ProcessesViewAll) || process.StartedByUserId == user.Id)
        {
            return true;
        }

        var teamIds = (user.Teams ?? []).Select(team => team.Id).ToHashSet();
        var canViewTasks = user.HasPermission(PermissionNames.TasksView);
        var canAct = user.HasPermission(PermissionNames.TasksAct);
        return process.Tasks.Any(task =>
            task.AssignedUserId == user.Id
            || task.ClaimedByUserId == user.Id
            || task.CompletedByUserId == user.Id
            || (task.Status == ProcessTaskStatus.Open
                && task.ClaimedByUserId == null
                && task.AssignmentType is null
                && canViewTasks
                && canAct)
            || (task.Status == ProcessTaskStatus.Open
                && task.ClaimedByUserId == null
                && canAct
                && task.AssignmentType == TaskAssignmentType.Team
                && task.CandidateTeamId is { } teamId
                && teamIds.Contains(teamId))
            || (task.Status == ProcessTaskStatus.Open
                && task.ClaimedByUserId == null
                && canAct
                && task.AssignmentType == TaskAssignmentType.CommunityRole
                && task.CandidateCommunityRoleId == user.CommunityRoleId)
            || (task.Status == ProcessTaskStatus.Open
                && task.ClaimedByUserId == null
                && canAct
                && task.AssignmentType == TaskAssignmentType.TeamAndCommunityRole
                && task.CandidateTeamId is { } intersectionTeamId
                && teamIds.Contains(intersectionTeamId)
                && task.CandidateCommunityRoleId == user.CommunityRoleId));
    }
}
