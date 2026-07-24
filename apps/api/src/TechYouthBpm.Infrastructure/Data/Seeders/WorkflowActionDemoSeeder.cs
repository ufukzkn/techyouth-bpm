using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;
using static TechYouthBpm.Infrastructure.Data.Seeders.DemoSeedIds;

namespace TechYouthBpm.Infrastructure.Data.Seeders;

/// <summary>
/// Seeds two deterministic action-lab workflows. Each workflow demonstrates every task action
/// and all supported assignment targets without changing user-created definitions or instances.
/// </summary>
internal static class WorkflowActionDemoSeeder
{
    private static readonly Guid TransferDefinitionId = StableGuid("action-lab:transfer:definition");
    private static readonly Guid TransferVersionId = StableGuid("action-lab:transfer:version:1");
    private static readonly Guid OperationsDefinitionId = StableGuid("action-lab:operations:definition");
    private static readonly Guid OperationsVersionId = StableGuid("action-lab:operations:version:1");
    private static readonly Guid TransferPoolTeamId = StableGuid("action-lab:transfer:pool-team");
    private static readonly Guid TransferRoleTeamId = StableGuid("action-lab:transfer:role-team");
    private static readonly Guid TransferLeadTeamId = StableGuid("action-lab:transfer:lead-team");
    private static readonly Guid OperationsPoolTeamId = StableGuid("action-lab:operations:pool-team");
    private static readonly Guid OperationsRoleTeamId = StableGuid("action-lab:operations:role-team");
    private static readonly Guid OperationsLeadTeamId = StableGuid("action-lab:operations:lead-team");

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var approverRoleId = await db.CommunityRoles
            .Where(role => role.CommunityId == SportCommunityId && role.TemplateKey == "approver" && role.IsSystemRole)
            .Select(role => role.Id)
            .SingleAsync(cancellationToken);

        await EnsureDemoTeamsAsync(db, cancellationToken);

        var workflows = new[]
        {
            new WorkflowSpec(
                "transfer-action-lab",
                TransferDefinitionId,
                TransferVersionId,
                "Transfer Aksiyon Laboratuvarı",
                "Onay, tamamlama, geri gönderme, ret ve yükseltme davranışlarını transfer ekipleriyle gösterir.",
                SportQuickTechnicalStartFormId,
                SportQuickTechnicalStartFormVersionId,
                new TaskSpec("teamPool", "Scout Havuzu İncelemesi", SportQuickScoutTaskFormVersionId, TaskAssignmentType.Team, TransferPoolTeamId, null, null, SportDemoScoutId, false),
                new TaskSpec("teamRole", "Teknik Rol Onayı", SportQuickTechnicalTaskFormVersionId, TaskAssignmentType.TeamAndCommunityRole, TransferRoleTeamId, approverRoleId, null, SportDemoApproverId, false),
                new TaskSpec("specificUser", "Kişiye Özel Operasyon", SportQuickScoutTaskFormVersionId, TaskAssignmentType.SpecificUser, null, null, SportDemoOperationsId, SportDemoOperationsId, false),
                new TaskSpec("teamLead", "Mali Sorumlu Kararı", SportQuickLeadTaskFormVersionId, TaskAssignmentType.Team, TransferLeadTeamId, null, null, SportDemoFinanceId, true)),
            new WorkflowSpec(
                "operations-action-lab",
                OperationsDefinitionId,
                OperationsVersionId,
                "Operasyon Aksiyon Laboratuvarı",
                "Aynı aksiyonları farklı takım ve sorumlu kombinasyonlarıyla gösterir.",
                SportQuickScoutStartFormId,
                SportQuickScoutStartFormVersionId,
                new TaskSpec("teamPool", "Transfer Operasyon Havuzu", SportQuickScoutTaskFormVersionId, TaskAssignmentType.Team, OperationsPoolTeamId, null, null, SportDemoOperationsId, false),
                new TaskSpec("teamRole", "Scout Rol Değerlendirmesi", SportQuickTechnicalTaskFormVersionId, TaskAssignmentType.TeamAndCommunityRole, OperationsRoleTeamId, approverRoleId, null, SportDemoScoutId, false),
                new TaskSpec("specificUser", "Teknik Uzman İşlemi", SportQuickScoutTaskFormVersionId, TaskAssignmentType.SpecificUser, null, null, SportDemoApproverId, SportDemoApproverId, false),
                new TaskSpec("teamLead", "Teknik Sorumlu Kararı", SportQuickLeadTaskFormVersionId, TaskAssignmentType.Team, OperationsLeadTeamId, null, null, SportDemoApproverId, true))
        };

