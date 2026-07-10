using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class ProcessService(
    AppDbContext db,
    IFormService formService,
    ProcessStateMachine stateMachine,
    ISystemAuditService auditService) : IProcessService
{
    public async Task<IReadOnlyList<ProcessSummaryDto>> ListAsync(UserDto user, CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.ProcessesView))
        {
            return [];
        }

        var query = db.ProcessInstances.AsNoTracking();

        if (!user.IsSuperAdmin() && user.CommunityId is not null)
        {
            query = query.Where(process => process.CommunityId == user.CommunityId);
        }

        if (!user.HasPermission(PermissionNames.TasksView))
        {
            query = query.Where(process => process.StartedByUserId == user.Id);
        }

        return await query
            .OrderByDescending(process => process.StartedAt)
            .Select(process => new ProcessSummaryDto(
                process.Id,
                process.FormDefinitionId,
                process.FormDefinition != null ? process.FormDefinition.Name : "Unknown form",
                process.CommunityId,
                process.Community != null ? process.Community.Name : string.Empty,
                process.Status,
                process.StartedAt,
                process.CompletedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcessDetailDto?> GetAsync(Guid id, UserDto user, CancellationToken cancellationToken = default)
    {
        var process = await ProcessQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (process is null || !CanSeeProcess(process, user))
        {
            return null;
        }

        return process.ToDetailDto();
    }

    public async Task<Result<ProcessDetailDto>> StartAsync(StartProcessRequest request, UserDto user, CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.ProcessesStart))
        {
            return Result<ProcessDetailDto>.Failure("Current user cannot start processes.");
        }

        var form = await formService.GetAsync(request.FormDefinitionId, user, cancellationToken);
        if (form is null)
        {
            return Result<ProcessDetailDto>.Failure("Form definition was not found.");
        }

        var isCommunityActive = await db.Communities.AnyAsync(
            community => community.Id == form.CommunityId && community.IsActive,
            cancellationToken);
        if (!isCommunityActive)
        {
            return Result<ProcessDetailDto>.Failure("The process community is not active.");
        }

        var validationErrors = FormDataValidator.Validate(form, request.FormData);
        if (validationErrors.Count > 0)
        {
            return Result<ProcessDetailDto>.Failure(validationErrors);
        }

        var now = DateTime.UtcNow;
        var startResult = stateMachine.Move(ProcessStatus.Pending, WorkflowAction.Start);
        if (!startResult.IsSuccess)
        {
            return Result<ProcessDetailDto>.Failure(startResult.Errors);
        }

        var process = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = request.FormDefinitionId,
            CommunityId = form.CommunityId,
            StartedByUserId = user.Id,
            Status = startResult.Value,
            FormDataJson = request.FormData.GetRawText(),
            StartedAt = now,
            Tasks =
            [
                new ProcessTask
                {
                    Id = Guid.NewGuid(),
                    AssignedRole = Role.User,
                    RequiredPermission = PermissionNames.TasksAct,
                    Status = ProcessTaskStatus.Open,
                    AvailableActionsJson = JsonHelpers.Serialize(new[] { WorkflowAction.Approve, WorkflowAction.Reject }),
                    CreatedAt = now
                }
            ],
            AuditLogs =
            [
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Action = WorkflowAction.Start,
                    FromStatus = ProcessStatus.Pending,
                    ToStatus = startResult.Value,
                    CreatedAt = now,
                    Note = "Process started from submitted form data."
                }
            ]
        };

        db.ProcessInstances.Add(process);
        await db.SaveChangesAsync(cancellationToken);
        await NotifyTaskCandidatesAsync(
            process.CommunityId,
            process.Id,
            form.Name,
            cancellationToken);
        await auditService.LogAsync(
            user,
            "Process.Started",
            "ProcessInstance",
            process.Id.ToString(),
            $"Process was started from form '{form.Name}'.",
            cancellationToken);

        var saved = await GetAsync(process.Id, user, cancellationToken);
        return Result<ProcessDetailDto>.Success(saved!);
    }

    private IQueryable<ProcessInstance> ProcessQuery() =>
        db.ProcessInstances
            .AsNoTracking()
            .AsSplitQuery()
            .Include(process => process.FormDefinition)
            .Include(process => process.Community)
            .Include(process => process.Tasks)
            .ThenInclude(task => task.AssignedCommunityRole)
            .Include(process => process.AuditLogs)
            .ThenInclude(log => log.User);

    private static bool CanSeeProcess(ProcessInstance process, UserDto user) =>
        user.IsSuperAdmin()
        || (process.CommunityId == user.CommunityId
            && (user.HasPermission(PermissionNames.ProcessesView)
                || process.StartedByUserId == user.Id));

    private async Task NotifyTaskCandidatesAsync(Guid communityId, Guid processId, string formName, CancellationToken cancellationToken)
    {
        var userIds = await db.Users
            .Where(user => user.Status == UserStatus.Active
                && user.CommunityMemberships.Any(membership =>
                    membership.IsActive
                    && membership.CommunityId == communityId
                    && membership.CommunityRole != null
                    && membership.CommunityRole.Permissions.Any(permission => permission.Permission == PermissionNames.TasksAct)))
            .Select(user => user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = "Task.Assigned",
                Title = "Yeni onay gorevi",
                Message = $"{formName} sureci icin onay bekleyen yeni bir task var.",
                EntityType = "ProcessInstance",
                EntityId = processId.ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }

        if (userIds.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
