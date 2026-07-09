using System.Text.Json;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Application.Forms;

public record ValidationRuleDto(
    ValidationRuleType RuleType,
    string DependsOnFieldKey,
    string ExpectedValue,
    string Message);

public record FormFieldDto(
    Guid Id,
    string Key,
    string Label,
    FieldType Type,
    bool Required,
    int SortOrder,
    IReadOnlyList<string> Options,
    IReadOnlyList<ValidationRuleDto> ValidationRules);

public record CreateFormFieldRequest(
    string Key,
    string Label,
    FieldType Type,
    bool Required,
    int SortOrder,
    IReadOnlyList<string> Options,
    IReadOnlyList<ValidationRuleDto> ValidationRules);

public record FormDefinitionDto(
    Guid Id,
    string Name,
    string Description,
    Guid CommunityId,
    string CommunityName,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    IReadOnlyList<FormFieldDto> Fields);

public record CreateFormRequest(
    string Name,
    string Description,
    IReadOnlyList<CreateFormFieldRequest> Fields,
    Guid? CommunityId = null);

public record StartProcessRequest(Guid FormDefinitionId, JsonElement FormData);
