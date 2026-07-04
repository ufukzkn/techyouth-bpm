using System.Text.Json;
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
    Role AssignedRole,
    ProcessTaskStatus Status,
    IReadOnlyList<WorkflowAction> AvailableActions,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record ProcessSummaryDto(
    Guid Id,
    Guid FormDefinitionId,
    string FormName,
    ProcessStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt);

public record ProcessDetailDto(
    Guid Id,
    Guid FormDefinitionId,
    string FormName,
    ProcessStatus Status,
    JsonElement FormData,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ProcessTaskDto> Tasks,
    IReadOnlyList<AuditLogDto> AuditLogs);

public record TaskActionRequest(WorkflowAction Action, string? Note);
