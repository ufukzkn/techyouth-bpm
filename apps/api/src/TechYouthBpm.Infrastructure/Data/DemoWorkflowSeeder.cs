using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Infrastructure.Data;

internal static class DemoWorkflowSeeder
{
    private static readonly Guid SportDefinitionId = Guid.Parse("abababab-0000-0000-0000-000000000002");
    private static readonly Guid LogisticsDefinitionId = Guid.Parse("abababab-0000-0000-0000-000000000003");
    private static readonly Guid ProductDefinitionId = Guid.Parse("abababab-0000-0000-0000-000000000004");
    private static readonly Guid HumanResourcesDefinitionId = Guid.Parse("abababab-0000-0000-0000-000000000005");
    private static readonly Guid ProcurementDefinitionId = Guid.Parse("abababab-0000-0000-0000-000000000006");

    private static readonly Guid SportVersionId = Guid.Parse("abababab-2000-0000-0000-000000000002");
    private static readonly Guid LogisticsVersionId = Guid.Parse("abababab-1000-0000-0000-000000000003");
    private static readonly Guid ProductVersionId = Guid.Parse("abababab-1000-0000-0000-000000000004");
    private static readonly Guid HumanResourcesVersionId = Guid.Parse("abababab-1000-0000-0000-000000000005");
    private static readonly Guid ProcurementVersionId = Guid.Parse("abababab-1000-0000-0000-000000000006");

    private static readonly Guid SportReviewTeamId = Guid.Parse("30303030-0000-0000-0000-000000000002");
    private static readonly Guid SportApprovalTeamId = Guid.Parse("30303030-0000-0000-0000-000000000004");
    private static readonly Guid LogisticsReviewTeamId = Guid.Parse("30303030-0000-0000-0000-000000000007");
    private static readonly Guid LogisticsApprovalTeamId = Guid.Parse("30303030-0000-0000-0000-000000000006");
    private static readonly Guid ProductReviewTeamId = Guid.Parse("30303030-0000-0000-0000-000000000009");
    private static readonly Guid ProductApprovalTeamId = Guid.Parse("30303030-0000-0000-0000-000000000010");
    private static readonly Guid HumanResourcesReviewTeamId = Guid.Parse("30303030-0000-0000-0000-000000000013");
    private static readonly Guid HumanResourcesApprovalTeamId = Guid.Parse("30303030-0000-0000-0000-000000000011");
    private static readonly Guid ProcurementReviewTeamId = Guid.Parse("30303030-0000-0000-0000-000000000015");
    private static readonly Guid ProcurementApprovalTeamId = Guid.Parse("30303030-0000-0000-0000-000000000014");

    private static readonly Guid SportAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000001");
    private static readonly Guid SportApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000003");
    private static readonly Guid LogisticsAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000004");
    private static readonly Guid LogisticsApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000005");
    private static readonly Guid ProductAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000006");
    private static readonly Guid ProductApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000023");
    private static readonly Guid HumanResourcesAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000013");
    private static readonly Guid HumanResourcesApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000016");
    private static readonly Guid ProcurementAdminRoleId = Guid.Parse("20202020-0000-0000-0000-000000000017");
    private static readonly Guid ProcurementApproverRoleId = Guid.Parse("20202020-0000-0000-0000-000000000020");

    private static readonly Guid SportStarterId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SportReviewerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SportApproverId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid LogisticsStarterId = Guid.Parse("12121212-0000-0000-0000-000000000001");
    private static readonly Guid LogisticsReviewerId = Guid.Parse("99999999-1111-1111-1111-111111111111");
    private static readonly Guid LogisticsApproverId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProductStarterId = Guid.Parse("12121212-0000-0000-0000-000000000003");
    private static readonly Guid ProductReviewerId = Guid.Parse("12121212-0000-0000-0000-000000000004");
    private static readonly Guid ProductApproverId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid HumanResourcesStarterId = Guid.Parse("99999999-6666-6666-6666-666666666666");
    private static readonly Guid HumanResourcesReviewerId = Guid.Parse("12121212-0000-0000-0000-000000000006");
    private static readonly Guid HumanResourcesApproverId = Guid.Parse("99999999-4444-4444-4444-444444444444");
    private static readonly Guid ProcurementStarterId = Guid.Parse("12121212-0000-0000-0000-000000000009");
    private static readonly Guid ProcurementReviewerId = Guid.Parse("12121212-0000-0000-0000-000000000010");
    private static readonly Guid ProcurementApproverId = Guid.Parse("99999999-5555-5555-5555-555555555555");

