using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Infrastructure.Services;

internal static class MappingExtensions
{
    public static UserDto ToDto(this User user)
    {
        var membership = ActiveMembership(user);
        var permissions = user.Role == Role.SuperAdmin
            ? PermissionNames.All
            : membership?.CommunityRole?.Permissions.Select(permission => permission.Permission).Distinct().Order().ToArray() ?? [];

        return new UserDto(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email,
            user.Role,
            user.Status,
            user.IsEmailVerified,
            user.MustChangePassword,
            membership?.CommunityId,
            membership?.Community?.Name ?? string.Empty,
            membership?.CommunityRoleId,
            membership?.CommunityRole?.Name ?? string.Empty,
            permissions);
    }

    public static UserAdminDto ToAdminDto(this User user)
    {
        var dto = user.ToDto();
        return new UserAdminDto(
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
            user.MustChangePassword,
            dto.CommunityId,
            dto.CommunityName,
            dto.CommunityRoleId,
            dto.CommunityRoleName,
            dto.Permissions);
    }

    public static FormDefinitionDto ToDto(this FormDefinition form) =>
        new(
            form.Id,
            form.Name,
            form.Description,
            form.CommunityId,
            form.Community?.Name ?? string.Empty,
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
            task.AssignedCommunityRoleId,
            task.AssignedCommunityRole?.Name ?? string.Empty,
            task.RequiredPermission,
            task.Status,
            JsonHelpers.Deserialize<IReadOnlyList<WorkflowAction>>(task.AvailableActionsJson, []),
            task.CreatedAt,
            task.CompletedAt);

    public static ProcessSummaryDto ToSummaryDto(this ProcessInstance process) =>
        new(
            process.Id,
            process.FormDefinitionId,
            process.FormDefinition?.Name ?? "Unknown form",
            process.CommunityId,
            process.Community?.Name ?? string.Empty,
            process.Status,
            process.StartedAt,
            process.CompletedAt);

    public static ProcessDetailDto ToDetailDto(this ProcessInstance process) =>
        new(
            process.Id,
            process.FormDefinitionId,
            process.FormDefinition?.Name ?? "Unknown form",
            process.CommunityId,
            process.Community?.Name ?? string.Empty,
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

    public static CommunityDto ToDto(this Community community) =>
        new(community.Id, community.Name, community.Description, community.InviteCode, community.IsActive, community.CreatedAt);

    public static CommunityRoleDto ToDto(this CommunityRole role) =>
        new(
            role.Id,
            role.CommunityId,
            role.Name,
            role.Description,
            role.TemplateKey,
            role.IsSystemRole,
            role.Permissions.Select(permission => permission.Permission).Order().ToArray());

    public static NotificationDto ToDto(this Notification notification) =>
        new(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.EntityType,
            notification.EntityId,
            notification.CreatedAt,
            notification.ReadAt);

    private static UserCommunityMembership? ActiveMembership(User user) =>
        user.CommunityMemberships.FirstOrDefault(membership => membership.IsActive);
}
