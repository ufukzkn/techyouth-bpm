using System.Text.Json.Nodes;
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
    ISystemAuditService auditService,
    IWorkflowVisibilityService workflowVisibilityService) : IProcessService
{
    public ProcessService(
        AppDbContext db,
        IFormService formService,
        ProcessStateMachine stateMachine,
        ISystemAuditService auditService)
        : this(db, formService, stateMachine, auditService, new WorkflowVisibilityService())
    {
    }

    public async Task<IReadOnlyList<ProcessSummaryDto>> ListAsync(
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        var result = await ListAsync(new ProcessListRequest(PageSize: 50), user, cancellationToken);
        return result.Items;
    }

    public async Task<PagedResult<ProcessSummaryDto>> ListAsync(
        ProcessListRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.ProcessesView)
            && !user.HasPermission(PermissionNames.ProcessesViewAll))
        {
            return new PagedResult<ProcessSummaryDto>([], 1, NormalizePageSize(request.PageSize), 0);
        }

        var page = Math.Max(1, request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var resolvedScope = workflowVisibilityService.ResolveScope(request.Scope, user);
        if (!resolvedScope.IsSuccess)
        {
            return new PagedResult<ProcessSummaryDto>([], page, pageSize, 0);
        }

        var query = workflowVisibilityService.ApplyProcessScope(
            db.ProcessInstances.AsNoTracking(),
            user,
            resolvedScope.Value);

        if (request.Status is { } status)
        {
            query = query.Where(process => process.Status == status);
        }

        if (string.Equals(request.Scope, "startedByMe", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(process => process.StartedByUserId == user.Id);
        }
        else if (string.Equals(request.Scope, "assignedToMe", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(process => process.Tasks.Any(task =>
                task.AssignedUserId == user.Id || task.ClaimedByUserId == user.Id));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var ordered = ApplyProcessOrdering(query, request.SortBy, request.SortDirection);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(process => new ProcessSummaryDto(
                process.Id,
                process.FormDefinitionId,
                process.FormDefinition != null ? process.FormDefinition.Name : "Unknown form",
                process.CommunityId,
                process.Community != null ? process.Community.Name : string.Empty,
                process.Status,
                process.StartedAt,
                process.CompletedAt,
                process.ProcessDefinitionVersionId,
                process.FormDefinitionVersionId,
                process.CurrentNodeKey,
                process.ProcessDefinitionVersion != null && process.ProcessDefinitionVersion.ProcessDefinition != null
                    ? process.ProcessDefinitionVersion.ProcessDefinition.Name
                    : string.Empty,
                process.Tasks
                    .Where(task => (task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed)
                        && task.DueAt.HasValue)
                    .Min(task => task.DueAt),
                process.Tasks
                    .Where(task => task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed)
                    .Max(task => (TaskPriority?)task.Priority),
                process.Tasks
                    .Where(task => task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed)
                    .OrderByDescending(task => task.CreatedAt)
                    .Select(task => new ProcessCurrentStepSummaryDto(
                        task.NodeKey,
                        task.Title,
                        task.AssignmentType,
                        task.CandidateTeam != null ? task.CandidateTeam.Name : string.Empty,
                        task.CandidateCommunityRole != null ? task.CandidateCommunityRole.Name : string.Empty,
                        task.AssignedUser != null ? task.AssignedUser.DisplayName : string.Empty,
                        task.ClaimedByUser != null ? task.ClaimedByUser.DisplayName : string.Empty,
                        task.RequiresTeamLead,
                        task.CreatedAt,
                        task.DueAt))
                    .FirstOrDefault(),
                process.StepExecutions
                    .Where(step => step.Status == ProcessStepStatus.Completed && step.CompletedAt.HasValue)
                    .OrderByDescending(step => step.CompletedAt)
                    .Select(step => new ProcessCompletedStepSummaryDto(
                        step.NodeKey,
                        step.NodeTitle,
                        step.CompletedByUser != null ? step.CompletedByUser.DisplayName : string.Empty,
                        step.Action,
                        step.CompletedAt!.Value))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProcessSummaryDto>(items, page, pageSize, totalCount);
    }

    private static IOrderedQueryable<ProcessInstance> ApplyProcessOrdering(
        IQueryable<ProcessInstance> query,
        string? sortBy,
        string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var normalizedSort = sortBy?.Trim().ToLowerInvariant();
        return normalizedSort switch
        {
            "dueat" or "nearestdeadline" => descending
                ? query.OrderByDescending(process => process.Tasks
                    .Where(task => (task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed) && task.DueAt.HasValue)
                    .Min(task => task.DueAt)).ThenByDescending(process => process.StartedAt)
                : query.OrderBy(process => !process.Tasks.Any(task =>
                        (task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed) && task.DueAt.HasValue))
                    .ThenBy(process => process.Tasks
                        .Where(task => (task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed) && task.DueAt.HasValue)
                        .Min(task => task.DueAt))
                    .ThenByDescending(process => process.StartedAt),
            "priority" => descending
                ? query.OrderByDescending(process => process.Tasks
                    .Where(task => task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed)
                    .Max(task => (TaskPriority?)task.Priority)).ThenByDescending(process => process.StartedAt)
                : query.OrderBy(process => process.Tasks
                    .Where(task => task.Status == ProcessTaskStatus.Open || task.Status == ProcessTaskStatus.Claimed)
                    .Max(task => (TaskPriority?)task.Priority)).ThenByDescending(process => process.StartedAt),
            "status" => descending
                ? query.OrderByDescending(process => process.Status).ThenByDescending(process => process.StartedAt)
                : query.OrderBy(process => process.Status).ThenByDescending(process => process.StartedAt),
            _ => descending
                ? query.OrderByDescending(process => process.StartedAt)
                : query.OrderBy(process => process.StartedAt)
        };
    }

    private static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize, 1, 50);

    public async Task<ProcessDetailDto?> GetAsync(
        Guid id,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        var process = await ProcessQuery().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (process is null || !workflowVisibilityService.CanViewProcess(process, user))
        {
            return null;
        }

        return process.ToDetailDto(user);
    }

    public async Task<Result<ProcessDetailDto>> StartAsync(
        StartProcessRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
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

        if (!await db.Communities.AnyAsync(
                community => community.Id == form.CommunityId && community.IsActive,
                cancellationToken))
        {
            return Result<ProcessDetailDto>.Failure("The process community is not active.");
        }

        var validationErrors = FormDataValidator.Validate(form, request.FormData);
        if (validationErrors.Count > 0)
        {
            return Result<ProcessDetailDto>.Failure(validationErrors);
        }

        var startResult = stateMachine.Move(ProcessStatus.Pending, WorkflowAction.Start);
        if (!startResult.IsSuccess)
        {
            return Result<ProcessDetailDto>.Failure(startResult.Errors);
        }

        var formEntity = await db.FormDefinitions
            .AsSplitQuery()
            .Include(item => item.Fields)
            .ThenInclude(field => field.ValidationRules)
            .Include(item => item.Versions)
            .SingleAsync(item => item.Id == form.Id, cancellationToken);
        var publishedFormVersion = formEntity.Versions
            .Where(version => version.Status == DefinitionVersionStatus.Published)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (publishedFormVersion is null)
        {
            publishedFormVersion = FormVersionModel.BuildLegacyPublishedVersion(formEntity, 1, user.Id, now);
            db.FormDefinitionVersions.Add(publishedFormVersion);
        }

        var process = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = request.FormDefinitionId,
            FormDefinitionVersionId = publishedFormVersion.Id,
            CommunityId = form.CommunityId,
            StartedByUserId = user.Id,
            Status = startResult.Value,
            FormDataJson = request.FormData.GetRawText(),
            VariablesJson = BuildVariablesJson(request.FormData.GetRawText()),
            StartedAt = now,
            Tasks =
            [
                new ProcessTask
                {
                    Id = Guid.NewGuid(),
                    AssignedRole = Role.User,
                    RequiredPermission = PermissionNames.TasksAct,
                    Title = "Approval",
                    Priority = TaskPriority.Normal,
                    Status = ProcessTaskStatus.Open,
                    AvailableActionsJson = JsonHelpers.Serialize(new[] { WorkflowAction.Approve, WorkflowAction.Reject }),
                    ClaimVersion = Guid.NewGuid(),
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

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ProcessInstances.Add(process);
        await db.SaveChangesAsync(cancellationToken);
        await NotifyLegacyTaskCandidatesAsync(process.CommunityId, process.Id, form.Name, cancellationToken);
        await auditService.LogAsync(
            user,
            "Process.Started",
            "ProcessInstance",
            process.Id.ToString(),
            $"Process was started from form '{form.Name}'.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ProcessDetailDto>.Success((await GetAsync(process.Id, user, cancellationToken))!);
    }

    public async Task<Result<ProcessDetailDto>> StartVersionAsync(
        StartProcessVersionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.ProcessesStart))
        {
            return Result<ProcessDetailDto>.Failure("Current user cannot start processes.");
        }

        var version = await db.ProcessDefinitionVersions
            .Include(item => item.ProcessDefinition)
            .ThenInclude(definition => definition!.Community)
            .Include(item => item.FormDefinitionVersion)
            .ThenInclude(formVersion => formVersion!.FormDefinition)
            .Include(item => item.FormDefinitionVersion)
            .ThenInclude(formVersion => formVersion!.Pages)
            .ThenInclude(page => page.Fields)
            .ThenInclude(field => field.ValidationRules)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessDefinitionVersionId, cancellationToken);

        if (version is null || version.Status != DefinitionVersionStatus.Published)
        {
            return Result<ProcessDetailDto>.Failure("Published process definition version was not found.");
        }

        var definition = version.ProcessDefinition!;
        if ((!user.IsSuperAdmin() && definition.CommunityId != user.CommunityId)
            || definition.Community?.IsActive != true)
        {
            return Result<ProcessDetailDto>.Failure("The process definition is outside the current active community.");
        }

        var formVersion = version.FormDefinitionVersion!;
        var formDto = formVersion.ToDto();
        var validationErrors = FormDataValidator.Validate(formDto, request.FormData);
        if (validationErrors.Count > 0)
        {
            return Result<ProcessDetailDto>.Failure(validationErrors);
        }

        var graph = JsonHelpers.Deserialize(version.GraphJson, new ProcessGraphDto("", [], []));
        var now = DateTime.UtcNow;
        var process = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = formVersion.FormDefinitionId,
            FormDefinitionVersionId = formVersion.Id,
            ProcessDefinitionVersionId = version.Id,
            CommunityId = definition.CommunityId,
            StartedByUserId = user.Id,
            Status = ProcessStatus.InProgress,
            FormDataJson = request.FormData.GetRawText(),
            VariablesJson = BuildVariablesJson(request.FormData.GetRawText()),
            StartedAt = now,
            AuditLogs =
            [
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Action = WorkflowAction.Start,
                    FromStatus = ProcessStatus.Pending,
                    ToStatus = ProcessStatus.InProgress,
                    CreatedAt = now,
                    Note = $"Process started with definition version {version.VersionNumber}."
                }
            ]
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ProcessInstances.Add(process);
        var routeResult = await new DynamicWorkflowEngine(db).StartAsync(process, graph, cancellationToken);
        if (!routeResult.IsSuccess)
        {
            return Result<ProcessDetailDto>.Failure(routeResult.Errors);
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            user,
            "Process.Started",
            "ProcessInstance",
            process.Id.ToString(),
            $"Process '{definition.Name}' version {version.VersionNumber} was started.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ProcessDetailDto>.Success((await GetAsync(process.Id, user, cancellationToken))!);
    }

    private IQueryable<ProcessInstance> ProcessQuery() =>
        db.ProcessInstances
            .AsNoTracking()
            .AsSplitQuery()
            .Include(process => process.FormDefinition)
            .Include(process => process.Community)
            .Include(process => process.ProcessDefinitionVersion)
            .ThenInclude(version => version!.ProcessDefinition)
            .Include(process => process.Tasks)
            .ThenInclude(task => task.AssignedCommunityRole)
            .Include(process => process.Tasks)
            .ThenInclude(task => task.FormDefinitionVersion)
            .ThenInclude(version => version!.FormDefinition)
            .Include(process => process.Tasks)
            .ThenInclude(task => task.FormDefinitionVersion)
            .ThenInclude(version => version!.Pages)
            .ThenInclude(page => page.Fields)
            .ThenInclude(field => field.ValidationRules)
            .Include(process => process.StepExecutions)
            .ThenInclude(step => step.CompletedByUser)
            .Include(process => process.AuditLogs)
            .ThenInclude(log => log.User);

    private async Task NotifyLegacyTaskCandidatesAsync(
        Guid communityId,
        Guid processId,
        string formName,
        CancellationToken cancellationToken)
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
                CommunityId = communityId,
                UserId = userId,
                Type = "Task.Assigned",
                Title = "Yeni onay görevi",
                Message = $"{formName} süreci için onay bekleyen yeni bir iş var.",
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

    private static string BuildVariablesJson(string formDataJson)
    {
        var variables = new JsonObject
        {
            ["start"] = JsonNode.Parse(formDataJson),
            ["steps"] = new JsonObject()
        };
        return variables.ToJsonString();
    }
}
