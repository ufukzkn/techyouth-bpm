using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

internal sealed class DynamicWorkflowEngine(AppDbContext db)
{
    private const int AutomaticHopLimit = 100;

    public async Task<Result> StartAsync(
        ProcessInstance process,
        ProcessGraphDto graph,
        CancellationToken cancellationToken)
    {
        var start = (graph.Nodes ?? []).SingleOrDefault(node => node.Type == ProcessNodeType.Start);
        if (start is null)
        {
            return Result.Failure("Published process graph has no Start node.");
        }

        var now = DateTime.UtcNow;
        var startStep = new ProcessStepExecution
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = process.Id,
            NodeKey = start.Key,
            NodeTitle = start.Title,
            NodeType = start.Type,
            Attempt = NextAttempt(process, start.Key),
            Status = ProcessStepStatus.Completed,
            EnteredAt = now,
            CompletedAt = now,
            OutputJson = process.FormDataJson
        };
        process.StepExecutions.Add(startStep);
        db.ProcessStepExecutions.Add(startStep);

        var edge = (graph.Edges ?? []).SingleOrDefault(item => item.Source == start.Key);
        return edge is null
            ? Result.Failure("Published process graph Start node has no outgoing edge.")
            : await RouteAsync(process, graph, edge.Target, cancellationToken);
    }

    public async Task<Result> ContinueAsync(
        ProcessInstance process,
        ProcessTask completedTask,
        WorkflowAction action,
        ProcessGraphDto graph,
        CancellationToken cancellationToken)
    {
        var edge = (graph.Edges ?? [])
            .Where(item => item.Source == completedTask.NodeKey && item.Action == action)
            .OrderBy(item => item.Order)
            .FirstOrDefault();
        return edge is null
            ? Result.Failure($"Action {action} has no route from node '{completedTask.NodeKey}'.")
            : await RouteAsync(process, graph, edge.Target, cancellationToken);
    }

    private async Task<Result> RouteAsync(
        ProcessInstance process,
        ProcessGraphDto graph,
        string targetKey,
        CancellationToken cancellationToken)
    {
        var nodes = (graph.Nodes ?? []).ToDictionary(node => node.Key, StringComparer.Ordinal);
        var currentKey = targetKey;

        for (var hop = 0; hop < AutomaticHopLimit; hop++)
        {
            if (!nodes.TryGetValue(currentKey, out var node))
            {
                return Result.Failure($"Route target '{currentKey}' was not found in the published graph.");
            }

            process.CurrentNodeKey = node.Key;
            switch (node.Type)
            {
                case ProcessNodeType.UserTask:
                    return await CreateUserTaskAsync(process, node, cancellationToken);
                case ProcessNodeType.ExclusiveGateway:
                {
                    CompleteAutomaticStep(process, node);
                    var edge = SelectGatewayEdge(process.VariablesJson, node.Key, graph.Edges ?? []);
                    if (edge is null)
                    {
                        return Result.Failure($"Exclusive gateway '{node.Key}' has no matching or default route.");
                    }

                    currentKey = edge.Target;
                    break;
                }
                case ProcessNodeType.CompletedEnd:
                    CompleteAutomaticStep(process, node);
                    process.Status = ProcessStatus.Completed;
                    process.CompletedAt = DateTime.UtcNow;
                    return Result.Success();
                case ProcessNodeType.RejectedEnd:
                    CompleteAutomaticStep(process, node);
                    process.Status = ProcessStatus.Rejected;
                    process.CompletedAt = DateTime.UtcNow;
                    return Result.Success();
                case ProcessNodeType.Start:
                    return Result.Failure("A runtime route cannot return to Start.");
                case ProcessNodeType.TeamSwimlane:
                    return Result.Failure("A runtime route cannot execute a TeamSwimlane.");
                default:
                    return Result.Failure($"Unsupported process node type '{node.Type}'.");
            }
        }

        return Result.Failure($"Automatic routing exceeded the {AutomaticHopLimit}-hop safety limit.");
    }

    private async Task<Result> CreateUserTaskAsync(
        ProcessInstance process,
        ProcessNodeDto node,
        CancellationToken cancellationToken)
    {
        var assignment = node.Assignment!;
        if (node.RequiresTeamLead
            && assignment.Type is not (TaskAssignmentType.Team or TaskAssignmentType.TeamAndCommunityRole))
        {
            return Result.Failure($"User task '{node.Key}' can require a team lead only for team assignments.");
        }

        var resolver = new TaskAssignmentResolver(db);
        IReadOnlyList<Guid> candidateIds;
        if (assignment.Type == TaskAssignmentType.ProcessStarter)
        {
            candidateIds = await db.Users
                .Where(user => user.Id == process.StartedByUserId
                    && user.Status == UserStatus.Active
                    && (user.Role == Role.SuperAdmin
                        || user.CommunityMemberships.Any(membership =>
                            membership.IsActive
                            && membership.CommunityId == process.CommunityId
                            && membership.Community != null
                            && membership.Community.IsActive)))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);
        }
        else
        {
            candidateIds = await resolver.ResolveCandidateUserIdsAsync(
                process.CommunityId,
                assignment,
                cancellationToken,
                node.RequiresTeamLead);
        }

        if (candidateIds.Count == 0)
        {
            return Result.Failure($"User task '{node.Key}' has no eligible candidate.");
        }

        var now = DateTime.UtcNow;
        var attempt = NextAttempt(process, node.Key);
        var assignedUserId = assignment.Type switch
        {
            TaskAssignmentType.ProcessStarter => process.StartedByUserId,
            TaskAssignmentType.SpecificUser => assignment.UserId,
            _ => null
        };
        var teamName = assignment.TeamId is { } teamId
            ? await db.Teams
                .AsNoTracking()
                .Where(team => team.Id == teamId)
                .Select(team => team.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? string.Empty
            : string.Empty;
        var communityRoleName = assignment.CommunityRoleId is { } communityRoleId
            ? await db.CommunityRoles
                .AsNoTracking()
                .Where(role => role.Id == communityRoleId)
                .Select(role => role.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? string.Empty
            : string.Empty;
        var assignedUserName = assignedUserId is { } userId
            ? await db.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.DisplayName)
                .SingleOrDefaultAsync(cancellationToken) ?? string.Empty
            : string.Empty;
        var task = new ProcessTask
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = process.Id,
            NodeKey = node.Key,
            Attempt = attempt,
            Title = node.Title.Trim(),
            Priority = node.Priority,
            AssignmentType = assignment.Type,
            AssignedUserId = assignedUserId,
            CandidateTeamId = assignment.TeamId,
            CandidateCommunityRoleId = assignment.CommunityRoleId,
            AssignedCommunityRoleId = assignment.CommunityRoleId,
            AssignedRole = Role.User,
            RequiredPermission = PermissionNames.TasksAct,
            Status = ProcessTaskStatus.Open,
            AvailableActionsJson = JsonHelpers.Serialize(node.Actions ?? []),
            FormDefinitionVersionId = node.FormDefinitionVersionId,
            ClaimVersion = Guid.NewGuid(),
            CreatedAt = now,
            DueAt = node.SlaDurationMinutes is { } slaMinutes ? now.AddMinutes(slaMinutes) : null,
            RequiresTeamLead = node.RequiresTeamLead
        };
        var step = new ProcessStepExecution
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = process.Id,
            NodeKey = node.Key,
            NodeTitle = node.Title,
            NodeType = node.Type,
            AssignmentType = assignment.Type,
            TeamNameSnapshot = teamName,
            CommunityRoleNameSnapshot = communityRoleName,
            AssignedUserNameSnapshot = assignedUserName,
            Attempt = attempt,
            Status = ProcessStepStatus.Active,
            EnteredAt = now
        };
        process.Tasks.Add(task);
        process.StepExecutions.Add(step);
        db.ProcessTasks.Add(task);
        db.ProcessStepExecutions.Add(step);

        foreach (var candidateId in candidateIds.Distinct())
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                CommunityId = process.CommunityId,
                UserId = candidateId,
                Type = "Task.Assigned",
                Title = "Yeni görev atandı",
                Message = $"{node.Title} görevi aksiyonunuzu bekliyor.",
                EntityType = "ProcessTask",
                EntityId = task.Id.ToString(),
                CreatedAt = now
            });
        }

        return Result.Success();
    }

    private void CompleteAutomaticStep(ProcessInstance process, ProcessNodeDto node)
    {
        var now = DateTime.UtcNow;
        var step = new ProcessStepExecution
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = process.Id,
            NodeKey = node.Key,
            NodeTitle = node.Title,
            NodeType = node.Type,
            Attempt = NextAttempt(process, node.Key),
            Status = ProcessStepStatus.Completed,
            EnteredAt = now,
            CompletedAt = now
        };
        process.StepExecutions.Add(step);
        db.ProcessStepExecutions.Add(step);
    }

    private static ProcessEdgeDto? SelectGatewayEdge(
        string variablesJson,
        string gatewayKey,
        IReadOnlyList<ProcessEdgeDto> edges)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(variablesJson) ? "{}" : variablesJson);
        var outgoing = edges.Where(edge => edge.Source == gatewayKey).OrderBy(edge => edge.Order).ToArray();
        return outgoing.FirstOrDefault(edge =>
                   !edge.IsDefault
                   && edge.Condition is not null
                   && EvaluateCondition(document.RootElement, edge.Condition))
            ?? outgoing.FirstOrDefault(edge => edge.IsDefault);
    }

    private static bool EvaluateCondition(JsonElement variables, ProcessConditionDto condition)
    {
        var found = TryResolvePath(variables, condition.Path, out var actual);
        var isEmpty = !found || actual.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || (actual.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(actual.GetString()))
            || (actual.ValueKind == JsonValueKind.Array && actual.GetArrayLength() == 0);

        if (condition.Operator == GraphConditionOperator.IsEmpty)
        {
            return isEmpty;
        }

        if (condition.Operator == GraphConditionOperator.IsNotEmpty)
        {
            return !isEmpty;
        }

        if (!found || condition.Value is not { } expected)
        {
            return false;
        }

        return condition.Operator switch
        {
            GraphConditionOperator.Equals => AreEqual(actual, expected),
            GraphConditionOperator.NotEquals => !AreEqual(actual, expected),
            GraphConditionOperator.GreaterThan => Compare(actual, expected) is > 0,
            GraphConditionOperator.GreaterThanOrEquals => Compare(actual, expected) is >= 0,
            GraphConditionOperator.LessThan => Compare(actual, expected) is < 0,
            GraphConditionOperator.LessThanOrEquals => Compare(actual, expected) is <= 0,
            GraphConditionOperator.Contains => Contains(actual, expected),
            _ => false
        };
    }

    private static bool TryResolvePath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var segment in (path ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                value = default;
                return false;
            }
        }

        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool AreEqual(JsonElement left, JsonElement right)
    {
        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber))
        {
            return leftNumber == rightNumber;
        }

        if (left.ValueKind is JsonValueKind.True or JsonValueKind.False
            && right.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return left.GetBoolean() == right.GetBoolean();
        }

        return string.Equals(ToText(left), ToText(right), StringComparison.Ordinal);
    }

    private static int? Compare(JsonElement left, JsonElement right)
    {
        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return null;
    }

    private static bool Contains(JsonElement actual, JsonElement expected) =>
        actual.ValueKind switch
        {
            JsonValueKind.String => (actual.GetString() ?? string.Empty).Contains(ToText(expected), StringComparison.Ordinal),
            JsonValueKind.Array => actual.EnumerateArray().Any(item => AreEqual(item, expected)),
            _ => false
        };

    private static bool TryDecimal(JsonElement value, out decimal number)
    {
        number = default;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out number);
    }

    private static string ToText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText()
    };

    private static int NextAttempt(ProcessInstance process, string nodeKey) =>
        process.StepExecutions
            .Where(step => step.NodeKey == nodeKey)
            .Select(step => step.Attempt)
            .DefaultIfEmpty(0)
            .Max() + 1;
}