    private static readonly Guid[] LegacyProcessIds = Enumerable.Range(1, 14)
        .Select(index => Guid.Parse($"cccccccc-0000-0000-0000-{index:000000000000}"))
        .ToArray();

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var specs = await ResolveRoleReferencesAsync(db, BuildSpecs(), cancellationToken);
        var versions = await EnsureDefinitionsAsync(db, specs, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await RemoveLegacyProcessesAsync(db, cancellationToken);
        await AddScenarioProcessesAsync(db, specs, versions, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<WorkflowSpec>> ResolveRoleReferencesAsync(
        AppDbContext db,
        IReadOnlyList<WorkflowSpec> specs,
        CancellationToken cancellationToken)
    {
        var communityIds = specs.Select(spec => spec.CommunityId).Distinct().ToArray();
        var roles = await db.CommunityRoles
            .AsNoTracking()
            .Where(role => communityIds.Contains(role.CommunityId)
                && (role.TemplateKey == CommunityRoleTemplates.Approver
                    || role.TemplateKey == CommunityRoleTemplates.CommunityAdmin))
            .Select(role => new { role.Id, role.CommunityId, role.TemplateKey })
            .ToListAsync(cancellationToken);

        Guid Resolve(Guid communityId, string templateKey, Guid preferredId)
        {
            var candidates = roles
                .Where(role => role.CommunityId == communityId && role.TemplateKey == templateKey)
                .ToArray();
            var preferred = candidates.SingleOrDefault(role => role.Id == preferredId);
            var resolved = preferred ?? candidates.FirstOrDefault();
            return resolved?.Id
                ?? throw new InvalidOperationException(
                    $"Community {communityId} does not have the required {templateKey} role.");
        }

        return specs
            .Select(spec => spec with
            {
                ReviewRoleId = Resolve(spec.CommunityId, CommunityRoleTemplates.Approver, spec.ReviewRoleId),
                ApprovalRoleId = Resolve(spec.CommunityId, CommunityRoleTemplates.CommunityAdmin, spec.ApprovalRoleId)
            })
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<string, ProcessDefinitionVersion>> EnsureDefinitionsAsync(
        AppDbContext db,
        IReadOnlyList<WorkflowSpec> specs,
        CancellationToken cancellationToken)
    {
        var definitionIds = specs.Select(spec => spec.DefinitionId).ToArray();
        var definitions = await db.ProcessDefinitions
            .Where(definition => definitionIds.Contains(definition.Id))
            .Include(definition => definition.Versions)
            .ToDictionaryAsync(definition => definition.Id, cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var spec in specs)
        {
            if (!definitions.TryGetValue(spec.DefinitionId, out var definition))
            {
                definition = new ProcessDefinition
                {
                    Id = spec.DefinitionId,
                    Name = spec.Name,
                    Description = spec.Description,
                    CommunityId = spec.CommunityId,
                    CreatedByUserId = spec.OwnerId,
                    CreatedAt = now.AddDays(-18)
                };
                definitions[spec.DefinitionId] = definition;
                db.ProcessDefinitions.Add(definition);
            }

            if (definition.Versions.Any(version => version.Id == spec.VersionId))
            {
                continue;
            }

            var startVersionId = await DemoFormSeeder.PublishedVersionIdAsync(
                db,
                spec.StartFormId,
                cancellationToken);
            var reviewVersionId = await DemoFormSeeder.PublishedVersionIdAsync(
                db,
                spec.ReviewFormId,
                cancellationToken);
            var approvalVersionId = await DemoFormSeeder.PublishedVersionIdAsync(
                db,
                spec.ApprovalFormId,
                cancellationToken);
            var nextVersionNumber = definition.Versions.Count == 0
                ? 1
                : definition.Versions.Max(version => version.VersionNumber) + 1;
            db.ProcessDefinitionVersions.Add(new ProcessDefinitionVersion
            {
                Id = spec.VersionId,
                ProcessDefinitionId = spec.DefinitionId,
                ProcessDefinition = definition,
                VersionNumber = nextVersionNumber,
                Status = DefinitionVersionStatus.Published,
                FormDefinitionVersionId = startVersionId,
                GraphJson = JsonHelpers.Serialize(BuildGraph(spec, startVersionId, reviewVersionId, approvalVersionId)),
                CreatedByUserId = spec.OwnerId,
                CreatedAt = now.AddDays(-14),
                PublishedByUserId = spec.OwnerId,
                PublishedAt = now.AddDays(-14)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await db.ProcessDefinitionVersions
            .Where(version => specs.Select(spec => spec.VersionId).Contains(version.Id))
            .Include(version => version.ProcessDefinition)
            .ToDictionaryAsync(
                version => specs.Single(spec => spec.VersionId == version.Id).Key,
                cancellationToken);
    }

    private static ProcessGraphDto BuildGraph(
        WorkflowSpec spec,
        Guid startFormVersionId,
        Guid reviewFormVersionId,
        Guid approvalFormVersionId) =>
        new(
            "1.0",
            [
                new ProcessNodeDto(
                    "start",
                    ProcessNodeType.Start,
                    spec.StartTitle,
                    startFormVersionId,
                    PositionX: 60,
                    PositionY: 130,
                    Width: 170,
                    Height: 72),
                new ProcessNodeDto(
                    "review",
                    ProcessNodeType.UserTask,
                    spec.ReviewTitle,
                    reviewFormVersionId,
                    TaskPriority.High,
                    [WorkflowAction.Approve, WorkflowAction.Reject],
                    new TaskAssignmentDto(
                        TaskAssignmentType.TeamAndCommunityRole,
                        TeamId: spec.ReviewTeamId,
                        CommunityRoleId: spec.ReviewRoleId),
                    PositionX: 330,
                    PositionY: 110,
                    Width: 230,
                    Height: 100,
                    Description: spec.ReviewDescription,
                    SlaDurationMinutes: spec.ReviewSlaMinutes),
                new ProcessNodeDto(
                    "approval",
                    ProcessNodeType.UserTask,
                    spec.ApprovalTitle,
                    approvalFormVersionId,
                    TaskPriority.Critical,
                    [WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.SendBack],
                    new TaskAssignmentDto(
                        TaskAssignmentType.TeamAndCommunityRole,
                        TeamId: spec.ApprovalTeamId,
                        CommunityRoleId: spec.ApprovalRoleId),
                    PositionX: 650,
                    PositionY: 110,
                    Width: 230,
                    Height: 100,
                    Description: spec.ApprovalDescription,
                    SlaDurationMinutes: spec.ApprovalSlaMinutes),
                new ProcessNodeDto("completed", ProcessNodeType.CompletedEnd, "Tamamlandi", PositionX: 980, PositionY: 60, Width: 160, Height: 72),
                new ProcessNodeDto("rejected", ProcessNodeType.RejectedEnd, "Reddedildi", PositionX: 980, PositionY: 190, Width: 160, Height: 72)
            ],
            [
                new ProcessEdgeDto("start", "review", Order: 0, Label: "Baslat"),
                new ProcessEdgeDto("review", "approval", WorkflowAction.Approve, Order: 1, Label: "Onayla"),
                new ProcessEdgeDto("review", "rejected", WorkflowAction.Reject, Order: 2, Label: "Reddet"),
                new ProcessEdgeDto("approval", "completed", WorkflowAction.Approve, Order: 3, Label: "Onayla"),
                new ProcessEdgeDto("approval", "rejected", WorkflowAction.Reject, Order: 4, Label: "Reddet"),
                new ProcessEdgeDto("approval", "review", WorkflowAction.SendBack, Order: 5, Label: "Geri Gonder")
            ]);

    private static async Task RemoveLegacyProcessesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var legacyProcessIdTexts = LegacyProcessIds.Select(id => id.ToString()).ToArray();
        var legacyTaskIdTexts = LegacyProcessIds
            .Select(id => id.ToString().Replace("cccccccc", "dddddddd", StringComparison.Ordinal))
            .ToArray();
        var legacyProcesses = await db.ProcessInstances
            .Where(process => LegacyProcessIds.Contains(process.Id))
            .ToListAsync(cancellationToken);
        if (legacyProcesses.Count > 0)
        {
            db.ProcessInstances.RemoveRange(legacyProcesses);
        }

        var staleNotifications = await db.Notifications
            .Where(notification => notification.EntityId != null
                && (legacyProcessIdTexts.Contains(notification.EntityId)
                    || legacyTaskIdTexts.Contains(notification.EntityId)))
            .ToListAsync(cancellationToken);
        var staleSystemLogs = await db.SystemAuditLogs
            .Where(log => log.EntityId != null
                && (legacyProcessIdTexts.Contains(log.EntityId)
                    || legacyTaskIdTexts.Contains(log.EntityId)))
            .ToListAsync(cancellationToken);
        db.Notifications.RemoveRange(staleNotifications);
        db.SystemAuditLogs.RemoveRange(staleSystemLogs);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task AddScenarioProcessesAsync(
        AppDbContext db,
        IReadOnlyList<WorkflowSpec> specs,
        IReadOnlyDictionary<string, ProcessDefinitionVersion> versions,
        CancellationToken cancellationToken)
    {
        var desiredIds = specs
            .SelectMany(spec => Enum.GetValues<DemoScenario>().Select(scenario => ProcessId(spec.Key, scenario)))
            .ToArray();
        var existingIds = (await db.ProcessInstances
                .Where(process => desiredIds.Contains(process.Id))
                .Select(process => process.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var spec in specs)
        {
            var version = versions[spec.Key];
            var reviewFormVersionId = await DemoFormSeeder.PublishedVersionIdAsync(db, spec.ReviewFormId, cancellationToken);
            var approvalFormVersionId = await DemoFormSeeder.PublishedVersionIdAsync(db, spec.ApprovalFormId, cancellationToken);
            foreach (var scenario in Enum.GetValues<DemoScenario>())
            {
                var processId = ProcessId(spec.Key, scenario);
                if (existingIds.Contains(processId))
                {
                    continue;
                }

                var seeded = BuildScenarioProcess(spec, version, reviewFormVersionId, approvalFormVersionId, scenario);
                db.ProcessInstances.Add(seeded.Process);
                db.Notifications.AddRange(seeded.Notifications);
                db.SystemAuditLogs.AddRange(seeded.SystemAuditLogs);
            }
        }
    }

    private static SeededProcess BuildScenarioProcess(
        WorkflowSpec spec,
        ProcessDefinitionVersion version,
        Guid reviewFormVersionId,
        Guid approvalFormVersionId,
        DemoScenario scenario)
    {
        var processId = ProcessId(spec.Key, scenario);
        var now = DateTime.UtcNow;
        var startedAt = scenario switch
        {
            DemoScenario.ReviewOverdue => now.AddDays(-3),
            DemoScenario.ApprovalUpcoming => now.AddDays(-1),
            DemoScenario.Completed => now.AddDays(-8),
            DemoScenario.CompletedAlternative => now.AddDays(-10),
            DemoScenario.Rejected => now.AddDays(-6),
            _ => now.AddDays(-2)
        };
        var starterId = scenario == DemoScenario.CompletedAlternative ? spec.OwnerId : spec.StarterId;
        var startData = StartData(spec, scenario);
        var steps = new Dictionary<string, object?>();
        var tasks = new List<ProcessTask>();
        var executions = new List<ProcessStepExecution>
        {
            Step(processId, "start", ProcessNodeType.Start, 1, ProcessStepStatus.Completed, startedAt, startedAt, starterId, WorkflowAction.Start, startData)
        };
        var audits = new List<AuditLog>
        {
            Audit(processId, starterId, WorkflowAction.Start, ProcessStatus.Pending, ProcessStatus.InProgress, startedAt, "Demo sureci baslatildi.")
        };
        var status = ProcessStatus.InProgress;
        var currentNode = "review";
        DateTime? completedAt = null;

        if (scenario == DemoScenario.ReviewOverdue)
        {
            var createdAt = startedAt.AddMinutes(20);
            tasks.Add(Task(
                processId,
                "review",
                1,
                spec.ReviewTitle,
                TaskPriority.High,
                spec.ReviewTeamId,
                spec.ReviewRoleId,
                reviewFormVersionId,
                createdAt,
                createdAt.AddMinutes(spec.ReviewSlaMinutes)));
            executions.Add(Step(processId, "review", ProcessNodeType.UserTask, 1, ProcessStepStatus.Active, createdAt));
        }
        else
        {
            var reviewCreatedAt = startedAt.AddMinutes(20);
            var reviewCompletedAt = reviewCreatedAt.AddHours(2);
            var reviewOutput = ReviewOutput(spec, scenario);
            steps["review"] = reviewOutput;
            tasks.Add(Task(
                processId,
                "review",
                1,
                spec.ReviewTitle,
                TaskPriority.High,
                spec.ReviewTeamId,
                spec.ReviewRoleId,
                reviewFormVersionId,
                reviewCreatedAt,
                reviewCreatedAt.AddMinutes(spec.ReviewSlaMinutes),
                ProcessTaskStatus.Completed,
                reviewCompletedAt,
                spec.ReviewerId));
            executions.Add(Step(
                processId,
                "review",
                ProcessNodeType.UserTask,
                1,
                ProcessStepStatus.Completed,
                reviewCreatedAt,
                reviewCompletedAt,
                spec.ReviewerId,
                WorkflowAction.Approve,
                reviewOutput));
            audits.Add(Audit(processId, spec.ReviewerId, WorkflowAction.Approve, ProcessStatus.InProgress, ProcessStatus.InProgress, reviewCompletedAt, "Ilk inceleme tamamlandi."));

            var approvalCreatedAt = reviewCompletedAt.AddMinutes(15);
            if (scenario == DemoScenario.ApprovalUpcoming)
            {
                currentNode = "approval";
                tasks.Add(Task(
                    processId,
                    "approval",
                    1,
                    spec.ApprovalTitle,
                    TaskPriority.Critical,
                    spec.ApprovalTeamId,
                    spec.ApprovalRoleId,
                    approvalFormVersionId,
                    approvalCreatedAt,
                    now.AddHours(8)));
                executions.Add(Step(processId, "approval", ProcessNodeType.UserTask, 1, ProcessStepStatus.Active, approvalCreatedAt));
            }
            else
            {
                var approvalCompletedAt = approvalCreatedAt.AddHours(3);
                var approvalOutput = ApprovalOutput(spec, scenario);
                tasks.Add(Task(
                    processId,
                    "approval",
                    1,
                    spec.ApprovalTitle,
                    TaskPriority.Critical,
                    spec.ApprovalTeamId,
                    spec.ApprovalRoleId,
                    approvalFormVersionId,
                    approvalCreatedAt,
                    approvalCreatedAt.AddMinutes(spec.ApprovalSlaMinutes),
                    ProcessTaskStatus.Completed,
                    approvalCompletedAt,
                    spec.ApproverId));

                if (scenario == DemoScenario.SentBack)
                {
                    steps.Clear();
                    executions.Add(Step(
                        processId,
                        "approval",
                        ProcessNodeType.UserTask,
                        1,
                        ProcessStepStatus.Completed,
                        approvalCreatedAt,
                        approvalCompletedAt,
                        spec.ApproverId,
                        WorkflowAction.SendBack,
                        approvalOutput));
                    audits.Add(Audit(processId, spec.ApproverId, WorkflowAction.SendBack, ProcessStatus.InProgress, ProcessStatus.InProgress, approvalCompletedAt, "Bilgiler duzeltilmek uzere geri gonderildi."));
                    var reopenedAt = now.AddHours(-1);
                    tasks.Add(Task(
                        processId,
                        "review",
                        2,
                        spec.ReviewTitle,
                        TaskPriority.Critical,
                        spec.ReviewTeamId,
                        spec.ReviewRoleId,
                        reviewFormVersionId,
                        reopenedAt,
                        reopenedAt.AddMinutes(spec.ReviewSlaMinutes)));
                    executions.Add(Step(processId, "review", ProcessNodeType.UserTask, 2, ProcessStepStatus.Active, reopenedAt));
                    currentNode = "review";
                }
                else
                {
                    steps["approval"] = approvalOutput;
                    var action = scenario == DemoScenario.Rejected ? WorkflowAction.Reject : WorkflowAction.Approve;
                    status = scenario == DemoScenario.Rejected ? ProcessStatus.Rejected : ProcessStatus.Completed;
                    currentNode = scenario == DemoScenario.Rejected ? "rejected" : "completed";
                    completedAt = approvalCompletedAt;
                    executions.Add(Step(
                        processId,
                        "approval",
                        ProcessNodeType.UserTask,
                        1,
                        ProcessStepStatus.Completed,
                        approvalCreatedAt,
                        approvalCompletedAt,
                        spec.ApproverId,
                        action,
                        approvalOutput));
                    executions.Add(Step(
                        processId,
                        currentNode,
                        scenario == DemoScenario.Rejected ? ProcessNodeType.RejectedEnd : ProcessNodeType.CompletedEnd,
                        1,
                        ProcessStepStatus.Completed,
                        approvalCompletedAt,
                        approvalCompletedAt,
                        spec.ApproverId));
                    audits.Add(Audit(processId, spec.ApproverId, action, ProcessStatus.InProgress, status, approvalCompletedAt, $"Surec {status.ToString().ToLowerInvariant()} durumuna gecti."));
                }
            }
        }

        var process = new ProcessInstance
        {
            Id = processId,
            FormDefinitionId = spec.StartFormId,
            FormDefinitionVersionId = version.FormDefinitionVersionId,
            ProcessDefinitionVersionId = version.Id,
            CommunityId = spec.CommunityId,
            StartedByUserId = starterId,
            Status = status,
            FormDataJson = JsonHelpers.Serialize(startData),
            VariablesJson = JsonHelpers.Serialize(new Dictionary<string, object?>
            {
                ["start"] = startData,
                ["steps"] = steps
            }),
            CurrentNodeKey = currentNode,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Tasks = tasks,
            StepExecutions = executions,
            AuditLogs = audits
        };
        var notifications = BuildNotifications(spec, process, scenario);
        var systemLogs = new[]
        {
            new SystemAuditLog
            {
                Id = StableGuid($"demo-system-audit:{processId}"),
                ActorUserId = spec.StarterId,
                Action = "Process.Seeded",
                EntityType = "ProcessInstance",
                EntityId = processId.ToString(),
                Description = $"'{spec.Name}' icin graph uyumlu demo sureci olusturuldu.",
                CreatedAt = startedAt
            }
        };
        return new SeededProcess(process, notifications, systemLogs);
    }

    private static IReadOnlyList<Notification> BuildNotifications(
        WorkflowSpec spec,
        ProcessInstance process,
        DemoScenario scenario)
    {
        var notifications = new List<Notification>();
        var openTask = process.Tasks.SingleOrDefault(task => task.Status == ProcessTaskStatus.Open);
        if (openTask is not null)
        {
            var recipientId = openTask.NodeKey == "approval" ? spec.ApproverId : spec.ReviewerId;
            notifications.Add(new Notification
            {
                Id = StableGuid($"demo-notification:{process.Id}:task:{openTask.Id}"),
                UserId = recipientId,
                Type = "Task.Assigned",
                Title = "Yeni gorev atandi",
                Message = $"{openTask.Title} gorevi aksiyonunuzu bekliyor.",
                EntityType = "ProcessTask",
                EntityId = openTask.Id.ToString(),
                CreatedAt = openTask.CreatedAt
            });
        }

        if (scenario is DemoScenario.Completed or DemoScenario.CompletedAlternative or DemoScenario.Rejected)
        {
            var isCompleted = scenario is DemoScenario.Completed or DemoScenario.CompletedAlternative;
            notifications.Add(new Notification
            {
                Id = StableGuid($"demo-notification:{process.Id}:outcome"),
                UserId = process.StartedByUserId,
                Type = isCompleted ? "Process.Completed" : "Process.Rejected",
                Title = isCompleted ? "Surec tamamlandi" : "Surec reddedildi",
                Message = $"{spec.Name} sonucu guncellendi.",
                EntityType = "ProcessInstance",
                EntityId = process.Id.ToString(),
                CreatedAt = process.CompletedAt ?? process.StartedAt
            });
        }

        return notifications;
    }

    private static ProcessTask Task(
        Guid processId,
        string nodeKey,
        int attempt,
        string title,
        TaskPriority priority,
        Guid teamId,
        Guid roleId,
        Guid formVersionId,
        DateTime createdAt,
        DateTime? dueAt,
        ProcessTaskStatus status = ProcessTaskStatus.Open,
        DateTime? completedAt = null,
        Guid? completedByUserId = null) =>
        new()
        {
            Id = StableGuid($"demo-task:{processId}:{nodeKey}:{attempt}"),
            ProcessInstanceId = processId,
            AssignedRole = Role.User,
            AssignedCommunityRoleId = roleId,
            NodeKey = nodeKey,
            Attempt = attempt,
            Title = title,
            Priority = priority,
            AssignmentType = TaskAssignmentType.TeamAndCommunityRole,
            CandidateTeamId = teamId,
            CandidateCommunityRoleId = roleId,
            ClaimVersion = StableGuid($"demo-claim:{processId}:{nodeKey}:{attempt}"),
            FormDefinitionVersionId = formVersionId,
            RequiredPermission = PermissionNames.TasksAct,
            Status = status,
            AvailableActionsJson = JsonHelpers.Serialize(nodeKey == "approval"
                ? new[] { WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.SendBack }
                : new[] { WorkflowAction.Approve, WorkflowAction.Reject }),
            CreatedAt = createdAt,
            DueAt = dueAt,
            CompletedAt = completedAt,
            CompletedByUserId = completedByUserId
        };

    private static ProcessStepExecution Step(
        Guid processId,
        string nodeKey,
        ProcessNodeType nodeType,
        int attempt,
        ProcessStepStatus status,
        DateTime enteredAt,
        DateTime? completedAt = null,
        Guid? completedByUserId = null,
        WorkflowAction? action = null,
        object? output = null) =>
        new()
        {
            Id = StableGuid($"demo-step:{processId}:{nodeKey}:{attempt}"),
            ProcessInstanceId = processId,
            NodeKey = nodeKey,
            NodeType = nodeType,
            Attempt = attempt,
            Status = status,
            EnteredAt = enteredAt,
            CompletedAt = completedAt,
            CompletedByUserId = completedByUserId,
            Action = action,
            OutputJson = JsonHelpers.Serialize(output ?? new { })
        };

    private static AuditLog Audit(
        Guid processId,
        Guid userId,
        WorkflowAction action,
        ProcessStatus from,
        ProcessStatus to,
        DateTime createdAt,
        string note) =>
        new()
        {
            Id = StableGuid($"demo-audit:{processId}:{action}:{createdAt.Ticks}"),
            ProcessInstanceId = processId,
            UserId = userId,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            CreatedAt = createdAt,
            Note = note
        };

    private static Dictionary<string, object?> StartData(WorkflowSpec spec, DemoScenario scenario) =>
        spec.Key switch
        {
            "sport" => new()
            {
                ["talepSahibi"] = spec.StarterLabel,
                ["oyuncuAdi"] = scenario is DemoScenario.Completed or DemoScenario.CompletedAlternative ? "Mario Gomez" : "Ricardo Quaresma",
                ["kulup"] = "Besiktas",
                ["pozisyon"] = "Forvet",
                ["bonservis"] = scenario == DemoScenario.ApprovalUpcoming ? 7_500_000 : 2_500_000,
                ["acilMi"] = scenario == DemoScenario.ReviewOverdue,
                ["gerekce"] = "Teknik kadro ihtiyaci."
            },
            "logistics" => new()
            {
                ["sorumlu"] = spec.StarterLabel,
                ["hedefKisi"] = "Depo Operasyon",
                ["kategori"] = "Kamp",
                ["tarih"] = DateTime.UtcNow.AddDays(4).ToString("yyyy-MM-dd"),
                ["iletisim"] = "lojistik@techyouth.local",
                ["not"] = "Demo sevkiyat talebi."
            },
            "product" => new()
            {
                ["talepSahibi"] = spec.StarterLabel,
                ["urunAdi"] = "Antrenman ekipmani",
                ["adet"] = scenario == DemoScenario.ApprovalUpcoming ? 40 : 12,
                ["acil"] = scenario == DemoScenario.ReviewOverdue
            },
            "hr" => new()
            {
                ["calisan"] = spec.StarterLabel,
                ["talepTipi"] = "Uzaktan Calisma",
                ["baslangicTarihi"] = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"),
                ["gunSayisi"] = 3,
                ["aciklama"] = "Demo calisma duzeni talebi."
            },
            _ => new()
            {
                ["talepSahibi"] = spec.StarterLabel,
                ["kategori"] = "Ekipman",
                ["tutar"] = scenario == DemoScenario.ApprovalUpcoming ? 750_000 : 125_000,
                ["tedarikci"] = "TechYouth Tedarik",
                ["acil"] = scenario == DemoScenario.ReviewOverdue
            }
        };

    private static Dictionary<string, object?> ReviewOutput(WorkflowSpec spec, DemoScenario scenario) =>
        spec.Key switch
        {
            "sport" => new()
            {
                ["teknikKarar"] = scenario == DemoScenario.Rejected ? "Uygun Degil" : "Uygun",
                ["teknikNot"] = "Kadro ve oyun plani degerlendirmesi tamamlandi."
            },
            "logistics" => new()
            {
                ["rotaNotu"] = "Rota ve kapasite kontrol edildi.",
                ["kapasiteUygun"] = scenario != DemoScenario.Rejected
            },
            "product" => new()
            {
                ["stokVar"] = scenario != DemoScenario.Rejected,
                ["ayrilanAdet"] = scenario == DemoScenario.Rejected ? 0 : 12
            },
            "hr" => new()
            {
                ["ekipPlani"] = "Vekalet ve kapasite plani hazir.",
                ["yoneticiUygun"] = scenario != DemoScenario.Rejected
            },
            _ => new()
            {
                ["tedarikci"] = "TechYouth Tedarik",
                ["teklifSayisi"] = 3,
                ["uygun"] = scenario != DemoScenario.Rejected
            }
        };

    private static Dictionary<string, object?> ApprovalOutput(WorkflowSpec spec, DemoScenario scenario) =>
        spec.Key switch
        {
            "sport" => new()
            {
                ["sozlesmeImzalandi"] = scenario is DemoScenario.Completed or DemoScenario.CompletedAlternative,
                ["tamamlanmaTarihi"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                ["operasyonNotu"] = scenario == DemoScenario.SentBack ? "Evraklar eksik." : "Operasyon karari kaydedildi."
            },
            "logistics" => new()
            {
                ["sevkKodu"] = $"SVK-{scenario.ToString()[..3].ToUpperInvariant()}",
                ["teslimEdildi"] = scenario is DemoScenario.Completed or DemoScenario.CompletedAlternative,
                ["sureSaat"] = 18
            },
            "product" => new()
            {
                ["paketKodu"] = $"PKT-{scenario.ToString()[..3].ToUpperInvariant()}",
                ["hazirlandi"] = scenario is DemoScenario.Completed or DemoScenario.CompletedAlternative
            },
            "hr" => new()
            {
                ["ozlukNotu"] = scenario == DemoScenario.SentBack ? "Tarih bilgisi duzeltilmeli." : "Talep ozluk kaydina islendi.",
                ["kaydaAlindi"] = scenario is DemoScenario.Completed or DemoScenario.CompletedAlternative
            },
            _ => new()
            {
                ["onaylananButce"] = scenario == DemoScenario.Rejected ? 0 : 125_000,
                ["butceOnaylandi"] = scenario is DemoScenario.Completed or DemoScenario.CompletedAlternative
            }
        };

    private static Guid ProcessId(string key, DemoScenario scenario) =>
        StableGuid($"demo-process:{key}:{scenario}");

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static IReadOnlyList<WorkflowSpec> BuildSpecs() =>
    [
        new(
            "sport",
            SportDefinitionId,
            SportVersionId,
            DemoFormSeeder.SportCommunityId,
            SportApproverId,
            "Transfer Talep Akisi",
            "Transfer talebini teknik inceleme ve operasyon onayina tasir.",
            DemoFormSeeder.SportStartFormId,
            DemoFormSeeder.SportReviewFormId,
            DemoFormSeeder.SportApprovalFormId,
            SportReviewTeamId,
            SportApprovalTeamId,
            SportApproverRoleId,
            SportAdminRoleId,
            SportStarterId,
            SportReviewerId,
            SportApproverId,
            "Transfer Talebi",
            "Teknik Degerlendirme",
            "Transfer Operasyon Onayi",
            "Teknik ekip talebi ve oyuncu uygunlugunu inceler.",
            "Transfer operasyon ekibi son karari verir.",
            8 * 60,
            24 * 60,
            "Demo Talep Sahibi"),
        new(
            "logistics",
            LogisticsDefinitionId,
            LogisticsVersionId,
            DemoFormSeeder.LogisticsCommunityId,
            LogisticsApproverId,
            "Sevkiyat Operasyon Akisi",
            "Planlama, depo ve teslimat adimlarini takip eder.",
            DemoFormSeeder.LogisticsStartFormId,
            DemoFormSeeder.LogisticsReviewFormId,
            DemoFormSeeder.LogisticsApprovalFormId,
            LogisticsReviewTeamId,
            LogisticsApprovalTeamId,
            LogisticsApproverRoleId,
            LogisticsAdminRoleId,
            LogisticsStarterId,
            LogisticsReviewerId,
            LogisticsApproverId,
            "Sevkiyat Talebi",
            "Teslimat Incelemesi",
            "Depo Cikis Onayi",
            "Teslimat ekibi rota ve hedef bilgisini inceler.",
            "Depo ekibi cikis ve sevk kararini tamamlar.",
            6 * 60,
            12 * 60,
            "Lojistik Talep Sahibi"),
        new(
            "product",
            ProductDefinitionId,
            ProductVersionId,
            DemoFormSeeder.ProductCommunityId,
            ProductApproverId,
            "Siparis Karsilama Akisi",
            "Stok kontrolunden paketlemeye urun siparisini yonetir.",
            DemoFormSeeder.ProductStartFormId,
            DemoFormSeeder.ProductReviewFormId,
            DemoFormSeeder.ProductApprovalFormId,
            ProductReviewTeamId,
            ProductApprovalTeamId,
            ProductApproverRoleId,
            ProductAdminRoleId,
            ProductStarterId,
            ProductReviewerId,
            ProductApproverId,
            "Urun Siparisi",
            "Stok Kontrolu",
            "Siparis Hazirlama",
            "Stok ekibi miktar ve rezervasyon uygunlugunu kontrol eder.",
            "Hazirlama ekibi paket ve cikis bilgisini tamamlar.",
            4 * 60,
            10 * 60,
            "Urun Talep Sahibi"),
        new(
            "hr",
            HumanResourcesDefinitionId,
            HumanResourcesVersionId,
            DemoFormSeeder.HumanResourcesCommunityId,
            HumanResourcesApproverId,
            "Izin ve Uzaktan Calisma Akisi",
            "Ekip kapasitesi ve ozluk kaydi uzerinden calisan talebini yonetir.",
            DemoFormSeeder.HumanResourcesStartFormId,
            DemoFormSeeder.HumanResourcesReviewFormId,
            DemoFormSeeder.HumanResourcesApprovalFormId,
            HumanResourcesReviewTeamId,
            HumanResourcesApprovalTeamId,
            HumanResourcesApproverRoleId,
            HumanResourcesAdminRoleId,
            HumanResourcesStarterId,
            HumanResourcesReviewerId,
            HumanResourcesApproverId,
            "Calisma Duzeni Talebi",
            "Ekip Kapasite Kontrolu",
            "IK Kayit Onayi",
            "Ekip kapasitesi ve izin takvimi degerlendirilir.",
            "IK ekibi talebi ozluk kaydina alir.",
            24 * 60,
            48 * 60,
            "Calisan"),
        new(
            "procurement",
            ProcurementDefinitionId,
            ProcurementVersionId,
            DemoFormSeeder.ProcurementCommunityId,
            ProcurementApproverId,
            "Talep Tedarikci ve Butce Akisi",
            "Satin alma talebini tedarikci ve butce kararlarina tasir.",
            DemoFormSeeder.ProcurementStartFormId,
            DemoFormSeeder.ProcurementReviewFormId,
            DemoFormSeeder.ProcurementApprovalFormId,
            ProcurementReviewTeamId,
            ProcurementApprovalTeamId,
            ProcurementApproverRoleId,
            ProcurementAdminRoleId,
            ProcurementStarterId,
            ProcurementReviewerId,
            ProcurementApproverId,
            "Satin Alma Talebi",
            "Tedarikci Degerlendirme",
            "Butce Onayi",
            "Tedarikci teklifleri ve uygunluk bilgileri incelenir.",
            "Talep butce kapsaminda sonuca baglanir.",
            12 * 60,
            24 * 60,
            "Satin Alma Talep Sahibi")
    ];

    private enum DemoScenario
    {
        ReviewOverdue,
        ApprovalUpcoming,
        Completed,
        CompletedAlternative,
        Rejected,
        SentBack
    }

    private sealed record WorkflowSpec(
        string Key,
        Guid DefinitionId,
        Guid VersionId,
        Guid CommunityId,
        Guid OwnerId,
        string Name,
        string Description,
        Guid StartFormId,
        Guid ReviewFormId,
        Guid ApprovalFormId,
        Guid ReviewTeamId,
        Guid ApprovalTeamId,
        Guid ReviewRoleId,
        Guid ApprovalRoleId,
        Guid StarterId,
        Guid ReviewerId,
        Guid ApproverId,
        string StartTitle,
        string ReviewTitle,
        string ApprovalTitle,
        string ReviewDescription,
        string ApprovalDescription,
        int ReviewSlaMinutes,
        int ApprovalSlaMinutes,
        string StarterLabel);

    private sealed record SeededProcess(
        ProcessInstance Process,
        IReadOnlyList<Notification> Notifications,
        IReadOnlyList<SystemAuditLog> SystemAuditLogs);
}
