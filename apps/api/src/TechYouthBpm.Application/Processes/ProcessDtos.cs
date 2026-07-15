using System.Text.Json;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Application.Processes;

public record AuditLogDto(
    Guid Id,
    WorkflowAction Action,
    ProcessStatus FromStatus,
    ProcessStatus ToStatus,
    Guid UserId,
    string UserDisplayName,
    string UserUsername,
    DateTime CreatedAt,
    string Note);

public record ProcessTaskDto(
    Guid Id,
    Guid ProcessInstanceId,
    Guid? AssignedCommunityRoleId,
    string AssignedCommunityRoleName,
    string RequiredPermission,
    ProcessTaskStatus Status,
    IReadOnlyList<WorkflowAction> AvailableActions,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string NodeKey = "",
    int Attempt = 1,
    string Title = "",
    TaskPriority Priority = TaskPriority.Normal,
    TaskAssignmentType? AssignmentType = null,
    Guid? AssignedUserId = null,
    Guid? CandidateTeamId = null,
    Guid? CandidateCommunityRoleId = null,
    Guid? ClaimedByUserId = null,
    DateTime? ClaimedAt = null,
    Guid? ClaimVersion = null,
    Guid? FormDefinitionVersionId = null,
    FormDefinitionVersionDto? TaskForm = null);

public record ProcessSummaryDto(
    Guid Id,
    Guid FormDefinitionId,
    string FormName,
    Guid CommunityId,
    string CommunityName,
    ProcessStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    Guid? ProcessDefinitionVersionId = null,
    Guid? FormDefinitionVersionId = null,
    string CurrentNodeKey = "");

public record ProcessStepExecutionDto(
    Guid Id,
    string NodeKey,
    ProcessNodeType NodeType,
    int Attempt,
    ProcessStepStatus Status,
    DateTime EnteredAt,
    DateTime? CompletedAt,
    Guid? CompletedByUserId,
    string? CompletedByUserDisplayName,
    WorkflowAction? Action,
    JsonElement Output);

public record ProcessDetailDto(
    Guid Id,
    Guid FormDefinitionId,
    string FormName,
    Guid CommunityId,
    string CommunityName,
    ProcessStatus Status,
    JsonElement FormData,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ProcessTaskDto> Tasks,
    IReadOnlyList<AuditLogDto> AuditLogs,
    Guid? ProcessDefinitionVersionId = null,
    Guid? FormDefinitionVersionId = null,
    string CurrentNodeKey = "",
    JsonElement Variables = default,
    IReadOnlyList<ProcessStepExecutionDto>? StepExecutions = null);

public record TaskActionRequest(WorkflowAction Action, string? Note, JsonElement? FormData = null);
