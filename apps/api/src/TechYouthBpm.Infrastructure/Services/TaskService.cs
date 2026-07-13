using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class TaskService(AppDbContext db, ProcessStateMachine stateMachine, ISystemAuditService auditService) : ITaskService
{
    public TaskService(AppDbContext db, ProcessStateMachine stateMachine)
        : this(db, stateMachine, new SystemAuditService(db))
    {
    }

    public async Task<IReadOnlyList<ProcessTaskDto>> ListMyTasksAsync(UserDto user, CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.TasksView))
        {
            return [];
        }

        var tasks = await db.ProcessTasks
            .Include(task => task.AssignedCommunityRole)
            .Include(task => task.ProcessInstance)
            .Where(task =>
                task.Status == ProcessTaskStatus.Open
                && (user.IsSuperAdmin()
                    || (task.ProcessInstance != null
                        && task.ProcessInstance.CommunityId == user.CommunityId
                        && user.HasPermission(task.RequiredPermission))))
            .OrderByDescending(task => task.CreatedAt)
            .ToListAsync(cancellationToken);

        return tasks.Select(task => task.ToDto()).ToArray();
    }

    public async Task<Result<ProcessDetailDto>> ExecuteActionAsync(Guid taskId, TaskActionRequest request, UserDto user, CancellationToken cancellationToken = default)
    {
        var task = await db.ProcessTasks
            .Include(item => item.AssignedCommunityRole)
            .Include(item => item.ProcessInstance)
            .ThenInclude(process => process!.FormDefinition)
            .SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);

        if (task is null || task.ProcessInstance is null)
        {
            return Result<ProcessDetailDto>.Failure("Task was not found.");
        }

        if (task.Status != ProcessTaskStatus.Open)
        {
            return Result<ProcessDetailDto>.Failure("Task is already closed.");
        }

        var isCommunityActive = await db.Communities.AnyAsync(
            community => community.Id == task.ProcessInstance.CommunityId && community.IsActive,
            cancellationToken);
        if (!isCommunityActive)
        {
            return Result<ProcessDetailDto>.Failure("The task community is not active.");
        }

        if (!user.IsSuperAdmin()
            && (task.ProcessInstance.CommunityId != user.CommunityId || !user.HasPermission(task.RequiredPermission)))
        {
            return Result<ProcessDetailDto>.Failure("Current user cannot execute this task.");
        }

        var availableActions = JsonHelpers.Deserialize<IReadOnlyList<WorkflowAction>>(task.AvailableActionsJson, []);
        if (!availableActions.Contains(request.Action))
        {
            return Result<ProcessDetailDto>.Failure($"Action {request.Action} is not available for this task.");
        }

        var processId = task.ProcessInstance.Id;
        var previousStatus = task.ProcessInstance.Status;
        var transition = stateMachine.Move(previousStatus, request.Action);
        if (!transition.IsSuccess)
        {
            return Result<ProcessDetailDto>.Failure(transition.Errors);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        task.Status = ProcessTaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedByUserId = user.Id;

        task.ProcessInstance.Status = transition.Value;
        if (transition.Value is ProcessStatus.Completed or ProcessStatus.Rejected)
        {
            task.ProcessInstance.CompletedAt = DateTime.UtcNow;
        }

        if (transition.Value is ProcessStatus.Escalated)
        {
            var escalationTask = new ProcessTask
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = task.ProcessInstance.Id,
                AssignedRole = Role.Admin,
                AssignedCommunityRoleId = task.AssignedCommunityRoleId,
                RequiredPermission = task.RequiredPermission,
                Status = ProcessTaskStatus.Open,
                AvailableActionsJson = JsonHelpers.Serialize(new[] { WorkflowAction.Approve, WorkflowAction.Reject }),
                CreatedAt = DateTime.UtcNow
            };
            db.ProcessTasks.Add(escalationTask);
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = task.ProcessInstance.Id,
            UserId = user.Id,
            Action = request.Action,
            FromStatus = previousStatus,
            ToStatus = transition.Value,
            CreatedAt = DateTime.UtcNow,
            Note = request.Note ?? string.Empty
        });

        if (task.ProcessInstance.StartedByUserId != user.Id)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = task.ProcessInstance.StartedByUserId,
                Type = $"Process.{transition.Value}",
                Title = "Surec durumunuz guncellendi",
                Message = $"Baslattiginiz surec {transition.Value} durumuna gecti.",
                EntityType = "ProcessInstance",
                EntityId = processId.ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            $"Task.{request.Action}",
            "ProcessTask",
            task.Id.ToString(),
            $"Task action '{request.Action}' moved process '{processId}' from {previousStatus} to {transition.Value}.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var process = await db.ProcessInstances
            .Include(item => item.FormDefinition)
            .Include(item => item.Community)
            .Include(item => item.Tasks)
            .ThenInclude(task => task.AssignedCommunityRole)
            .Include(item => item.AuditLogs)
            .ThenInclude(log => log.User)
            .SingleAsync(item => item.Id == processId, cancellationToken);

        return Result<ProcessDetailDto>.Success(process.ToDetailDto());
    }
}
