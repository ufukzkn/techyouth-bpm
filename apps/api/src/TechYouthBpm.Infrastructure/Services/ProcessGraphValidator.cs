using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class ProcessGraphValidator(AppDbContext db) : IProcessGraphValidator
{
    private const string SupportedSchemaVersion = "1.0";

    public Result ValidateStructure(ProcessGraphDto graph)
    {
        var errors = new List<string>();
        var nodes = graph.Nodes ?? [];
        var edges = graph.Edges ?? [];

        if (!string.Equals(graph.SchemaVersion?.Trim(), SupportedSchemaVersion, StringComparison.Ordinal))
        {
            errors.Add($"Graph schemaVersion must be '{SupportedSchemaVersion}'.");
        }

        if (nodes.Count == 0)
        {
            return Result.Failure(errors.Append("The process graph needs nodes."));
        }

        var duplicateKeys = nodes
            .GroupBy(node => node.Key?.Trim() ?? string.Empty, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (var key in duplicateKeys)
        {
            errors.Add($"Node key '{key}' is duplicated.");
        }

        foreach (var node in nodes)
        {
            ValidateNode(node, errors);
        }

        var nodeByKey = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Key))
            .GroupBy(node => node.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var node in nodes.Where(node => !string.IsNullOrWhiteSpace(node.ParentKey)))
        {
            if (!nodeByKey.TryGetValue(node.ParentKey!, out var parent) || parent.Type != ProcessNodeType.TeamSwimlane)
            {
                errors.Add($"Node '{node.Key}' parent '{node.ParentKey}' must reference a TeamSwimlane.");
            }
        }

        var starts = nodes.Where(node => node.Type == ProcessNodeType.Start).ToArray();
        if (starts.Length != 1)
        {
            errors.Add("The process graph must contain exactly one Start node.");
        }

        if (!nodes.Any(IsEnd))
        {
            errors.Add("The process graph must contain at least one end node.");
        }

        foreach (var edge in edges)
        {
            if (!nodeByKey.TryGetValue(edge.Source ?? string.Empty, out var source))
            {
                errors.Add($"Edge source '{edge.Source}' does not exist.");
                continue;
            }

            var targetKey = edge.Target ?? string.Empty;
            if (!nodeByKey.TryGetValue(targetKey, out var target))
            {
                errors.Add($"Edge target '{edge.Target}' does not exist.");
            }
            else if (target.Type == ProcessNodeType.TeamSwimlane)
            {
                errors.Add($"Runtime edge target '{edge.Target}' cannot be a TeamSwimlane.");
            }

            if (source.Type == ProcessNodeType.TeamSwimlane)
            {
                errors.Add($"Visual swimlane '{source.Key}' cannot have runtime edges.");
            }

            if (string.Equals(edge.Source, edge.Target, StringComparison.Ordinal))
            {
                errors.Add($"Edge from '{edge.Source}' cannot target itself.");
            }
        }

        foreach (var node in nodes.Where(node => node.Type != ProcessNodeType.TeamSwimlane))
        {
            var outgoing = edges.Where(edge => edge.Source == node.Key).ToArray();
            var incoming = edges.Where(edge => edge.Target == node.Key).ToArray();

            switch (node.Type)
            {
                case ProcessNodeType.Start:
                    if (incoming.Length > 0 || outgoing.Length != 1)
                    {
                        errors.Add($"Start node '{node.Key}' must have no incoming edge and exactly one outgoing edge.");
                    }
                    if (outgoing.Any(edge => edge.Action is not null || edge.Condition is not null || edge.IsDefault))
                    {
                        errors.Add($"Start node '{node.Key}' outgoing edge cannot contain an action, condition or default marker.");
                    }
                    break;
                case ProcessNodeType.UserTask:
                    ValidateUserTaskEdges(node, outgoing, errors);
                    break;
                case ProcessNodeType.ExclusiveGateway:
                    ValidateGatewayEdges(node, outgoing, errors);
                    break;
                case ProcessNodeType.CompletedEnd:
                case ProcessNodeType.RejectedEnd:
                    if (outgoing.Length > 0)
                    {
                        errors.Add($"End node '{node.Key}' cannot have outgoing edges.");
                    }
                    break;
            }
        }

        if (starts.Length == 1)
        {
            ValidateReachability(starts[0], nodes, edges, errors);
        }

        ValidateCyclesAndSendBack(nodes, edges, nodeByKey, errors);

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }

    public async Task<Result> ValidateForPublishAsync(
        ProcessGraphDto graph,
        Guid communityId,
        Guid formDefinitionVersionId,
        CancellationToken cancellationToken = default)
    {
        var structural = ValidateStructure(graph);
        var errors = structural.Errors.ToList();

        if (!await IsPublishedFormVersionInCommunityAsync(formDefinitionVersionId, communityId, cancellationToken))
        {
            errors.Add("The start form version must be published and belong to the process community.");
        }

        var start = (graph.Nodes ?? []).SingleOrDefault(node => node.Type == ProcessNodeType.Start);
        if (start?.FormDefinitionVersionId is { } graphStartFormId && graphStartFormId != formDefinitionVersionId)
        {
            errors.Add("The Start node form version must match the process definition version start form.");
        }

        foreach (var formVersionId in (graph.Nodes ?? [])
                     .Where(node => node.Type == ProcessNodeType.UserTask && node.FormDefinitionVersionId.HasValue)
                     .Select(node => node.FormDefinitionVersionId!.Value)
                     .Distinct())
        {
            if (!await IsPublishedFormVersionInCommunityAsync(formVersionId, communityId, cancellationToken))
            {
                errors.Add($"Task form version '{formVersionId}' must be published and belong to the process community.");
            }
        }

        foreach (var node in (graph.Nodes ?? []).Where(node => node.Type == ProcessNodeType.UserTask && node.Assignment is not null))
        {
            await ValidateAssignmentReferencesAsync(node, communityId, errors, cancellationToken);
        }

        await ValidateConditionReferencesAsync(
            graph,
            formDefinitionVersionId,
            errors,
            cancellationToken);

        foreach (var swimlane in (graph.Nodes ?? []).Where(node => node.Type == ProcessNodeType.TeamSwimlane))
        {
            if (swimlane.TeamId is not { } teamId
                || !await db.Teams.AnyAsync(
                    team => team.Id == teamId && team.CommunityId == communityId && team.IsActive,
                    cancellationToken))
            {
                errors.Add($"Swimlane '{swimlane.Key}' must reference an active team in the process community.");
            }
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors.Distinct());
    }

    private static void ValidateNode(ProcessNodeDto node, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(node.Key))
        {
            errors.Add("Every node needs a stable key.");
        }

        if (!double.IsFinite(node.PositionX) || !double.IsFinite(node.PositionY))
        {
            errors.Add($"Node '{node.Key}' must have finite canvas coordinates.");
        }

        if (node.Width is { } width && (!double.IsFinite(width) || width <= 0))
        {
            errors.Add($"Node '{node.Key}' width must be a positive finite number.");
        }

        if (node.Height is { } height && (!double.IsFinite(height) || height <= 0))
        {
            errors.Add($"Node '{node.Key}' height must be a positive finite number.");
        }

        if (node.Type != ProcessNodeType.UserTask)
        {
            if (node.Type == ProcessNodeType.TeamSwimlane && node.TeamId is null)
            {
                errors.Add($"Team swimlane '{node.Key}' needs a team.");
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(node.Title))
        {
            errors.Add($"User task '{node.Key}' needs a title.");
        }

        var actions = node.Actions ?? [];
        if (actions.Count == 0)
        {
            errors.Add($"User task '{node.Key}' needs at least one action.");
        }
        else if (actions.Distinct().Count() != actions.Count)
        {
            errors.Add($"User task '{node.Key}' has duplicate actions.");
        }

        if (node.Assignment is null)
        {
            errors.Add($"User task '{node.Key}' needs an assignment.");
            return;
        }

        if (node.SlaDurationMinutes is { } slaMinutes && (slaMinutes < 1 || slaMinutes > 525_600))
        {
            errors.Add($"User task '{node.Key}' SLA must be between 1 minute and 365 days.");
        }

        var assignment = node.Assignment;
        if (node.RequiresTeamLead
            && assignment.Type is not (TaskAssignmentType.Team or TaskAssignmentType.TeamAndCommunityRole))
        {
            errors.Add($"User task '{node.Key}' can require a team lead only for Team or TeamAndCommunityRole assignments.");
        }

        switch (assignment.Type)
        {
            case TaskAssignmentType.ProcessStarter:
                break;
            case TaskAssignmentType.SpecificUser when assignment.UserId is null:
                errors.Add($"User task '{node.Key}' needs a specific user.");
                break;
            case TaskAssignmentType.Team when assignment.TeamId is null:
                errors.Add($"User task '{node.Key}' needs a team.");
                break;
            case TaskAssignmentType.CommunityRole when assignment.CommunityRoleId is null:
                errors.Add($"User task '{node.Key}' needs a community role.");
                break;
            case TaskAssignmentType.TeamAndCommunityRole when assignment.TeamId is null || assignment.CommunityRoleId is null:
                errors.Add($"User task '{node.Key}' needs both a team and a community role.");
                break;
        }
    }

    private static void ValidateUserTaskEdges(ProcessNodeDto node, IReadOnlyList<ProcessEdgeDto> outgoing, List<string> errors)
    {
        var actions = node.Actions ?? [];
        foreach (var action in actions)
        {
            var matches = outgoing.Count(edge => edge.Action == action);
            if (matches != 1)
            {
                errors.Add($"User task '{node.Key}' must have exactly one outgoing edge for action {action}.");
            }
        }

        if (outgoing.Any(edge => edge.Action is null || !actions.Contains(edge.Action.Value)))
        {
            errors.Add($"Every outgoing edge from user task '{node.Key}' must use one of its actions.");
        }

        if (outgoing.Any(edge => edge.Condition is not null || edge.IsDefault))
        {
            errors.Add($"Outgoing action edges from user task '{node.Key}' cannot be conditional or default edges.");
        }
    }

    private static void ValidateGatewayEdges(ProcessNodeDto node, IReadOnlyList<ProcessEdgeDto> outgoing, List<string> errors)
    {
        if (outgoing.Count < 2)
        {
            errors.Add($"Exclusive gateway '{node.Key}' needs at least two outgoing edges.");
        }

        if (outgoing.Count(edge => edge.IsDefault) != 1)
        {
            errors.Add($"Exclusive gateway '{node.Key}' needs exactly one default edge.");
        }

        if (outgoing.Any(edge => !edge.IsDefault && edge.Condition is null))
        {
            errors.Add($"Every non-default edge from gateway '{node.Key}' needs a condition.");
        }

        if (outgoing.Any(edge => edge.IsDefault && edge.Condition is not null))
        {
            errors.Add($"Default edge from gateway '{node.Key}' cannot have a condition.");
        }

        if (outgoing.Any(edge => edge.Action is not null))
        {
            errors.Add($"Exclusive gateway '{node.Key}' edges cannot contain task actions.");
        }

        foreach (var condition in outgoing.Where(edge => !edge.IsDefault).Select(edge => edge.Condition!))
        {
            ValidateConditionShape(node.Key, condition, errors);
        }
    }

    private static void ValidateConditionShape(string gatewayKey, ProcessConditionDto condition, List<string> errors)
    {
        var path = condition.Path?.Trim() ?? string.Empty;
        if (!(path.StartsWith("start.", StringComparison.Ordinal)
            || path.StartsWith("steps.", StringComparison.Ordinal)))
        {
            errors.Add($"Gateway '{gatewayKey}' condition path '{condition.Path}' must use start.* or steps.<node>.*.");
        }

        if (condition.Operator is not (GraphConditionOperator.IsEmpty or GraphConditionOperator.IsNotEmpty)
            && condition.Value is null)
        {
            errors.Add($"Gateway '{gatewayKey}' condition '{condition.Path}' needs a typed value.");
        }

        if (condition.Value is { } value
            && value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            errors.Add($"Gateway '{gatewayKey}' condition '{condition.Path}' value must be a string, number, boolean or null.");
        }
    }

    private static void ValidateReachability(
        ProcessNodeDto start,
        IReadOnlyList<ProcessNodeDto> nodes,
        IReadOnlyList<ProcessEdgeDto> edges,
        List<string> errors)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal) { start.Key };
        var queue = new Queue<string>();
        queue.Enqueue(start.Key);

        while (queue.TryDequeue(out var current))
        {
            foreach (var target in edges.Where(edge => edge.Source == current).Select(edge => edge.Target))
            {
                if (reachable.Add(target))
                {
                    queue.Enqueue(target);
                }
            }
        }

        foreach (var node in nodes.Where(node => node.Type != ProcessNodeType.TeamSwimlane && !reachable.Contains(node.Key)))
        {
            errors.Add($"Runtime node '{node.Key}' is not reachable from Start.");
        }
    }

    private static void ValidateCyclesAndSendBack(
        IReadOnlyList<ProcessNodeDto> nodes,
        IReadOnlyList<ProcessEdgeDto> edges,
        IReadOnlyDictionary<string, ProcessNodeDto> nodeByKey,
        List<string> errors)
    {
        var forwardEdges = edges
            .Where(edge => edge.Action != WorkflowAction.SendBack
                && nodeByKey.ContainsKey(edge.Source ?? string.Empty)
                && nodeByKey.ContainsKey(edge.Target ?? string.Empty))
            .ToArray();
        var adjacency = forwardEdges
            .GroupBy(edge => edge.Source, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.Target).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        bool HasCycle(string nodeKey)
        {
            if (visiting.Contains(nodeKey)) return true;
            if (!visited.Add(nodeKey)) return false;

            visiting.Add(nodeKey);
            if (adjacency.TryGetValue(nodeKey, out var targets)
                && targets.Any(HasCycle))
            {
                return true;
            }
            visiting.Remove(nodeKey);
            return false;
        }

        if (nodes
            .Where(node => node.Type != ProcessNodeType.TeamSwimlane)
            .Select(node => node.Key)
            .Any(HasCycle))
        {
            errors.Add("Automatic workflow edges cannot contain cycles; use an explicit SendBack action to return to an earlier user task.");
        }

        foreach (var edge in edges.Where(edge => edge.Action == WorkflowAction.SendBack))
        {
            if (!nodeByKey.TryGetValue(edge.Source ?? string.Empty, out var source)
                || !nodeByKey.TryGetValue(edge.Target ?? string.Empty, out var target))
            {
                continue;
            }

            if (source.Type != ProcessNodeType.UserTask || target.Type != ProcessNodeType.UserTask)
            {
                errors.Add($"SendBack edge from '{edge.Source}' must target an earlier UserTask.");
                continue;
            }

            if (!HasPath(edge.Target!, edge.Source!, adjacency))
            {
                errors.Add($"SendBack edge from '{edge.Source}' must target a UserTask that occurs earlier in the forward flow.");
            }
        }
    }

    private static bool HasPath(
        string source,
        string target,
        IReadOnlyDictionary<string, string[]> adjacency)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(source);
        while (queue.TryDequeue(out var current))
        {
            if (!visited.Add(current)) continue;
            if (string.Equals(current, target, StringComparison.Ordinal)) return true;
            if (!adjacency.TryGetValue(current, out var targets)) continue;
            foreach (var next in targets) queue.Enqueue(next);
        }
        return false;
    }

    private async Task ValidateAssignmentReferencesAsync(
        ProcessNodeDto node,
        Guid communityId,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var assignment = node.Assignment!;

        if (assignment.UserId is { } userId
            && !await db.Users.AnyAsync(user =>
                user.Id == userId
                && user.Status == UserStatus.Active
                && user.CommunityMemberships.Any(membership => membership.IsActive && membership.CommunityId == communityId),
                cancellationToken))
        {
            errors.Add($"Assignment user '{userId}' for task '{node.Key}' is not active in the process community.");
        }

        if (assignment.TeamId is { } teamId
            && !await db.Teams.AnyAsync(team => team.Id == teamId && team.CommunityId == communityId && team.IsActive, cancellationToken))
        {
            errors.Add($"Assignment team '{teamId}' for task '{node.Key}' is not active in the process community.");
        }

        if (node.RequiresTeamLead
            && assignment.TeamId is { } requiredLeadTeamId
            && !await db.Users.AnyAsync(user =>
                user.Status == UserStatus.Active
                && user.TeamMemberships.Any(membership =>
                    membership.TeamId == requiredLeadTeamId
                    && membership.IsActive
                    && membership.IsLead
                    && membership.Team != null
                    && membership.Team.IsActive
                    && membership.Team.CommunityId == communityId)
                && user.CommunityMemberships.Any(membership =>
                    membership.IsActive
                    && membership.CommunityId == communityId
                    && (assignment.Type != TaskAssignmentType.TeamAndCommunityRole
                        || membership.CommunityRoleId == assignment.CommunityRoleId)
                    && membership.CommunityRole != null
                    && membership.CommunityRole.Permissions.Any(permission => permission.Permission == PermissionNames.TasksAct)),
                cancellationToken))
        {
            errors.Add($"Assignment team '{requiredLeadTeamId}' for task '{node.Key}' needs an active team lead with Tasks.Act permission.");
        }

        if (assignment.CommunityRoleId is { } roleId)
        {
            var role = await db.CommunityRoles
                .Include(item => item.Permissions)
                .SingleOrDefaultAsync(item => item.Id == roleId && item.CommunityId == communityId, cancellationToken);
            if (role is null)
            {
                errors.Add($"Assignment role '{roleId}' for task '{node.Key}' is outside the process community.");
            }
            else if (!role.Permissions.Any(permission => permission.Permission == PermissionNames.TasksAct))
            {
                errors.Add($"Assignment role '{roleId}' for task '{node.Key}' does not grant {PermissionNames.TasksAct}.");
            }
        }
    }

    private async Task ValidateConditionReferencesAsync(
        ProcessGraphDto graph,
        Guid startFormVersionId,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var nodes = graph.Nodes ?? [];
        var taskFormVersions = nodes
            .Where(node => node.Type == ProcessNodeType.UserTask && node.FormDefinitionVersionId.HasValue)
            .ToDictionary(node => node.Key, node => node.FormDefinitionVersionId!.Value, StringComparer.Ordinal);
        var referencedVersionIds = taskFormVersions.Values.Append(startFormVersionId).Distinct().ToArray();
        var versions = await db.FormDefinitionVersions
            .Where(version => referencedVersionIds.Contains(version.Id))
            .Include(version => version.Pages)
            .ThenInclude(page => page.Fields)
            .AsSplitQuery()
            .ToDictionaryAsync(version => version.Id, cancellationToken);

        var forwardAdjacency = (graph.Edges ?? [])
            .Where(edge => edge.Action != WorkflowAction.SendBack)
            .GroupBy(edge => edge.Source, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.Target).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        foreach (var edge in (graph.Edges ?? []).Where(edge => edge.Condition is not null))
        {
            var condition = edge.Condition!;
            var segments = (condition.Path ?? string.Empty)
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Guid formVersionId;
            string fieldKey;

            if (segments.Length == 2 && segments[0] == "start")
            {
                formVersionId = startFormVersionId;
                fieldKey = segments[1];
            }
            else if (segments.Length == 3
                && segments[0] == "steps"
                && taskFormVersions.TryGetValue(segments[1], out formVersionId))
            {
                fieldKey = segments[2];
                if (!HasPath(segments[1], edge.Source, forwardAdjacency))
                {
                    errors.Add($"Condition path '{condition.Path}' cannot read a task form that does not execute before gateway '{edge.Source}'.");
                }
            }
            else
            {
                errors.Add($"Condition path '{condition.Path}' does not reference a bound start or task form field.");
                continue;
            }

            if (!versions.TryGetValue(formVersionId, out var version))
            {
                errors.Add($"Condition path '{condition.Path}' references an unknown form field.");
                continue;
            }

            var field = version.Pages
                .SelectMany(page => page.Fields)
                .FirstOrDefault(field => string.Equals(field.Key, fieldKey, StringComparison.Ordinal));
            if (field is null)
            {
                errors.Add($"Condition path '{condition.Path}' references an unknown form field.");
                continue;
            }

            ValidateConditionValueType(condition, field.Type, errors);
        }
    }

    private static void ValidateConditionValueType(
        ProcessConditionDto condition,
        FieldType fieldType,
        List<string> errors)
    {
        if (condition.Operator is GraphConditionOperator.IsEmpty or GraphConditionOperator.IsNotEmpty)
        {
            return;
        }

        if (condition.Value is not { } value)
        {
            return;
        }

        var typeMatches = fieldType switch
        {
            FieldType.Number => value.ValueKind == JsonValueKind.Number,
            FieldType.Checkbox => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => value.ValueKind == JsonValueKind.String
        };
        if (!typeMatches)
        {
            errors.Add($"Condition path '{condition.Path}' value type does not match form field type '{fieldType}'.");
        }

        if (condition.Operator is GraphConditionOperator.GreaterThan
            or GraphConditionOperator.GreaterThanOrEquals
            or GraphConditionOperator.LessThan
            or GraphConditionOperator.LessThanOrEquals
            && fieldType != FieldType.Number)
        {
            errors.Add($"Condition path '{condition.Path}' can use numeric comparison only with a Number field.");
        }

        if (condition.Operator == GraphConditionOperator.Contains
            && fieldType is FieldType.Number or FieldType.Checkbox)
        {
            errors.Add($"Condition path '{condition.Path}' cannot use Contains with field type '{fieldType}'.");
        }
    }

    private Task<bool> IsPublishedFormVersionInCommunityAsync(
        Guid versionId,
        Guid communityId,
        CancellationToken cancellationToken) =>
        db.FormDefinitionVersions.AnyAsync(version =>
            version.Id == versionId
            && version.Status == DefinitionVersionStatus.Published
            && version.FormDefinition != null
            && version.FormDefinition.CommunityId == communityId,
            cancellationToken);

    private static bool IsEnd(ProcessNodeDto node) =>
        node.Type is ProcessNodeType.CompletedEnd or ProcessNodeType.RejectedEnd;
}
