using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Infrastructure.Services;

public class WorkflowVisibilityService(TaskAccessPolicy taskAccessPolicy) : IWorkflowVisibilityService
{
    public WorkflowVisibilityService() : this(new TaskAccessPolicy())
    {
    }

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
                ? taskAccessPolicy.ApplyPersonalProcessScope(query, user)
                : query.Where(_ => false);
        }

        var communityQuery = query.Where(process => process.CommunityId == communityId);
        if (scope == WorkflowVisibilityScope.Community)
        {
            return communityQuery;
        }

        return taskAccessPolicy.ApplyPersonalProcessScope(communityQuery, user);
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

        return taskAccessPolicy.ApplyPersonalTaskScope(communityQuery, user);
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

        if (user.HasPermission(PermissionNames.ProcessesViewAll)
            || user.HasPermission(PermissionNames.TasksManageAll)
            || process.StartedByUserId == user.Id)
        {
            return true;
        }

        return process.Tasks.Any(task => taskAccessPolicy.CanSee(task, user));
    }
}
