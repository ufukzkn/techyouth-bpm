using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Infrastructure.Services;

internal static class MappingExtensions
{
    public static UserDto ToDto(this User user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email,
            user.Role,
            user.Status,
            user.IsEmailVerified,
            user.MustChangePassword);

    public static UserAdminDto ToAdminDto(this User user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email,
            user.Role,
            user.Status,
            user.IsEmailVerified,
            user.FailedLoginCount,
            user.LockedUntil,
            user.CreatedAt,
            user.MustChangePassword);

    public static FormDefinitionDto ToDto(this FormDefinition form) =>
        new(
            form.Id,
            form.Name,
            form.Description,
            form.CreatedByUserId,
            form.CreatedAt,
            form.Fields
                .OrderBy(field => field.SortOrder)
                .Select(field => new FormFieldDto(
                    field.Id,
                    field.Key,
                    field.Label,
                    field.Type,
                    field.Required,
                    field.SortOrder,
                    JsonHelpers.Deserialize<IReadOnlyList<string>>(field.OptionsJson, []),
                    field.ValidationRules.Select(rule => new ValidationRuleDto(
                        rule.RuleType,
                        rule.DependsOnFieldKey,
                        rule.ExpectedValue,
                        rule.Message)).ToArray()))
                .ToArray());

    public static ProcessTaskDto ToDto(this ProcessTask task) =>
        new(
            task.Id,
            task.ProcessInstanceId,
            task.AssignedRole,
            task.Status,
            JsonHelpers.Deserialize<IReadOnlyList<WorkflowAction>>(task.AvailableActionsJson, []),
            task.CreatedAt,
            task.CompletedAt);

    public static ProcessSummaryDto ToSummaryDto(this ProcessInstance process) =>
        new(
            process.Id,
            process.FormDefinitionId,
            process.FormDefinition?.Name ?? "Unknown form",
            process.Status,
            process.StartedAt,
            process.CompletedAt);

    public static ProcessDetailDto ToDetailDto(this ProcessInstance process) =>
        new(
            process.Id,
            process.FormDefinitionId,
            process.FormDefinition?.Name ?? "Unknown form",
            process.Status,
            JsonHelpers.ToElement(process.FormDataJson),
            process.StartedAt,
            process.CompletedAt,
            process.Tasks.OrderByDescending(task => task.CreatedAt).Select(task => task.ToDto()).ToArray(),
            process.AuditLogs.OrderBy(log => log.CreatedAt).Select(log => new AuditLogDto(
                log.Id,
                log.Action,
                log.FromStatus,
                log.ToStatus,
                log.UserId,
                log.User?.DisplayName ?? "Unknown user",
                log.User?.Username ?? "unknown",
                log.CreatedAt,
                log.Note)).ToArray());
}