        var versions = await EnsureDefinitionsAsync(db, workflows, cancellationToken);
        await EnsureInstancesAsync(db, workflows, versions, cancellationToken);
    }

    private static async Task EnsureDemoTeamsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var teams = new[]
        {
            (TransferPoolTeamId, "Transfer Demo Havuzu", "Transfer aksiyon laboratuvarı takım havuzu."),
            (TransferRoleTeamId, "Transfer Demo Rol Ekibi", "Transfer aksiyon laboratuvarı takım ve rol adımı."),
            (TransferLeadTeamId, "Transfer Demo Sorumluları", "Transfer aksiyon laboratuvarı lider adımı."),
            (OperationsPoolTeamId, "Operasyon Demo Havuzu", "Operasyon aksiyon laboratuvarı takım havuzu."),
            (OperationsRoleTeamId, "Operasyon Demo Rol Ekibi", "Operasyon aksiyon laboratuvarı takım ve rol adımı."),
            (OperationsLeadTeamId, "Operasyon Demo Sorumluları", "Operasyon aksiyon laboratuvarı lider adımı.")
        };
        var existingTeamIds = (await db.Teams
                .Where(team => teams.Select(candidate => candidate.Item1).Contains(team.Id))
                .Select(team => team.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var createdAt = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        foreach (var team in teams.Where(team => !existingTeamIds.Contains(team.Item1)))
        {
            db.Teams.Add(new Team
            {
                Id = team.Item1,
                CommunityId = SportCommunityId,
                Name = team.Item2,
                NormalizedName = team.Item2.ToUpperInvariant(),
                Description = team.Item3,
                IsActive = true,
                CreatedByUserId = SportDemoAdminId,
                CreatedAt = createdAt
            });
        }

        var memberships = new[]
        {
            (TransferPoolTeamId, SportDemoScoutId, false),
            (TransferRoleTeamId, SportDemoApproverId, false),
            (TransferLeadTeamId, SportDemoFinanceId, true),
            (OperationsPoolTeamId, SportDemoOperationsId, false),
            (OperationsRoleTeamId, SportDemoScoutId, false),
            (OperationsLeadTeamId, SportDemoApproverId, true)
        };
        var existingMembershipIds = (await db.TeamMemberships
                .Where(membership => memberships.Select(candidate => candidate.Item1).Contains(membership.TeamId))
                .Select(membership => membership.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        foreach (var membership in memberships)
        {
            var membershipId = StableGuid($"action-lab:team-membership:{membership.Item1}:{membership.Item2}");
            if (existingMembershipIds.Contains(membershipId))
            {
                continue;
            }

            db.TeamMemberships.Add(new TeamMembership
            {
                Id = membershipId,
                TeamId = membership.Item1,
                UserId = membership.Item2,
                IsLead = membership.Item3,
                IsActive = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyDictionary<string, ProcessDefinitionVersion>> EnsureDefinitionsAsync(
        AppDbContext db,
        IReadOnlyCollection<WorkflowSpec> workflows,
        CancellationToken cancellationToken)
    {
        var definitionIds = workflows.Select(workflow => workflow.DefinitionId).ToArray();
        var definitions = await db.ProcessDefinitions
            .Where(definition => definitionIds.Contains(definition.Id))
            .Include(definition => definition.Versions)
            .ToDictionaryAsync(definition => definition.Id, cancellationToken);
        var now = new DateTime(2026, 1, 6, 9, 0, 0, DateTimeKind.Utc);

        foreach (var workflow in workflows)
        {
            if (!definitions.TryGetValue(workflow.DefinitionId, out var definition))
            {
                definition = new ProcessDefinition
                {
                    Id = workflow.DefinitionId,
                    Name = workflow.Name,
                    Description = workflow.Description,
                    CommunityId = SportCommunityId,
                    CreatedByUserId = SportDemoAdminId,
                    CreatedAt = now.AddDays(-7)
                };
                definitions.Add(definition.Id, definition);
                db.ProcessDefinitions.Add(definition);
            }

            if (definition.Versions.Any(version => version.Id == workflow.VersionId))
            {
                continue;
            }

            db.ProcessDefinitionVersions.Add(new ProcessDefinitionVersion
            {
                Id = workflow.VersionId,
                ProcessDefinitionId = definition.Id,
                ProcessDefinition = definition,
                VersionNumber = definition.Versions.Count == 0 ? 1 : definition.Versions.Max(version => version.VersionNumber) + 1,
                Status = DefinitionVersionStatus.Published,
                FormDefinitionVersionId = workflow.StartFormVersionId,
                GraphJson = JsonHelpers.Serialize(BuildGraph(workflow)),
                CreatedByUserId = SportDemoAdminId,
                CreatedAt = now.AddDays(-7),
                PublishedByUserId = SportDemoAdminId,
                PublishedAt = now.AddDays(-7)
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var versionIds = workflows.Select(workflow => workflow.VersionId).ToArray();
        return await db.ProcessDefinitionVersions
            .Where(version => versionIds.Contains(version.Id))
            .ToDictionaryAsync(
                version => workflows.Single(workflow => workflow.VersionId == version.Id).Key,
                cancellationToken);
    }

    private static ProcessGraphDto BuildGraph(WorkflowSpec workflow)
    {
        var poolLane = $"{workflow.Pool.TeamId}:pool-lane";
        var roleLane = $"{workflow.Role.TeamId}:role-lane";
        var personLane = $"{workflow.Key}:person-lane";
        var leadLane = $"{workflow.Lead.TeamId}:lead-lane";

        return new ProcessGraphDto(
            "1.0",
            [
                Lane(poolLane, workflow.Pool.TeamId!.Value, TeamName(workflow.Pool.TeamId), 20),
                Lane(roleLane, workflow.Role.TeamId!.Value, TeamName(workflow.Role.TeamId), 280),
                Lane(personLane, SportTransferTeamId, "Kişiye Özel İşler", 540),
                Lane(leadLane, workflow.Lead.TeamId!.Value, TeamName(workflow.Lead.TeamId), 800),
                new ProcessNodeDto("start", ProcessNodeType.Start, "Aksiyon Demo Talebi", workflow.StartFormVersionId, ParentKey: poolLane, PositionX: 45, PositionY: 72, Width: 180, Height: 72),
                TaskNode(workflow.Pool, poolLane, [WorkflowAction.Complete, WorkflowAction.Reject], 285),
                TaskNode(workflow.Role, roleLane, [WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.SendBack], 285),
                TaskNode(workflow.Person, personLane, [WorkflowAction.Complete, WorkflowAction.Escalate, WorkflowAction.SendBack], 285),
                TaskNode(workflow.Lead, leadLane, [WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.SendBack], 285),
                new ProcessNodeDto("completed", ProcessNodeType.CompletedEnd, "Süreç Tamamlandı", ParentKey: leadLane, PositionX: 760, PositionY: 45, Width: 190, Height: 72),
                new ProcessNodeDto("rejected", ProcessNodeType.RejectedEnd, "Süreç Reddedildi", ParentKey: leadLane, PositionX: 760, PositionY: 130, Width: 190, Height: 72)
            ],
            [
                new ProcessEdgeDto("start", workflow.Pool.Key, Order: 0, Label: "Talebi gönder"),
                new ProcessEdgeDto(workflow.Pool.Key, workflow.Role.Key, WorkflowAction.Complete, Order: 1, Label: "İncelemeyi tamamla"),
                new ProcessEdgeDto(workflow.Pool.Key, "rejected", WorkflowAction.Reject, Order: 2, Label: "Havuz reddi"),
                new ProcessEdgeDto(workflow.Role.Key, workflow.Person.Key, WorkflowAction.Approve, Order: 3, Label: "Rol onayı"),
                new ProcessEdgeDto(workflow.Role.Key, "rejected", WorkflowAction.Reject, Order: 4, Label: "Rol reddi"),
                new ProcessEdgeDto(workflow.Role.Key, workflow.Pool.Key, WorkflowAction.SendBack, Order: 5, Label: "Havuza geri gönder"),
                new ProcessEdgeDto(workflow.Person.Key, workflow.Lead.Key, WorkflowAction.Complete, Order: 6, Label: "İşi tamamla"),
                new ProcessEdgeDto(workflow.Person.Key, workflow.Lead.Key, WorkflowAction.Escalate, Order: 7, Label: "Sorumluya yükselt"),
                new ProcessEdgeDto(workflow.Person.Key, workflow.Role.Key, WorkflowAction.SendBack, Order: 8, Label: "Rol adımına geri gönder"),
                new ProcessEdgeDto(workflow.Lead.Key, "completed", WorkflowAction.Approve, Order: 9, Label: "Nihai onay"),
                new ProcessEdgeDto(workflow.Lead.Key, "rejected", WorkflowAction.Reject, Order: 10, Label: "Nihai ret"),
                new ProcessEdgeDto(workflow.Lead.Key, workflow.Person.Key, WorkflowAction.SendBack, Order: 11, Label: "Uzmana geri gönder")
            ]);
    }

    private static ProcessNodeDto Lane(string key, Guid teamId, string title, double y) =>
        new(key, ProcessNodeType.TeamSwimlane, title, PositionX: 40, PositionY: y, Width: 1080, Height: 220, TeamId: teamId);

    private static ProcessNodeDto TaskNode(
        TaskSpec task,
        string parentKey,
        IReadOnlyList<WorkflowAction> actions,
        double x) =>
        new(
            task.Key,
            ProcessNodeType.UserTask,
            task.Title,
            task.FormVersionId,
            task.RequiresTeamLead ? TaskPriority.Critical : TaskPriority.High,
            actions,
            new TaskAssignmentDto(task.AssignmentType, task.UserId, task.TeamId, task.RoleId),
            parentKey,
            x,
            58,
            250,
            104,
            $"{task.Title} için aksiyon demonstrasyonu.",
            SlaDurationMinutes: task.RequiresTeamLead ? 120 : 240,
            RequiresTeamLead: task.RequiresTeamLead);

    private static async Task EnsureInstancesAsync(
        AppDbContext db,
        IReadOnlyCollection<WorkflowSpec> workflows,
        IReadOnlyDictionary<string, ProcessDefinitionVersion> versions,
        CancellationToken cancellationToken)
    {
        var desiredIds = workflows
            .SelectMany(workflow => Enum.GetValues<ActionScenario>().Select(scenario => ProcessId(workflow.Key, scenario)))
            .ToArray();
        var existingIds = (await db.ProcessInstances
                .Where(process => desiredIds.Contains(process.Id))
                .Select(process => process.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var workflow in workflows)
        {
            foreach (var scenario in Enum.GetValues<ActionScenario>())
            {
                if (existingIds.Contains(ProcessId(workflow.Key, scenario)))
                {
                    continue;
                }

                var seeded = BuildScenario(workflow, versions[workflow.Key], scenario);
                db.ProcessInstances.Add(seeded.Process);
                db.Notifications.AddRange(seeded.Notifications);
                db.SystemAuditLogs.AddRange(seeded.SystemAudits);
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static SeededScenario BuildScenario(
        WorkflowSpec workflow,
        ProcessDefinitionVersion version,
        ActionScenario scenario)
    {
        var processId = ProcessId(workflow.Key, scenario);
        var startedAt = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc)
            .AddDays(workflow.Key == "transfer-action-lab" ? 0 : 1)
            .AddHours((int)scenario * 2);
        var completedSteps = CompletedSteps(workflow, scenario);
        var activeTask = ActiveTask(workflow, scenario);
        var terminalStatus = scenario switch
        {
            ActionScenario.Approve => ProcessStatus.Completed,
            ActionScenario.Reject => ProcessStatus.Rejected,
            _ => ProcessStatus.InProgress
        };
        var taskEntities = new List<ProcessTask>();
        var executions = new List<ProcessStepExecution>
        {
            Step(processId, "start", "Aksiyon Demo Talebi", ProcessNodeType.Start, ProcessStepStatus.Completed, 1, startedAt, startedAt, SportDemoStarterId, WorkflowAction.Start, null)
        };
        var audits = new List<AuditLog>
        {
            Audit(processId, SportDemoStarterId, WorkflowAction.Start, startedAt, ProcessStatus.Pending, ProcessStatus.InProgress)
        };
        var systemAudits = new List<SystemAuditLog>
        {
            SystemAudit(processId, SportDemoStarterId, "Process.Seeded", startedAt, $"{workflow.Name} / {scenario} aksiyon demosu oluşturuldu.")
        };
        var variables = new Dictionary<string, object?>();
        var cursor = startedAt.AddMinutes(5);

        foreach (var completed in completedSteps)
        {
            var output = new Dictionary<string, object?>
            {
                ["demoAksiyonu"] = completed.Action.ToString(),
                ["açıklama"] = $"{completed.Task.Title} adımında {completed.Action} uygulandı."
            };
            var completedAt = cursor.AddMinutes(20);
            var completedTask = Task(processId, workflow.Key, completed.Task, completed.Attempt, ProcessTaskStatus.Completed, cursor, completedAt, completed.Task.PerformerId, completed.Action);
            taskEntities.Add(completedTask);
            executions.Add(Step(processId, completed.Task.Key, completed.Task.Title, ProcessNodeType.UserTask, ProcessStepStatus.Completed, completed.Attempt, cursor, completedAt, completed.Task.PerformerId, completed.Action, completed.Task));
            var actionStatus = completed.Action == WorkflowAction.Reject
                ? ProcessStatus.Rejected
                : scenario == ActionScenario.Approve && completed.Task.Key == workflow.Lead.Key
                    ? ProcessStatus.Completed
                    : ProcessStatus.InProgress;
            audits.Add(Audit(processId, completed.Task.PerformerId, completed.Action, completedAt, ProcessStatus.InProgress, actionStatus));
            systemAudits.Add(SystemAudit(processId, completed.Task.PerformerId, $"Task.{completed.Action}", completedAt, $"{completed.Task.Title} adımında {completed.Action} uygulandı.", completedTask.Id));
            variables[completed.Task.Key] = output;
            cursor = completedAt.AddMinutes(5);
        }

        if (scenario == ActionScenario.SendBack)
        {
            variables.Clear();
        }

        var notifications = new List<Notification>();
        if (activeTask is not null)
        {
            var active = Task(processId, workflow.Key, activeTask.Value.Task, activeTask.Value.Attempt, ProcessTaskStatus.Open, cursor, null, null, null);
            taskEntities.Add(active);
            executions.Add(Step(processId, activeTask.Value.Task.Key, activeTask.Value.Task.Title, ProcessNodeType.UserTask, ProcessStepStatus.Active, activeTask.Value.Attempt, cursor, null, null, null, activeTask.Value.Task));
            notifications.Add(new Notification
            {
                Id = StableGuid($"action-lab:notification:{workflow.Key}:{scenario}"),
                UserId = activeTask.Value.Task.PerformerId,
                CommunityId = SportCommunityId,
                Type = "Task.Assigned",
                Title = "Yeni aksiyon demo görevi",
                Message = $"{activeTask.Value.Task.Title} görevi aksiyonunuzu bekliyor.",
                EntityType = "ProcessTask",
                EntityId = active.Id.ToString(),
                CreatedAt = cursor
            });
        }
        else
        {
            var endTitle = terminalStatus == ProcessStatus.Completed ? "Süreç Tamamlandı" : "Süreç Reddedildi";
            var endKey = terminalStatus == ProcessStatus.Completed ? "completed" : "rejected";
            executions.Add(Step(processId, endKey, endTitle, terminalStatus == ProcessStatus.Completed ? ProcessNodeType.CompletedEnd : ProcessNodeType.RejectedEnd, ProcessStepStatus.Completed, 1, cursor, cursor, completedSteps.Last().Task.PerformerId, null, null));
            notifications.Add(new Notification
            {
                Id = StableGuid($"action-lab:notification:{workflow.Key}:{scenario}"),
                UserId = SportDemoStarterId,
                CommunityId = SportCommunityId,
                Type = terminalStatus == ProcessStatus.Completed ? "Process.Completed" : "Process.Rejected",
                Title = terminalStatus == ProcessStatus.Completed ? "Aksiyon demo süreci tamamlandı" : "Aksiyon demo süreci reddedildi",
                Message = $"{workflow.Name} için {scenario} senaryosu sonuçlandı.",
                EntityType = "ProcessInstance",
                EntityId = processId.ToString(),
                CreatedAt = cursor
            });
        }

        var startData = new Dictionary<string, object?>
        {
            ["demo"] = true,
            ["workflow"] = workflow.Name,
            ["aksiyonSenaryosu"] = scenario.ToString()
        };
        var process = new ProcessInstance
        {
            Id = processId,
            FormDefinitionId = workflow.StartFormId,
            FormDefinitionVersionId = workflow.StartFormVersionId,
            ProcessDefinitionVersionId = version.Id,
            CommunityId = SportCommunityId,
            StartedByUserId = SportDemoStarterId,
            Status = terminalStatus,
            FormDataJson = JsonHelpers.Serialize(startData),
            VariablesJson = JsonHelpers.Serialize(new Dictionary<string, object?> { ["start"] = startData, ["steps"] = variables }),
            CurrentNodeKey = activeTask?.Task.Key ?? (terminalStatus == ProcessStatus.Completed ? "completed" : "rejected"),
            StartedAt = startedAt,
            CompletedAt = activeTask is null ? cursor : null,
            Tasks = taskEntities,
            StepExecutions = executions,
            AuditLogs = audits
        };

        return new SeededScenario(process, notifications, systemAudits);
    }

    private static IReadOnlyList<CompletedTask> CompletedSteps(WorkflowSpec workflow, ActionScenario scenario) =>
        scenario switch
        {
            ActionScenario.Complete => [new(workflow.Pool, WorkflowAction.Complete, 1)],
            ActionScenario.Approve =>
            [
                new(workflow.Pool, WorkflowAction.Complete, 1),
                new(workflow.Role, WorkflowAction.Approve, 1),
                new(workflow.Person, WorkflowAction.Complete, 1),
                new(workflow.Lead, WorkflowAction.Approve, 1)
            ],
            ActionScenario.SendBack =>
            [
                new(workflow.Pool, WorkflowAction.Complete, 1),
                new(workflow.Role, WorkflowAction.SendBack, 1)
            ],
            ActionScenario.Escalate =>
            [
                new(workflow.Pool, WorkflowAction.Complete, 1),
                new(workflow.Role, WorkflowAction.Approve, 1),
                new(workflow.Person, WorkflowAction.Escalate, 1)
            ],
            ActionScenario.Reject =>
            [
                new(workflow.Pool, WorkflowAction.Complete, 1),
                new(workflow.Role, WorkflowAction.Reject, 1)
            ],
            _ => []
        };

    private static (TaskSpec Task, int Attempt)? ActiveTask(WorkflowSpec workflow, ActionScenario scenario) =>
        scenario switch
        {
            ActionScenario.Complete => (workflow.Role, 1),
            ActionScenario.SendBack => (workflow.Pool, 2),
            ActionScenario.Escalate => (workflow.Lead, 1),
            _ => null
        };

    private static ProcessTask Task(
        Guid processId,
        string workflowKey,
        TaskSpec spec,
        int attempt,
        ProcessTaskStatus status,
        DateTime createdAt,
        DateTime? completedAt,
        Guid? completedBy,
        WorkflowAction? action) =>
        new()
        {
            Id = StableGuid($"action-lab:task:{workflowKey}:{processId}:{spec.Key}:{attempt}"),
            ProcessInstanceId = processId,
            AssignedRole = Role.User,
            AssignedCommunityRoleId = spec.RoleId,
            NodeKey = spec.Key,
            Attempt = attempt,
            Title = spec.Title,
            Priority = spec.RequiresTeamLead ? TaskPriority.Critical : TaskPriority.High,
            AssignmentType = spec.AssignmentType,
            AssignedUserId = spec.UserId,
            CandidateTeamId = spec.TeamId,
            CandidateCommunityRoleId = spec.RoleId,
            ClaimVersion = StableGuid($"action-lab:claim:{workflowKey}:{processId}:{spec.Key}:{attempt}"),
            FormDefinitionVersionId = spec.FormVersionId,
            RequiredPermission = PermissionNames.TasksAct,
            Status = status,
            AvailableActionsJson = JsonHelpers.Serialize(ActionsFor(spec.Key)),
            CreatedAt = createdAt,
            DueAt = createdAt.AddYears(1).AddHours(spec.RequiresTeamLead ? 2 : 4),
            RequiresTeamLead = spec.RequiresTeamLead,
            CompletedAt = completedAt,
            CompletedByUserId = completedBy,
            CompletedAction = action,
            CompletionNote = action is null ? string.Empty : $"{action} aksiyonu demo verisi olarak uygulandı."
        };

    private static IReadOnlyList<WorkflowAction> ActionsFor(string nodeKey) => nodeKey switch
    {
        "teamPool" => [WorkflowAction.Complete, WorkflowAction.Reject],
        "teamRole" => [WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.SendBack],
        "specificUser" => [WorkflowAction.Complete, WorkflowAction.Escalate, WorkflowAction.SendBack],
        "teamLead" => [WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.SendBack],
        _ => []
    };

    private static ProcessStepExecution Step(
        Guid processId,
        string key,
        string title,
        ProcessNodeType type,
        ProcessStepStatus status,
        int attempt,
        DateTime enteredAt,
        DateTime? completedAt,
        Guid? completedBy,
        WorkflowAction? action,
        TaskSpec? task) =>
        new()
        {
            Id = StableGuid($"action-lab:step:{processId}:{key}:{attempt}"),
            ProcessInstanceId = processId,
            NodeKey = key,
            NodeTitle = title,
            NodeType = type,
            AssignmentType = task?.AssignmentType,
            TeamNameSnapshot = task is null ? string.Empty : TeamName(task.TeamId),
            CommunityRoleNameSnapshot = task?.RoleId is null ? string.Empty : "Onay Sorumlusu",
            AssignedUserNameSnapshot = task?.UserId is null ? string.Empty : UserName(task.UserId.Value),
            Attempt = attempt,
            Status = status,
            EnteredAt = enteredAt,
            CompletedAt = completedAt,
            CompletedByUserId = completedBy,
            Action = action,
            Note = action is null ? string.Empty : $"{action} aksiyonu uygulandı.",
            OutputJson = JsonHelpers.Serialize(action is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?> { ["action"] = action.ToString() })
        };

    private static AuditLog Audit(
        Guid processId,
        Guid userId,
        WorkflowAction action,
        DateTime at,
        ProcessStatus from,
        ProcessStatus to) =>
        new()
        {
            Id = StableGuid($"action-lab:audit:{processId}:{action}:{at.Ticks}"),
            ProcessInstanceId = processId,
            UserId = userId,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            CreatedAt = at,
            Note = $"{action} aksiyonu demo senaryosunda uygulandı."
        };

    private static SystemAuditLog SystemAudit(
        Guid processId,
        Guid actorId,
        string action,
        DateTime at,
        string description,
        Guid? taskId = null) =>
        new()
        {
            Id = StableGuid($"action-lab:system-audit:{processId}:{action}:{at.Ticks}"),
            ActorUserId = actorId,
            CommunityId = SportCommunityId,
            Category = action.StartsWith("Task.", StringComparison.Ordinal) ? "tasks" : "processes",
            Action = action,
            EntityType = action.StartsWith("Task.", StringComparison.Ordinal) ? "ProcessTask" : "ProcessInstance",
            EntityId = (taskId ?? processId).ToString(),
            Description = description,
            CreatedAt = at
        };

    private static string TeamName(Guid? teamId) => teamId switch
    {
        var id when id == SportScoutTeamId => "Scout Ekibi",
        var id when id == SportTechnicalTeamId => "Teknik Değerlendirme",
        var id when id == SportFinanceTeamId => "Mali İşler",
        var id when id == SportTransferTeamId => "Transfer Operasyon",
        var id when id == TransferPoolTeamId => "Transfer Demo Havuzu",
        var id when id == TransferRoleTeamId => "Transfer Demo Rol Ekibi",
        var id when id == TransferLeadTeamId => "Transfer Demo Sorumluları",
        var id when id == OperationsPoolTeamId => "Operasyon Demo Havuzu",
        var id when id == OperationsRoleTeamId => "Operasyon Demo Rol Ekibi",
        var id when id == OperationsLeadTeamId => "Operasyon Demo Sorumluları",
        _ => string.Empty
    };

    private static string UserName(Guid userId) => userId switch
    {
        var id when id == SportDemoOperationsId => "Sportif Demo Transfer Operasyon",
        var id when id == SportDemoApproverId => "Sportif Demo Teknik Onay",
        _ => string.Empty
    };

    private static Guid ProcessId(string workflowKey, ActionScenario scenario) =>
        StableGuid($"action-lab:process:{workflowKey}:{scenario}");

    private static Guid StableGuid(string value) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record WorkflowSpec(
        string Key,
        Guid DefinitionId,
        Guid VersionId,
        string Name,
        string Description,
        Guid StartFormId,
        Guid StartFormVersionId,
        TaskSpec Pool,
        TaskSpec Role,
        TaskSpec Person,
        TaskSpec Lead);

    private sealed record TaskSpec(
        string Key,
        string Title,
        Guid FormVersionId,
        TaskAssignmentType AssignmentType,
        Guid? TeamId,
        Guid? RoleId,
        Guid? UserId,
        Guid PerformerId,
        bool RequiresTeamLead);

    private sealed record CompletedTask(TaskSpec Task, WorkflowAction Action, int Attempt);

    private sealed record SeededScenario(
        ProcessInstance Process,
        IReadOnlyList<Notification> Notifications,
        IReadOnlyList<SystemAuditLog> SystemAudits);

    private enum ActionScenario
    {
        Complete = 1,
        Approve = 2,
        SendBack = 3,
        Reject = 4,
        Escalate = 5
    }
}
