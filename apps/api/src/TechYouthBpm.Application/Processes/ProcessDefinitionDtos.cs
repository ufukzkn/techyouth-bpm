using System.Text.Json;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Application.Processes;

public record TaskAssignmentDto(
    TaskAssignmentType Type,
    Guid? UserId = null,
    Guid? TeamId = null,
    Guid? CommunityRoleId = null);

public record ProcessNodeDto(
    string Key,
    ProcessNodeType Type,
    string Title = "",
    Guid? FormDefinitionVersionId = null,
    TaskPriority Priority = TaskPriority.Normal,
    IReadOnlyList<WorkflowAction>? Actions = null,
    TaskAssignmentDto? Assignment = null,
    string? ParentKey = null,
    double PositionX = 0,
    double PositionY = 0,
    double? Width = null,
    double? Height = null,
    string? Description = null,
    Guid? TeamId = null);

public record ProcessConditionDto(
    string Path,
    GraphConditionOperator Operator,
    JsonElement? Value = null);

public record ProcessEdgeDto(
    string Source,
    string Target,
    WorkflowAction? Action = null,
    ProcessConditionDto? Condition = null,
    bool IsDefault = false,
    int Order = 0,
    string? Label = null);

public record ProcessGraphDto(
    string SchemaVersion,
    IReadOnlyList<ProcessNodeDto> Nodes,
    IReadOnlyList<ProcessEdgeDto> Edges);

public record ProcessDefinitionSummaryDto(
    Guid Id,
    string Name,
    string Description,
    Guid CommunityId,
    string CommunityName,
    int? LatestVersionNumber,
    Guid? LatestPublishedVersionId,
    Guid? LatestPublishedFormDefinitionVersionId,
    DateTime CreatedAt);

public record RunnableProcessDefinitionDto(
    Guid Id,
    string Name,
    string Description,
    Guid CommunityId,
    string CommunityName,
    Guid ProcessDefinitionVersionId,
    Guid FormDefinitionVersionId,
    int VersionNumber);

public record ProcessDefinitionDto(
    Guid Id,
    string Name,
    string Description,
    Guid CommunityId,
    string CommunityName,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    IReadOnlyList<ProcessDefinitionVersionDto> Versions);

public record ProcessDefinitionVersionDto(
    Guid Id,
    Guid ProcessDefinitionId,
    int VersionNumber,
    DefinitionVersionStatus Status,
    Guid FormDefinitionVersionId,
    ProcessGraphDto Graph,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    Guid? PublishedByUserId,
    DateTime? PublishedAt);

public record CreateProcessDefinitionRequest(
    string Name,
    string Description,
    Guid? CommunityId = null);

public record UpdateProcessDefinitionRequest(string Name, string Description);

public record CreateProcessDefinitionVersionRequest(
    Guid FormDefinitionVersionId,
    ProcessGraphDto Graph);

public record UpdateProcessDefinitionVersionRequest(
    Guid FormDefinitionVersionId,
    ProcessGraphDto Graph);

public record StartProcessVersionRequest(
    Guid ProcessDefinitionVersionId,
    JsonElement FormData);

public record ClaimTaskRequest(Guid? ClaimVersion = null);

public record ProcessGraphValidationDto(bool IsValid, IReadOnlyList<string> Errors);
