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
        var query = ProcessQuery();

        if (user.Role == Role.User)
        {
            query = query.Where(process => process.StartedByUserId == user.Id);
        }

        var processes = await query
            .OrderByDescending(process => process.StartedAt)
            .ToListAsync(cancellationToken);

        return processes.Select(process => process.ToSummaryDto()).ToArray();
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
        if (user.Role is not (Role.Admin or Role.User))
        {
            return Result<ProcessDetailDto>.Failure("Only Admin and User roles can start a process.");
        }

        var form = await formService.GetAsync(request.FormDefinitionId, cancellationToken);
        if (form is null)
        {
            return Result<ProcessDetailDto>.Failure("Form definition was not found.");
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
            StartedByUserId = user.Id,
            Status = startResult.Value,
            FormDataJson = request.FormData.GetRawText(),
            StartedAt = now,
            Tasks =
            [
                new ProcessTask
                {
                    Id = Guid.NewGuid(),
                    AssignedRole = Role.Approver,
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
            .Include(process => process.FormDefinition)
            .Include(process => process.Tasks)
            .Include(process => process.AuditLogs)
            .ThenInclude(log => log.User);

    private static bool CanSeeProcess(ProcessInstance process, UserDto user) =>
        user.Role is Role.Admin or Role.Approver || process.StartedByUserId == user.Id;
}
