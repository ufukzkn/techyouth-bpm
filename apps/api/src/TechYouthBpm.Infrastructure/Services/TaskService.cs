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

public class TaskService(AppDbContext db, ProcessStateMachine stateMachine) : ITaskService
{
    public async Task<IReadOnlyList<ProcessTaskDto>> ListMyTasksAsync(UserDto user, CancellationToken cancellationToken = default)
    {
        var tasks = await db.ProcessTasks
            .Where(task => task.Status == ProcessTaskStatus.Open && (task.AssignedRole == user.Role || user.Role == Role.Admin))
            .OrderByDescending(task => task.CreatedAt)
            .ToListAsync(cancellationToken);

        return tasks.Select(task => task.ToDto()).ToArray();
    }

    public async Task<Result<ProcessDetailDto>> ExecuteActionAsync(Guid taskId, TaskActionRequest request, UserDto user, CancellationToken cancellationToken = default)
    {
        var task = await db.ProcessTasks
            .Include(item => item.ProcessInstance)
            .ThenInclude(process => process!.FormDefinition)
            .Include(item => item.ProcessInstance)
            .ThenInclude(process => process!.Tasks)
            .Include(item => item.ProcessInstance)
            .ThenInclude(process => process!.AuditLogs)
            .ThenInclude(log => log.User)
            .SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);

        if (task is null || task.ProcessInstance is null)
        {
            return Result<ProcessDetailDto>.Failure("Task was not found.");
        }

        if (task.Status != ProcessTaskStatus.Open)
        {
            return Result<ProcessDetailDto>.Failure("Task is already closed.");
        }

        if (user.Role != Role.Admin && task.AssignedRole != user.Role)
        {
            return Result<ProcessDetailDto>.Failure("Current user cannot execute this task.");
        }

        var availableActions = JsonHelpers.Deserialize<IReadOnlyList<WorkflowAction>>(task.AvailableActionsJson, []);
        if (!availableActions.Contains(request.Action))
        {
            return Result<ProcessDetailDto>.Failure($"Action {request.Action} is not available for this task.");
        }

        var previousStatus = task.ProcessInstance.Status;
        var transition = stateMachine.Move(previousStatus, request.Action);
        if (!transition.IsSuccess)
        {
            return Result<ProcessDetailDto>.Failure(transition.Errors);
        }

        task.Status = ProcessTaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedByUserId = user.Id;

        task.ProcessInstance.Status = transition.Value;
        if (transition.Value is ProcessStatus.Completed or ProcessStatus.Rejected)
        {
            task.ProcessInstance.CompletedAt = DateTime.UtcNow;
        }

        task.ProcessInstance.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = request.Action,
            FromStatus = previousStatus,
            ToStatus = transition.Value,
            CreatedAt = DateTime.UtcNow,
            Note = request.Note ?? string.Empty
        });

        await db.SaveChangesAsync(cancellationToken);

        return Result<ProcessDetailDto>.Success(task.ProcessInstance.ToDetailDto());
    }
}
