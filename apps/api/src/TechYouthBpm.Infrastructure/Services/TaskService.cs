using System.Text.Json;
using System.Text.Json.Nodes;
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

public class TaskService(
    AppDbContext db,
    ProcessStateMachine stateMachine,
    ISystemAuditService auditService,
    IWorkflowVisibilityService workflowVisibilityService,
    TaskAccessPolicy taskAccessPolicy) : ITaskService
{
    public TaskService(AppDbContext db, ProcessStateMachine stateMachine)
        : this(db, stateMachine, new SystemAuditService(db), new WorkflowVisibilityService(), new TaskAccessPolicy())
    {
    }

    public TaskService(
        AppDbContext db,
        ProcessStateMachine stateMachine,
        ISystemAuditService auditService)
        : this(db, stateMachine, auditService, new WorkflowVisibilityService(), new TaskAccessPolicy())
    {
    }

    public async Task<IReadOnlyList<ProcessTaskDto>> ListMyTasksAsync(
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        var result = await ListMyTasksAsync(new TaskListRequest(PageSize: 50), user, cancellationToken);
        return result.Items;
    }

    public async Task<PagedResult<ProcessTaskDto>> ListMyTasksAsync(
        TaskListRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        if (!user.HasPermission(PermissionNames.TasksView)
            && !user.HasPermission(PermissionNames.TasksManageAll))
        {
            return new PagedResult<ProcessTaskDto>([], page, pageSize, 0);
        }

        var view = request.View?.Trim().ToLowerInvariant();
        var isHistory = view == "history";
        var baseQuery = db.ProcessTasks.AsNoTracking();
        var statusQuery = isHistory
            ? baseQuery.Where(task => task.Status == ProcessTaskStatus.Completed)
            : baseQuery.Where(task =>
                task.Status == ProcessTaskStatus.Open
                || task.Status == ProcessTaskStatus.Claimed);
        var query = isHistory
            ? statusQuery.Where(task => task.CompletedByUserId == user.Id)
            : workflowVisibilityService.ApplyTaskScope(
                statusQuery,
                user,
                WorkflowVisibilityScope.Personal);

        if (request.Priority is { } priority)
        {
            query = query.Where(task => task.Priority == priority);
        }

        if (request.TaskId is { } taskId)
        {
            query = query.Where(task => task.Id == taskId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var ordered = ApplyTaskOrdering(query, request.SortBy, request.SortDirection, isHistory);
        var tasks = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(task => new TaskListProjection
            {
                Id = task.Id,
                ProcessInstanceId = task.ProcessInstanceId,
                CommunityId = task.ProcessInstance!.CommunityId,
                AssignedCommunityRoleId = task.AssignedCommunityRoleId,
                AssignedCommunityRoleName = task.AssignedCommunityRole != null ? task.AssignedCommunityRole.Name : string.Empty,
                RequiredPermission = task.RequiredPermission,
                Status = task.Status,
                AvailableActionsJson = task.AvailableActionsJson,
                CreatedAt = task.CreatedAt,
                CompletedAt = task.CompletedAt,
                NodeKey = task.NodeKey,
                Attempt = task.Attempt,
                Title = task.Title,
                Priority = task.Priority,
                AssignmentType = task.AssignmentType,
                AssignedUserId = task.AssignedUserId,
                CandidateTeamId = task.CandidateTeamId,
                CandidateCommunityRoleId = task.CandidateCommunityRoleId,
                ClaimedByUserId = task.ClaimedByUserId,
                ClaimedAt = task.ClaimedAt,
                ClaimVersion = task.ClaimVersion,
                FormDefinitionVersionId = task.FormDefinitionVersionId,
                DueAt = task.DueAt,
                WorkflowName = task.ProcessInstance.ProcessDefinitionVersion != null
                    ? task.ProcessInstance.ProcessDefinitionVersion.ProcessDefinition!.Name
                    : string.Empty,
                FormName = task.FormDefinitionVersion != null
                    ? task.FormDefinitionVersion.FormDefinition!.Name
                    : task.ProcessInstance.FormDefinition!.Name,
                CommunityName = task.ProcessInstance.Community!.Name,
                RequiresTeamLead = task.RequiresTeamLead,
                AssignedUserDisplayName = task.AssignedUser != null ? task.AssignedUser.DisplayName : string.Empty,
                CandidateTeamName = task.CandidateTeam != null ? task.CandidateTeam.Name : string.Empty,
                CandidateCommunityRoleName = task.CandidateCommunityRole != null ? task.CandidateCommunityRole.Name : string.Empty,
                ClaimedByUserDisplayName = task.ClaimedByUser != null ? task.ClaimedByUser.DisplayName : string.Empty,
                CompletedByUserId = task.CompletedByUserId,
                CompletedByUserDisplayName = task.CompletedByUser != null ? task.CompletedByUser.DisplayName : string.Empty,
                CompletedAction = task.CompletedAction,
                CompletionNote = task.CompletionNote,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ProcessTaskDto>(
            tasks.Select(task => task.ToDto(user, taskAccessPolicy)).ToArray(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<ProcessTaskDto?> GetAsync(
        Guid taskId,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (!user.HasPermission(PermissionNames.TasksView)
            && !user.HasPermission(PermissionNames.TasksManageAll)
            && !user.IsSuperAdmin())
        {
            return null;
        }

        var task = await TaskQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        return task is not null && taskAccessPolicy.CanSee(task, user)
            ? task.ToDto(user)
            : null;
    }

    private static IOrderedQueryable<ProcessTask> ApplyTaskOrdering(
        IQueryable<ProcessTask> query,
        string? sortBy,
        string? sortDirection,
        bool history = false)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        if (history && string.Equals(sortBy, "dueAt", StringComparison.OrdinalIgnoreCase))
        {
            return query
                .OrderByDescending(task => task.CompletedAt)
                .ThenByDescending(task => task.CreatedAt);
        }

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "priority" => descending
                ? query.OrderByDescending(task => task.Priority).ThenBy(task => task.DueAt == null).ThenBy(task => task.DueAt)
                : query.OrderBy(task => task.Priority).ThenBy(task => task.DueAt == null).ThenBy(task => task.DueAt),
            "createdat" or "newest" => descending
                ? query.OrderByDescending(task => task.CreatedAt)
                : query.OrderBy(task => task.CreatedAt),
            "oldest" => query.OrderBy(task => task.CreatedAt),
            _ => descending
                ? query.OrderByDescending(task => task.DueAt).ThenByDescending(task => task.Priority).ThenByDescending(task => task.CreatedAt)
                : query.OrderBy(task => task.DueAt == null).ThenBy(task => task.DueAt).ThenByDescending(task => task.Priority).ThenBy(task => task.CreatedAt)
        };
    }

    public async Task<Result<ProcessTaskDto>> ClaimAsync(
        Guid taskId,
        ClaimTaskRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        var task = await TaskQuery().SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null)
        {
            return Result<ProcessTaskDto>.Failure("Task was not found.");
        }

        if (!TaskAccessPolicy.IsCandidatePool(task.AssignmentType))
        {
            return Result<ProcessTaskDto>.Failure("Directly assigned tasks do not require a claim.");
        }

        if (task.Status != ProcessTaskStatus.Open || task.ClaimedByUserId is not null)
        {
            return Result<ProcessTaskDto>.Failure("Task is already claimed or closed.");
        }

        if (request.ClaimVersion is { } expectedVersion && expectedVersion != task.ClaimVersion)
        {
            return Result<ProcessTaskDto>.Failure("Task claim changed. Refresh and try again.");
        }

        var claimAccess = taskAccessPolicy.Evaluate(task, user);
        if (!claimAccess.CanClaim)
        {
            return Result<ProcessTaskDto>.Failure(TaskAccessPolicy.ErrorFor(claimAccess));
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            task.Status = ProcessTaskStatus.Claimed;
            task.ClaimedByUserId = user.Id;
            task.ClaimedAt = DateTime.UtcNow;
            task.ClaimVersion = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                user,
                "Task.Claimed",
                "ProcessTask",
                task.Id.ToString(),
                $"Task '{task.Id}' was claimed.",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProcessTaskDto>.Failure("Task was claimed by another user. Refresh and try again.");
        }

        return Result<ProcessTaskDto>.Success(task.ToDto(user));
    }

    public async Task<Result<ProcessTaskDto>> ReleaseAsync(
        Guid taskId,
        ClaimTaskRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        var task = await TaskQuery().SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null)
        {
            return Result<ProcessTaskDto>.Failure("Task was not found.");
        }

        if (!TaskAccessPolicy.IsCandidatePool(task.AssignmentType))
        {
            return Result<ProcessTaskDto>.Failure("Directly assigned tasks do not have a claim to release.");
        }

        if (task.Status != ProcessTaskStatus.Claimed || task.ClaimedByUserId is null)
        {
            return Result<ProcessTaskDto>.Failure("Task is not currently claimed.");
        }

        if (!user.IsSuperAdmin() && task.ClaimedByUserId != user.Id)
        {
            return Result<ProcessTaskDto>.Failure("Only the current claimant can release this task.");
        }

        if (request.ClaimVersion is { } expectedVersion && expectedVersion != task.ClaimVersion)
        {
            return Result<ProcessTaskDto>.Failure("Task claim changed. Refresh and try again.");
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            task.Status = ProcessTaskStatus.Open;
            task.ClaimedByUserId = null;
            task.ClaimedAt = null;
            task.ClaimVersion = Guid.NewGuid();
            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                user,
                "Task.Released",
                "ProcessTask",
                task.Id.ToString(),
                $"Task '{task.Id}' claim was released.",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProcessTaskDto>.Failure("Task claim changed. Refresh and try again.");
        }

        return Result<ProcessTaskDto>.Success(task.ToDto(user));
    }

    public async Task<Result<ProcessDetailDto>> ExecuteActionAsync(
        Guid taskId,
        TaskActionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        var task = await TaskExecutionQuery().SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null || task.ProcessInstance is null)
        {
            return Result<ProcessDetailDto>.Failure("Task was not found.");
        }

        if (task.Status is ProcessTaskStatus.Completed or ProcessTaskStatus.Cancelled)
        {
            return Result<ProcessDetailDto>.Failure("Task is already closed.");
        }

        if (!await db.Communities.AnyAsync(
                community => community.Id == task.ProcessInstance.CommunityId && community.IsActive,
                cancellationToken))
        {
            return Result<ProcessDetailDto>.Failure("The task community is not active.");
        }

        var actionAccess = taskAccessPolicy.Evaluate(task, user);
        if (!actionAccess.CanAct)
        {
            return Result<ProcessDetailDto>.Failure(TaskAccessPolicy.ErrorFor(actionAccess));
        }

        var availableActions = JsonHelpers.Deserialize<IReadOnlyList<WorkflowAction>>(task.AvailableActionsJson, []);
        if (!availableActions.Contains(request.Action))
        {
            return Result<ProcessDetailDto>.Failure($"Action {request.Action} is not available for this task.");
        }

        return task.ProcessInstance.ProcessDefinitionVersionId.HasValue && !string.IsNullOrWhiteSpace(task.NodeKey)
            ? await ExecuteDynamicActionAsync(task, request, user, cancellationToken)
            : await ExecuteLegacyActionAsync(task, request, user, cancellationToken);
    }

    private async Task<Result<ProcessDetailDto>> ExecuteDynamicActionAsync(
        ProcessTask task,
        TaskActionRequest request,
        UserDto user,
        CancellationToken cancellationToken)
    {
        var process = task.ProcessInstance!;
        var output = request.FormData?.Clone() ?? EmptyObject();
        if (task.FormDefinitionVersionId is { } formVersionId)
        {
            var formVersion = await db.FormDefinitionVersions
                .Include(version => version.FormDefinition)
                .Include(version => version.Pages)
                .ThenInclude(page => page.Fields)
                .ThenInclude(field => field.ValidationRules)
                .AsSplitQuery()
                .SingleOrDefaultAsync(version => version.Id == formVersionId, cancellationToken);
            if (formVersion is null || formVersion.Status != DefinitionVersionStatus.Published)
            {
                return Result<ProcessDetailDto>.Failure("Task form version was not found.");
            }

            if (request.FormData is null)
            {
                return Result<ProcessDetailDto>.Failure("Task form data is required.");
            }

            var validationErrors = FormDataValidator.Validate(formVersion.ToDto(), output);
            if (validationErrors.Count > 0)
            {
                return Result<ProcessDetailDto>.Failure(validationErrors);
            }
        }

        var graph = JsonHelpers.Deserialize(
            process.ProcessDefinitionVersion!.GraphJson,
            new ProcessGraphDto("", [], []));
        var previousStatus = process.Status;
        var now = DateTime.UtcNow;

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            task.Status = ProcessTaskStatus.Completed;
            task.CompletedAt = now;
            task.CompletedByUserId = user.Id;
            task.CompletedAction = request.Action;
            task.CompletionNote = request.Note?.Trim() ?? string.Empty;
            task.ClaimVersion = Guid.NewGuid();

            var activeStep = process.StepExecutions.SingleOrDefault(step =>
                step.NodeKey == task.NodeKey
                && step.Attempt == task.Attempt
                && step.Status == ProcessStepStatus.Active);
            if (activeStep is null)
            {
                return Result<ProcessDetailDto>.Failure("Active process step was not found for this task.");
            }

            activeStep.Status = ProcessStepStatus.Completed;
            activeStep.CompletedAt = now;
            activeStep.CompletedByUserId = user.Id;
            activeStep.Action = request.Action;
            activeStep.Note = request.Note?.Trim() ?? string.Empty;
            activeStep.OutputJson = output.GetRawText();
            process.VariablesJson = request.Action == WorkflowAction.SendBack
                ? RemoveInvalidatedStepOutputs(process.VariablesJson, graph, task.NodeKey)
                : MergeStepOutput(process.VariablesJson, task.NodeKey, output);

            var routeResult = await new DynamicWorkflowEngine(db).ContinueAsync(
                process,
                task,
                request.Action,
                graph,
                cancellationToken);
            if (!routeResult.IsSuccess)
            {
                return Result<ProcessDetailDto>.Failure(routeResult.Errors);
            }

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = process.Id,
                UserId = user.Id,
                Action = request.Action,
                FromStatus = previousStatus,
                ToStatus = process.Status,
                CreatedAt = now,
                Note = request.Note ?? string.Empty
            });
            AddProgressNotification(process, user.Id);
            AddOutcomeNotification(process, user.Id);

            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                user,
                $"Task.{request.Action}",
                "ProcessTask",
                task.Id.ToString(),
                $"Task action '{request.Action}' continued process '{process.Id}' from node '{task.NodeKey}'.",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProcessDetailDto>.Failure("Task changed while the action was being applied. Refresh and try again.");
        }

        return Result<ProcessDetailDto>.Success(await LoadProcessDetailAsync(process.Id, user, cancellationToken));
    }

    private async Task<Result<ProcessDetailDto>> ExecuteLegacyActionAsync(
        ProcessTask task,
        TaskActionRequest request,
        UserDto user,
        CancellationToken cancellationToken)
    {
        if (task.Status != ProcessTaskStatus.Open)
        {
            return Result<ProcessDetailDto>.Failure("Task is already closed.");
        }

        var process = task.ProcessInstance!;
        var previousStatus = process.Status;
        var transition = stateMachine.Move(previousStatus, request.Action);
        if (!transition.IsSuccess)
        {
            return Result<ProcessDetailDto>.Failure(transition.Errors);
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            task.Status = ProcessTaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.CompletedByUserId = user.Id;
            task.CompletedAction = request.Action;
            task.CompletionNote = request.Note?.Trim() ?? string.Empty;
            task.ClaimVersion = Guid.NewGuid();

            process.Status = transition.Value;
            if (transition.Value is ProcessStatus.Completed or ProcessStatus.Rejected)
            {
                process.CompletedAt = DateTime.UtcNow;
            }

            if (transition.Value is ProcessStatus.Escalated)
            {
                db.ProcessTasks.Add(new ProcessTask
                {
                    Id = Guid.NewGuid(),
                    ProcessInstanceId = process.Id,
                    AssignedRole = Role.Admin,
                    AssignedCommunityRoleId = task.AssignedCommunityRoleId,
                    RequiredPermission = task.RequiredPermission,
                    Title = task.Title,
                    Priority = task.Priority,
                    Status = ProcessTaskStatus.Open,
                    AvailableActionsJson = JsonHelpers.Serialize(new[] { WorkflowAction.Approve, WorkflowAction.Reject }),
                    ClaimVersion = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow
                });
            }

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = process.Id,
                UserId = user.Id,
                Action = request.Action,
                FromStatus = previousStatus,
                ToStatus = transition.Value,
                CreatedAt = DateTime.UtcNow,
                Note = request.Note ?? string.Empty
            });
            AddOutcomeNotification(process, user.Id);

            await db.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(
                user,
                $"Task.{request.Action}",
                "ProcessTask",
                task.Id.ToString(),
                $"Task action '{request.Action}' moved process '{process.Id}' from {previousStatus} to {transition.Value}.",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProcessDetailDto>.Failure("Task changed while the action was being applied. Refresh and try again.");
        }

        return Result<ProcessDetailDto>.Success(await LoadProcessDetailAsync(process.Id, user, cancellationToken));
    }

    private IQueryable<ProcessTask> TaskQuery() =>
        db.ProcessTasks
            .Include(task => task.AssignedCommunityRole)
            .Include(task => task.AssignedUser)
            .Include(task => task.CandidateTeam)
            .Include(task => task.CandidateCommunityRole)
            .Include(task => task.ClaimedByUser)
            .Include(task => task.CompletedByUser)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.FormDefinition)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.Community)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.ProcessDefinitionVersion)
            .ThenInclude(version => version!.ProcessDefinition)
            .Include(task => task.FormDefinitionVersion)
            .ThenInclude(version => version!.FormDefinition)
            .Include(task => task.FormDefinitionVersion)
            .ThenInclude(version => version!.Pages)
            .ThenInclude(page => page.Fields)
            .ThenInclude(field => field.ValidationRules)
            .AsSplitQuery();

    private sealed class TaskListProjection
    {
        public Guid Id { get; init; }
        public Guid ProcessInstanceId { get; init; }
        public Guid CommunityId { get; init; }
        public Guid? AssignedCommunityRoleId { get; init; }
        public string AssignedCommunityRoleName { get; init; } = string.Empty;
        public string RequiredPermission { get; init; } = string.Empty;
        public ProcessTaskStatus Status { get; init; }
        public string AvailableActionsJson { get; init; } = "[]";
        public DateTime CreatedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public string NodeKey { get; init; } = string.Empty;
        public int Attempt { get; init; }
        public string Title { get; init; } = string.Empty;
        public TaskPriority Priority { get; init; }
        public TaskAssignmentType? AssignmentType { get; init; }
        public Guid? AssignedUserId { get; init; }
        public Guid? CandidateTeamId { get; init; }
        public Guid? CandidateCommunityRoleId { get; init; }
        public Guid? ClaimedByUserId { get; init; }
        public DateTime? ClaimedAt { get; init; }
        public Guid ClaimVersion { get; init; }
        public Guid? FormDefinitionVersionId { get; init; }
        public DateTime? DueAt { get; init; }
        public string WorkflowName { get; init; } = string.Empty;
        public string FormName { get; init; } = string.Empty;
        public string CommunityName { get; init; } = string.Empty;
        public bool RequiresTeamLead { get; init; }
        public string AssignedUserDisplayName { get; init; } = string.Empty;
        public string CandidateTeamName { get; init; } = string.Empty;
        public string CandidateCommunityRoleName { get; init; } = string.Empty;
        public string ClaimedByUserDisplayName { get; init; } = string.Empty;
        public Guid? CompletedByUserId { get; init; }
        public string CompletedByUserDisplayName { get; init; } = string.Empty;
        public WorkflowAction? CompletedAction { get; init; }
        public string CompletionNote { get; init; } = string.Empty;

        public ProcessTaskDto ToDto(UserDto user, TaskAccessPolicy policy)
        {
            var access = policy.Evaluate(new ProcessTask
            {
                Id = Id,
                ProcessInstanceId = ProcessInstanceId,
                ProcessInstance = new ProcessInstance { CommunityId = CommunityId },
                AssignedCommunityRoleId = AssignedCommunityRoleId,
                RequiredPermission = RequiredPermission,
                Status = Status,
                AssignmentType = AssignmentType,
                AssignedUserId = AssignedUserId,
                CandidateTeamId = CandidateTeamId,
                CandidateCommunityRoleId = CandidateCommunityRoleId,
                ClaimedByUserId = ClaimedByUserId,
                RequiresTeamLead = RequiresTeamLead,
                CompletedByUserId = CompletedByUserId,
            }, user);

            return new ProcessTaskDto(
                Id,
                ProcessInstanceId,
                AssignedCommunityRoleId,
                AssignedCommunityRoleName,
                RequiredPermission,
                Status,
                JsonHelpers.Deserialize<IReadOnlyList<WorkflowAction>>(AvailableActionsJson, []),
                CreatedAt,
                CompletedAt,
                NodeKey,
                Attempt,
                Title,
                Priority,
                AssignmentType,
                AssignedUserId,
                CandidateTeamId,
                CandidateCommunityRoleId,
                ClaimedByUserId,
                ClaimedAt,
                ClaimVersion,
                FormDefinitionVersionId,
                null,
                DueAt,
                WorkflowName,
                FormName,
                CommunityName,
                RequiresTeamLead,
                access.CanAct,
                access.ActionDenialReasonCode,
                AssignedUserDisplayName,
                CandidateTeamName,
                CandidateCommunityRoleName,
                ClaimedByUserDisplayName,
                CompletedByUserId,
                CompletedByUserDisplayName,
                CompletedAction,
                CompletionNote,
                access.CanClaim,
                access.ClaimDenialReasonCode);
        }
    }

    private IQueryable<ProcessTask> TaskExecutionQuery() =>
        db.ProcessTasks
            .Include(task => task.AssignedCommunityRole)
            .Include(task => task.AssignedUser)
            .Include(task => task.CandidateTeam)
            .Include(task => task.CandidateCommunityRole)
            .Include(task => task.ClaimedByUser)
            .Include(task => task.CompletedByUser)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.FormDefinition)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.ProcessDefinitionVersion)
            .ThenInclude(version => version!.ProcessDefinition)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.StepExecutions)
            .ThenInclude(step => step.CompletedByUser)
            .Include(task => task.ProcessInstance)
            .ThenInclude(process => process!.Tasks)
            .Include(task => task.FormDefinitionVersion)
            .ThenInclude(version => version!.FormDefinition)
            .Include(task => task.FormDefinitionVersion)
            .ThenInclude(version => version!.Pages)
            .ThenInclude(page => page.Fields)
            .ThenInclude(field => field.ValidationRules)
            .AsSplitQuery();

    private async Task<ProcessDetailDto> LoadProcessDetailAsync(Guid processId, UserDto user, CancellationToken cancellationToken)
    {
        var process = await db.ProcessInstances
            .AsNoTracking()
            .Include(item => item.FormDefinition)
            .Include(item => item.Community)
            .Include(item => item.ProcessDefinitionVersion)
            .ThenInclude(version => version!.ProcessDefinition)
            .Include(item => item.Tasks)
            .ThenInclude(task => task.AssignedCommunityRole)
            .Include(item => item.Tasks)
            .ThenInclude(task => task.AssignedUser)
            .Include(item => item.Tasks)
            .ThenInclude(task => task.CandidateTeam)
            .Include(item => item.Tasks)
            .ThenInclude(task => task.CandidateCommunityRole)
            .Include(item => item.Tasks)
            .ThenInclude(task => task.ClaimedByUser)
            .Include(item => item.Tasks)
            .ThenInclude(task => task.CompletedByUser)
            .Include(item => item.Tasks)
            .ThenInclude(task => task.FormDefinitionVersion)
            .ThenInclude(version => version!.FormDefinition)
            .Include(item => item.Tasks)
            .ThenInclude(task => task.FormDefinitionVersion)
            .ThenInclude(version => version!.Pages)
            .ThenInclude(page => page.Fields)
            .ThenInclude(field => field.ValidationRules)
            .Include(item => item.StepExecutions)
            .ThenInclude(step => step.CompletedByUser)
            .Include(item => item.AuditLogs)
            .ThenInclude(log => log.User)
            .AsSplitQuery()
            .SingleAsync(item => item.Id == processId, cancellationToken);
        return process.ToDetailDto(user);
    }

    private void AddOutcomeNotification(ProcessInstance process, Guid actorUserId)
    {
        if (process.Status is not (ProcessStatus.Completed or ProcessStatus.Rejected)
            || process.StartedByUserId == actorUserId)
        {
            return;
        }

        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = process.StartedByUserId,
            Type = $"Process.{process.Status}",
            Title = "Surec durumunuz guncellendi",
            Message = $"Baslattiginiz surec {process.Status} durumuna gecti.",
            EntityType = "ProcessInstance",
            EntityId = process.Id.ToString(),
            CreatedAt = DateTime.UtcNow
        });
    }

    private void AddProgressNotification(ProcessInstance process, Guid actorUserId)
    {
        if (process.Status != ProcessStatus.InProgress || process.StartedByUserId == actorUserId)
        {
            return;
        }

        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = process.StartedByUserId,
            Type = "Process.Advanced",
            Title = "Sureciniz yeni bir adima gecti",
            Message = $"Baslattiginiz surec '{process.CurrentNodeKey}' adiminda ilerliyor.",
            EntityType = "ProcessInstance",
            EntityId = process.Id.ToString(),
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string MergeStepOutput(string variablesJson, string nodeKey, JsonElement output)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(variablesJson) ? "{}" : variablesJson) as JsonObject
            ?? new JsonObject();
        var steps = root["steps"] as JsonObject ?? new JsonObject();
        root["steps"] = steps;
        steps[nodeKey] = JsonNode.Parse(output.GetRawText());
        return root.ToJsonString();
    }

    private static string RemoveInvalidatedStepOutputs(
        string variablesJson,
        ProcessGraphDto graph,
        string sourceNodeKey)
    {
        var sendBackTarget = (graph.Edges ?? [])
            .Where(edge => edge.Source == sourceNodeKey && edge.Action == WorkflowAction.SendBack)
            .OrderBy(edge => edge.Order)
            .Select(edge => edge.Target)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sendBackTarget))
        {
            return variablesJson;
        }

        var invalidatedNodes = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(sendBackTarget);
        while (pending.TryDequeue(out var current))
        {
            if (!invalidatedNodes.Add(current)) continue;
            foreach (var target in (graph.Edges ?? [])
                         .Where(edge => edge.Action != WorkflowAction.SendBack && edge.Source == current)
                         .Select(edge => edge.Target))
            {
                pending.Enqueue(target);
            }
        }

        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(variablesJson) ? "{}" : variablesJson) as JsonObject
            ?? new JsonObject();
        if (root["steps"] is JsonObject steps)
        {
            foreach (var nodeKey in invalidatedNodes)
            {
                steps.Remove(nodeKey);
            }
        }
        return root.ToJsonString();
    }

    private static JsonElement EmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
