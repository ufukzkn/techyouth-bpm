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
        var teams = user.TeamMemberships
            .Where(teamMembership => teamMembership.IsActive && teamMembership.Team?.IsActive == true)
            .OrderBy(teamMembership => teamMembership.Team!.Name)
            .Select(teamMembership => new UserTeamDto(teamMembership.TeamId, teamMembership.Team!.Name, teamMembership.IsLead))
            .ToArray();

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
            permissions,
            membership?.Community?.IsActive ?? true,
            teams);
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
            dto.Permissions,
            dto.IsCommunityActive,
            dto.Teams);
    }

    public static FormDefinitionDto ToDto(this FormDefinition form)
    {
        var latestPublished = form.Versions
            .Where(version => version.Status == DefinitionVersionStatus.Published)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();

        return new(
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
                .ToArray(),
            latestPublished?.Id,
            latestPublished?.VersionNumber);
    }

    public static FormDefinitionVersionDto ToDto(this FormDefinitionVersion version) =>
        new(
            version.Id,
            version.FormDefinitionId,
            version.FormDefinition?.Name ?? string.Empty,
            version.VersionNumber,
            version.Status,
            version.CreatedByUserId,
            version.CreatedAt,
            version.PublishedByUserId,
            version.PublishedAt,
            version.Pages
                .OrderBy(page => page.SortOrder)
                .Select(page => new FormPageDto(
                    page.Id,
                    page.Key,
                    page.Title,
                    page.Description,
                    page.SortOrder,
                    page.Fields
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
                        .ToArray()))
                .ToArray());

    public static ProcessDefinitionVersionDto ToDto(this ProcessDefinitionVersion version) =>
        new(
            version.Id,
            version.ProcessDefinitionId,
            version.VersionNumber,
            version.Status,
            version.FormDefinitionVersionId,
            JsonHelpers.Deserialize(version.GraphJson, new ProcessGraphDto("1.0", [], [])),
            version.CreatedByUserId,
            version.CreatedAt,
            version.PublishedByUserId,
            version.PublishedAt);

    public static ProcessDefinitionDto ToDto(this ProcessDefinition definition) =>
        new(
            definition.Id,
            definition.Name,
            definition.Description,
            definition.CommunityId,
            definition.Community?.Name ?? string.Empty,
            definition.CreatedByUserId,
            definition.CreatedAt,
            definition.Versions
                .OrderByDescending(version => version.VersionNumber)
                .Select(version => version.ToDto())
                .ToArray());

    public static ProcessTaskDto ToDto(this ProcessTask task, UserDto? currentUser = null)
    {
        var canCurrentUserAct = !task.RequiresTeamLead
            || currentUser is null
            || currentUser.IsSuperAdmin()
            || (task.CandidateTeamId is { } teamId
                && (currentUser.Teams ?? []).Any(team => team.Id == teamId && team.IsLead));

        return new(
            task.Id,
            task.ProcessInstanceId,
            task.AssignedCommunityRoleId,
            task.AssignedCommunityRole?.Name ?? string.Empty,
            task.RequiredPermission,
            task.Status,
            JsonHelpers.Deserialize<IReadOnlyList<WorkflowAction>>(task.AvailableActionsJson, []),
            task.CreatedAt,
            task.CompletedAt,
            task.NodeKey,
            task.Attempt,
            task.Title,
            task.Priority,
            task.AssignmentType,
            task.AssignedUserId,
            task.CandidateTeamId,
            task.CandidateCommunityRoleId,
            task.ClaimedByUserId,
            task.ClaimedAt,
            task.ClaimVersion,
            task.FormDefinitionVersionId,
            task.FormDefinitionVersion?.ToDto(),
            task.DueAt,
            task.ProcessInstance?.ProcessDefinitionVersion?.ProcessDefinition?.Name ?? string.Empty,
            task.FormDefinitionVersion?.FormDefinition?.Name
                ?? task.ProcessInstance?.FormDefinition?.Name
                ?? string.Empty,
            task.ProcessInstance?.Community?.Name ?? string.Empty,
            task.RequiresTeamLead,
            canCurrentUserAct,
            canCurrentUserAct ? null : TaskActionDenialReasonCodes.TeamLeadRequired);
    }

    public static ProcessSummaryDto ToSummaryDto(this ProcessInstance process) =>
        new(
            process.Id,
            process.FormDefinitionId,
            process.FormDefinition?.Name ?? "Unknown form",
            process.CommunityId,
            process.Community?.Name ?? string.Empty,
            process.Status,
            process.StartedAt,
            process.CompletedAt,
            process.ProcessDefinitionVersionId,
            process.FormDefinitionVersionId,
            process.CurrentNodeKey,
            process.ProcessDefinitionVersion?.ProcessDefinition?.Name ?? string.Empty,
            process.Tasks
                .Where(task => (task.Status is ProcessTaskStatus.Open or ProcessTaskStatus.Claimed) && task.DueAt.HasValue)
                .OrderBy(task => task.DueAt)
                .Select(task => task.DueAt)
                .FirstOrDefault(),
            process.Tasks
                .Where(task => task.Status is ProcessTaskStatus.Open or ProcessTaskStatus.Claimed)
                .OrderByDescending(task => task.Priority)
                .Select(task => (TaskPriority?)task.Priority)
                .FirstOrDefault());

    public static ProcessDetailDto ToDetailDto(this ProcessInstance process, UserDto? currentUser = null) =>
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
            process.Tasks.OrderByDescending(task => task.CreatedAt).Select(task => task.ToDto(currentUser)).ToArray(),
            process.AuditLogs.OrderBy(log => log.CreatedAt).Select(log => new AuditLogDto(
                log.Id,
                log.Action,
                log.FromStatus,
                log.ToStatus,
                log.UserId,
                log.User?.DisplayName ?? "Unknown user",
                log.User?.Username ?? "unknown",
                log.CreatedAt,
                log.Note)).ToArray(),
            process.ProcessDefinitionVersionId,
            process.FormDefinitionVersionId,
            process.CurrentNodeKey,
            JsonHelpers.ToElement(process.VariablesJson),
            process.StepExecutions
                .OrderBy(step => step.EnteredAt)
                .Select(step => new ProcessStepExecutionDto(
                    step.Id,
                    step.NodeKey,
                    step.NodeType,
                    step.Attempt,
                    step.Status,
                    step.EnteredAt,
                    step.CompletedAt,
                    step.CompletedByUserId,
                    step.CompletedByUser?.DisplayName,
                    step.Action,
                    JsonHelpers.ToElement(step.OutputJson)))
                .ToArray());

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
