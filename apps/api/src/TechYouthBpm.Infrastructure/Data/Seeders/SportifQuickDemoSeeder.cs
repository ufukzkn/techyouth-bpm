using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Security;
using TechYouthBpm.Infrastructure.Services;
using static TechYouthBpm.Infrastructure.Data.Seeders.DemoSeedIds;

namespace TechYouthBpm.Infrastructure.Data.Seeders;

/// <summary>
/// Adds small, predictable Sportif Faaliyetler scenarios without touching user-created records.
/// The seed is intentionally independent from the larger transfer showcase so each assignment rule
/// can be demonstrated with one user task and two clear outcomes.
/// </summary>
internal static class SportifQuickDemoSeeder
{
    private static readonly IReadOnlyList<DemoAccount> Accounts =
    [
        new(SportDemoAdminId, "sport.admin", "Sportif Demo Admin", "community-admin", null, false),
        new(SportDemoStarterId, "sport.starter", "Sportif Demo Baslatici", "process-starter", null, false),
        new(SportDemoScoutId, "sport.scout", "Sportif Demo Scout", "approver", SportScoutTeamId, true),
        new(SportDemoApproverId, "sport.approver", "Sportif Demo Teknik Onay", "approver", SportTechnicalTeamId, true),
        new(SportDemoFinanceId, "sport.finance", "Sportif Demo Mali Onay", "approver", SportFinanceTeamId, true),
        new(SportDemoOperationsId, "sport.operations", "Sportif Demo Transfer Operasyon", "approver", SportTransferTeamId, true),
        new(SportDemoViewerId, "sport.viewer", "Sportif Demo Gozlemci", "read-only", null, false)
    ];

    private static readonly IReadOnlyList<FormSpec> Forms =
    [
        new(SportQuickScoutStartFormId, SportQuickScoutStartFormVersionId, "Hizli Scout Talebi", "Scout ekibi icin kisa inceleme talebi.", "oyuncuAdi", "Oyuncu adi"),
        new(SportQuickScoutTaskFormId, SportQuickScoutTaskFormVersionId, "Hizli Scout Karari", "Scout karari icin kisa task formu.", "scoutNotu", "Scout notu"),
        new(SportQuickTechnicalStartFormId, SportQuickTechnicalStartFormVersionId, "Hizli Teknik Talep", "Teknik ekip icin kisa onay talebi.", "talepBasligi", "Talep basligi"),
        new(SportQuickTechnicalTaskFormId, SportQuickTechnicalTaskFormVersionId, "Hizli Teknik Karar", "Teknik onay task formu.", "teknikNot", "Teknik not"),
        new(SportQuickLeadStartFormId, SportQuickLeadStartFormVersionId, "Hizli Mali Talep", "Mali isler lideri icin kisa talep.", "butceKalemi", "Butce kalemi"),
        new(SportQuickLeadTaskFormId, SportQuickLeadTaskFormVersionId, "Hizli Mali Karar", "Takim sorumlusu mali karar formu.", "maliNot", "Mali not")
    ];

    private static readonly IReadOnlyList<Guid> LegacyRoleIds =
    [
        SportScoutLeadRoleId,
        SportTechnicalApproverRoleId,
        SportFinanceApproverRoleId,
        SportOperationsRoleId
    ];

    private static readonly IReadOnlyList<WorkflowSpec> WorkflowTemplates =
    [
        new(
            "quick-scout",
            SportQuickScoutWorkflowId,
            SportQuickScoutWorkflowVersionV2Id,
            "Hizli Scout Incelemesi",
            "Scout Ekibi icin iki cikisli kisa inceleme akisi.",
            SportQuickScoutStartFormId,
            SportQuickScoutTaskFormVersionId,
            "scoutReview",
            "Scout Değerlendirmesi",
            TaskAssignmentType.Team,
            SportScoutTeamId,
            null,
            false,
            SportDemoStarterId,
            SportDemoScoutId),
        new(
            "quick-technical",
            SportQuickTechnicalWorkflowId,
            SportQuickTechnicalWorkflowVersionV2Id,
            "Hizli Teknik Onay",
            "Teknik takim ve role atanan iki cikisli kisa onay akisi.",
            SportQuickTechnicalStartFormId,
            SportQuickTechnicalTaskFormVersionId,
            "technicalApproval",
            "İlk İnceleme",
            TaskAssignmentType.TeamAndCommunityRole,
            SportTechnicalTeamId,
            SportApproverRoleId,
            false,
            SportDemoStarterId,
            SportDemoApproverId),
        new(
            "quick-lead",
            SportQuickLeadWorkflowId,
            SportQuickLeadWorkflowVersionV2Id,
            "Hizli Lider Onayi",
            "Mali Isler takim sorumlusuna atanan iki cikisli kisa onay akisi.",
            SportQuickLeadStartFormId,
            SportQuickLeadTaskFormVersionId,
            "financeLeadApproval",
            "Mali Lider Onayı",
            TaskAssignmentType.Team,
            SportFinanceTeamId,
            null,
            true,
            SportDemoStarterId,
            SportDemoFinanceId)
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var sportApproverRoleId = await EnsureRolesAsync(db, cancellationToken);
        var workflows = ResolveWorkflowRoles(sportApproverRoleId);
        await EnsureAccountsAsync(db, cancellationToken);
        await EnsureFormsAsync(db, cancellationToken);
        var versions = await EnsureWorkflowsAsync(db, workflows, cancellationToken);
        await UpgradeLegacySeedDataAsync(db, workflows, versions, cancellationToken);
        await EnsureScenarioProcessesAsync(db, workflows, versions, cancellationToken);
        await RetireLegacyRolesAsync(db, cancellationToken);
    }

    private static async Task<Guid> EnsureRolesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var approverRoleId = await db.CommunityRoles
            .Where(role => role.CommunityId == SportCommunityId)
            .Where(role => role.TemplateKey == "approver" && role.IsSystemRole)
            .Select(role => role.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (approverRoleId == Guid.Empty)
        {
            throw new InvalidOperationException("Sportif Faaliyetler icin Onay Sorumlusu sistem rolu bulunamadi.");
        }

        return approverRoleId;
    }

    private static IReadOnlyList<WorkflowSpec> ResolveWorkflowRoles(Guid sportApproverRoleId) =>
        WorkflowTemplates
            .Select(workflow => workflow.CommunityRoleId == SportApproverRoleId
                ? workflow with { CommunityRoleId = sportApproverRoleId }
                : workflow)
            .ToArray();

    private static async Task EnsureAccountsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existingUsers = await db.Users
            .Select(user => new { user.Id, user.Username, user.Email })
            .ToListAsync(cancellationToken);
        var userIds = existingUsers.Select(user => user.Id).ToHashSet();
        var usernames = existingUsers.Select(user => user.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = existingUsers.Select(user => user.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addableAccounts = Accounts
            .Where(account => !userIds.Contains(account.UserId)
                && !usernames.Contains(account.Username)
                && !emails.Contains($"{account.Username}@techyouth.local"))
            .ToArray();
        if (addableAccounts.Length > 0)
        {
            db.Users.AddRange(addableAccounts.Select(account => new User
            {
                Id = account.UserId,
                Username = account.Username,
                DisplayName = account.DisplayName,
                Email = $"{account.Username}@techyouth.local",
                Password = PasswordHasher.Hash("sport123"),
                Role = Role.User,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-11)
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        var roleIdsByTemplate = await db.CommunityRoles
            .Where(role => role.CommunityId == SportCommunityId)
            .Select(role => new { role.Id, role.TemplateKey })
            .ToDictionaryAsync(role => role.TemplateKey, role => role.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var presentAccountIds = (await db.Users
                .Where(user => Accounts.Select(account => account.UserId).Contains(user.Id))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var activeMemberships = await db.UserCommunityMemberships
            .Where(membership => presentAccountIds.Contains(membership.UserId) && membership.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var account in Accounts.Where(account => presentAccountIds.Contains(account.UserId)))
        {
            if (!roleIdsByTemplate.TryGetValue(account.RoleTemplateKey, out var roleId))
            {
                throw new InvalidOperationException($"Sportif quick demo role '{account.RoleTemplateKey}' was not found.");
            }

            var membership = activeMemberships.SingleOrDefault(item => item.UserId == account.UserId);
            if (membership is null)
            {
                db.UserCommunityMemberships.Add(new UserCommunityMembership
                {
                    Id = StableGuid($"sportif-quick-membership:{account.UserId}"),
                    UserId = account.UserId,
                    CommunityId = SportCommunityId,
                    CommunityRoleId = roleId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                });
            }
            else if (membership.CommunityId == SportCommunityId)
            {
                membership.CommunityRoleId = roleId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var existingTeamMemberships = await db.TeamMemberships
            .Where(membership => Accounts.Select(account => account.UserId).Contains(membership.UserId))
            .ToListAsync(cancellationToken);
        foreach (var account in Accounts.Where(account => account.TeamId.HasValue && presentAccountIds.Contains(account.UserId)))
        {
            var teamId = account.TeamId.GetValueOrDefault();
            var membership = existingTeamMemberships.SingleOrDefault(item =>
                item.TeamId == teamId && item.UserId == account.UserId);
            if (membership is null)
            {
                db.TeamMemberships.Add(new TeamMembership
                {
                    Id = StableGuid($"sportif-quick-team:{account.TeamId}:{account.UserId}"),
                    TeamId = teamId,
                    UserId = account.UserId,
                    IsLead = account.IsLead,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    UpdatedAt = DateTime.UtcNow.AddDays(-10)
                });
            }
            else
            {
                membership.IsActive = true;
                membership.IsLead = account.IsLead;
                membership.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureFormsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var ids = Forms.Select(form => form.FormId).ToArray();
        var existingFormIds = (await db.FormDefinitions
                .Where(form => ids.Contains(form.Id))
                .Select(form => form.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var now = DateTime.UtcNow;
        foreach (var form in Forms.Where(form => !existingFormIds.Contains(form.FormId)))
        {
            db.FormDefinitions.Add(new FormDefinition
            {
                Id = form.FormId,
                Name = form.Name,
                Description = form.Description,
                CommunityId = SportCommunityId,
                CreatedByUserId = SportDemoAdminId,
                CreatedAt = now.AddDays(-9)
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var versionIds = Forms.Select(form => form.VersionId).ToArray();
        var existingVersionIds = (await db.FormDefinitionVersions
                .Where(version => versionIds.Contains(version.Id))
                .Select(version => version.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        foreach (var form in Forms.Where(form => !existingVersionIds.Contains(form.VersionId)))
        {
            db.FormDefinitionVersions.Add(new FormDefinitionVersion
            {
                Id = form.VersionId,
                FormDefinitionId = form.FormId,
                VersionNumber = 1,
                Status = DefinitionVersionStatus.Published,
                CreatedByUserId = SportDemoAdminId,
                CreatedAt = now.AddDays(-9),
                PublishedByUserId = SportDemoAdminId,
                PublishedAt = now.AddDays(-9),
                Pages =
                [
                    new FormPageDefinition
                    {
                        Id = StableGuid($"sportif-quick-page:{form.VersionId}"),
                        Key = "request",
                        Title = form.Name,
                        Description = form.Description,
                        SortOrder = 1,
                        Fields =
                        [
                            new FormVersionFieldDefinition
                            {
                                Id = StableGuid($"sportif-quick-field:{form.VersionId}:{form.FieldKey}"),
                                Key = form.FieldKey,
                                Label = form.FieldLabel,
                                Type = FieldType.Text,
                                Required = true,
                                SortOrder = 1,
                                OptionsJson = "[]"
                            }
                        ]
                    }
                ]
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyDictionary<string, ProcessDefinitionVersion>> EnsureWorkflowsAsync(
        AppDbContext db,
        IReadOnlyList<WorkflowSpec> workflows,
        CancellationToken cancellationToken)
    {
        var definitionIds = workflows.Select(workflow => workflow.DefinitionId).ToArray();
        var definitions = await db.ProcessDefinitions
            .Where(definition => definitionIds.Contains(definition.Id))
            .Include(definition => definition.Versions)
            .ToDictionaryAsync(definition => definition.Id, cancellationToken);
        var now = DateTime.UtcNow;

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
                    CreatedAt = now.AddDays(-8)
                };
                definitions.Add(workflow.DefinitionId, definition);
                db.ProcessDefinitions.Add(definition);
            }

            if (definition.Versions.Any(version => version.Id == workflow.VersionId))
            {
                continue;
            }

            var startVersionId = Forms.Single(form => form.FormId == workflow.StartFormId).VersionId;
            var version = new ProcessDefinitionVersion
            {
                Id = workflow.VersionId,
                ProcessDefinitionId = definition.Id,
                ProcessDefinition = definition,
                VersionNumber = definition.Versions.Count == 0 ? 1 : definition.Versions.Max(version => version.VersionNumber) + 1,
                Status = DefinitionVersionStatus.Published,
                FormDefinitionVersionId = startVersionId,
                GraphJson = JsonHelpers.Serialize(BuildGraph(workflow, startVersionId)),
                CreatedByUserId = SportDemoAdminId,
                CreatedAt = now.AddDays(-8),
                PublishedByUserId = SportDemoAdminId,
                PublishedAt = now.AddDays(-8)
            };

            // Existing definitions need an explicit Added state because deterministic GUIDs
            // otherwise make EF treat the new version as an update to an existing row.
            db.ProcessDefinitionVersions.Add(version);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return await db.ProcessDefinitionVersions
            .Where(version => workflows.Select(workflow => workflow.VersionId).Contains(version.Id))
            .ToDictionaryAsync(
                version => workflows.Single(workflow => workflow.VersionId == version.Id).Key,
                cancellationToken);
    }

    private static async Task UpgradeLegacySeedDataAsync(
        AppDbContext db,
        IReadOnlyList<WorkflowSpec> workflows,
        IReadOnlyDictionary<string, ProcessDefinitionVersion> versions,
        CancellationToken cancellationToken)
    {
        var workflowByProcessId = workflows
            .SelectMany(workflow => Enum.GetValues<Scenario>()
                .Select(scenario => new { ProcessId = ProcessId(workflow.Key, scenario), Workflow = workflow }))
            .ToDictionary(item => item.ProcessId, item => item.Workflow);
        var processIds = workflowByProcessId.Keys.ToArray();
        var processes = await db.ProcessInstances
            .Where(process => processIds.Contains(process.Id))
            .AsSplitQuery()
            .Include(process => process.Tasks)
            .Include(process => process.StepExecutions)
            .ToListAsync(cancellationToken);

        foreach (var process in processes)
        {
            var workflow = workflowByProcessId[process.Id];
            var version = versions[workflow.Key];

            // These IDs belong only to deterministic demo scenarios. User-created instances stay pinned.
            process.ProcessDefinitionVersionId = version.Id;
            process.FormDefinitionId = workflow.StartFormId;
            process.FormDefinitionVersionId = version.FormDefinitionVersionId;

            foreach (var task in process.Tasks)
            {
                task.Title = workflow.TaskTitle;
                task.AssignmentType = workflow.AssignmentType;
                task.CandidateTeamId = workflow.TeamId;
                task.AssignedCommunityRoleId = workflow.CommunityRoleId;
                task.CandidateCommunityRoleId = workflow.CommunityRoleId;
                task.RequiresTeamLead = workflow.RequiresTeamLead;
            }

            foreach (var step in process.StepExecutions.Where(step => step.NodeKey == workflow.TaskNodeKey))
            {
                step.NodeTitle = workflow.TaskTitle;
                step.AssignmentType = workflow.AssignmentType;
                step.TeamNameSnapshot = TeamName(workflow.TeamId);
                step.CommunityRoleNameSnapshot = workflow.CommunityRoleId is null
                    ? string.Empty
                    : RoleName(workflow.CommunityRoleId.Value);
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task RetireLegacyRolesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var legacyRoles = await db.CommunityRoles
            .Where(role => role.CommunityId == SportCommunityId && LegacyRoleIds.Contains(role.Id))
            .ToListAsync(cancellationToken);

        foreach (var role in legacyRoles)
        {
            var hasMembershipReference = await db.UserCommunityMemberships
                .AnyAsync(membership => membership.CommunityRoleId == role.Id, cancellationToken);
            var hasTaskReference = await db.ProcessTasks
                .AnyAsync(task => task.AssignedCommunityRoleId == role.Id
                    || task.CandidateCommunityRoleId == role.Id, cancellationToken);

            if (!hasMembershipReference && !hasTaskReference)
            {
                db.CommunityRoles.Remove(role);
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static ProcessGraphDto BuildGraph(WorkflowSpec workflow, Guid startFormVersionId) =>
        new(
            "1.0",
            [
                new ProcessNodeDto("start", ProcessNodeType.Start, "Yeni Talep", startFormVersionId, PositionX: 80, PositionY: 120, Width: 160, Height: 72),
                new ProcessNodeDto(
                    workflow.TaskNodeKey,
                    ProcessNodeType.UserTask,
                    workflow.TaskTitle,
                    workflow.TaskFormVersionId,
                    TaskPriority.Normal,
                    [WorkflowAction.Approve, WorkflowAction.Reject],
                    new TaskAssignmentDto(workflow.AssignmentType, TeamId: workflow.TeamId, CommunityRoleId: workflow.CommunityRoleId),
                    PositionX: 340,
                    PositionY: 108,
                    Width: 240,
                    Height: 100,
                    Description: workflow.Description,
                    SlaDurationMinutes: 240,
                    RequiresTeamLead: workflow.RequiresTeamLead),
                new ProcessNodeDto("completed", ProcessNodeType.CompletedEnd, "Tamamlandi", PositionX: 700, PositionY: 60, Width: 160, Height: 72),
                new ProcessNodeDto("rejected", ProcessNodeType.RejectedEnd, "Reddedildi", PositionX: 700, PositionY: 200, Width: 160, Height: 72)
            ],
            [
                new ProcessEdgeDto("start", workflow.TaskNodeKey, Order: 0, Label: "Talebi gonder"),
                new ProcessEdgeDto(workflow.TaskNodeKey, "completed", WorkflowAction.Approve, Order: 1, Label: "Onayla"),
                new ProcessEdgeDto(workflow.TaskNodeKey, "rejected", WorkflowAction.Reject, Order: 2, Label: "Reddet")
            ]);

    private static async Task EnsureScenarioProcessesAsync(
        AppDbContext db,
        IReadOnlyList<WorkflowSpec> workflows,
        IReadOnlyDictionary<string, ProcessDefinitionVersion> versions,
        CancellationToken cancellationToken)
    {
        var desiredIds = workflows
            .SelectMany(workflow => Enum.GetValues<Scenario>().Select(scenario => ProcessId(workflow.Key, scenario)))
            .ToArray();
        var existingIds = (await db.ProcessInstances
                .Where(process => desiredIds.Contains(process.Id))
                .Select(process => process.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var workflow in workflows)
        {
            foreach (var scenario in Enum.GetValues<Scenario>())
            {
                var processId = ProcessId(workflow.Key, scenario);
                if (existingIds.Contains(processId))
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
        Scenario scenario)
    {
        var now = DateTime.UtcNow;
        var processId = ProcessId(workflow.Key, scenario);
        var startedAt = scenario switch
        {
            Scenario.Open => now.AddHours(-3),
            Scenario.Claimed => now.AddHours(-5),
            Scenario.Completed => now.AddDays(-3),
            Scenario.Rejected => now.AddDays(-2),
            _ => now
        };
        var taskCreatedAt = startedAt.AddMinutes(5);
        DateTime? completedAt = scenario is Scenario.Completed or Scenario.Rejected
            ? taskCreatedAt.AddMinutes(40)
            : null;
        var completed = scenario is Scenario.Completed or Scenario.Rejected;
        var action = scenario == Scenario.Rejected ? WorkflowAction.Reject : WorkflowAction.Approve;
        var taskStatus = scenario switch
        {
            Scenario.Open => ProcessTaskStatus.Open,
            Scenario.Claimed => ProcessTaskStatus.Claimed,
            _ => ProcessTaskStatus.Completed
        };
        var currentNode = completed ? (scenario == Scenario.Rejected ? "rejected" : "completed") : workflow.TaskNodeKey;
        var processStatus = scenario switch
        {
            Scenario.Completed => ProcessStatus.Completed,
            Scenario.Rejected => ProcessStatus.Rejected,
            _ => ProcessStatus.InProgress
        };
        var taskId = StableGuid($"sportif-quick-task:{workflow.Key}:{scenario}");
        var output = new Dictionary<string, object?>
        {
            ["karar"] = scenario == Scenario.Rejected ? "Reddedildi" : "Onaylandi",
            ["not"] = $"{workflow.TaskTitle} demo sonucu"
        };
        var task = new ProcessTask
        {
            Id = taskId,
            ProcessInstanceId = processId,
            AssignedRole = Role.User,
            AssignedCommunityRoleId = workflow.CommunityRoleId,
            NodeKey = workflow.TaskNodeKey,
            Attempt = 1,
            Title = workflow.TaskTitle,
            Priority = TaskPriority.Normal,
            AssignmentType = workflow.AssignmentType,
            CandidateTeamId = workflow.TeamId,
            CandidateCommunityRoleId = workflow.CommunityRoleId,
            ClaimedByUserId = scenario == Scenario.Claimed ? workflow.EligibleUserId : null,
            ClaimedAt = scenario == Scenario.Claimed ? taskCreatedAt.AddMinutes(15) : null,
            ClaimVersion = StableGuid($"sportif-quick-claim:{workflow.Key}:{scenario}"),
            FormDefinitionVersionId = workflow.TaskFormVersionId,
            RequiredPermission = PermissionNames.TasksAct,
            Status = taskStatus,
            AvailableActionsJson = JsonHelpers.Serialize(new[] { WorkflowAction.Approve, WorkflowAction.Reject }),
            CreatedAt = taskCreatedAt,
            DueAt = taskCreatedAt.AddHours(4),
            RequiresTeamLead = workflow.RequiresTeamLead,
            CompletedAt = completedAt,
            CompletedByUserId = completed ? workflow.EligibleUserId : null,
            CompletedAction = completed ? action : null,
            CompletionNote = completed ? $"{workflow.TaskTitle} demo karari kaydedildi." : string.Empty
        };
        var startData = new Dictionary<string, object?>
        {
            ["demo"] = true,
            ["workflow"] = workflow.Name,
            ["scenario"] = scenario.ToString()
        };
        var steps = new List<ProcessStepExecution>
        {
            Step(processId, "start", "Yeni Talep", ProcessNodeType.Start, ProcessStepStatus.Completed, startedAt, startedAt, SportDemoStarterId, WorkflowAction.Start, startData)
        };
        if (completed)
        {
            steps.Add(Step(
                processId,
                workflow.TaskNodeKey,
                workflow.TaskTitle,
                ProcessNodeType.UserTask,
                ProcessStepStatus.Completed,
                taskCreatedAt,
                completedAt,
                workflow.EligibleUserId,
                action,
                output,
                workflow));
            steps.Add(Step(
                processId,
                currentNode,
                scenario == Scenario.Rejected ? "Reddedildi" : "Tamamlandi",
                scenario == Scenario.Rejected ? ProcessNodeType.RejectedEnd : ProcessNodeType.CompletedEnd,
                ProcessStepStatus.Completed,
                completedAt!.Value,
                completedAt,
                workflow.EligibleUserId,
                null,
                null));
        }
        else
        {
            steps.Add(Step(
                processId,
                workflow.TaskNodeKey,
                workflow.TaskTitle,
                ProcessNodeType.UserTask,
                ProcessStepStatus.Active,
                taskCreatedAt,
                null,
                null,
                null,
                null,
                workflow));
        }

        var audits = new List<AuditLog>
        {
            new()
            {
                Id = StableGuid($"sportif-quick-audit:{workflow.Key}:{scenario}:start"),
                ProcessInstanceId = processId,
                UserId = SportDemoStarterId,
                Action = WorkflowAction.Start,
                FromStatus = ProcessStatus.Pending,
                ToStatus = ProcessStatus.InProgress,
                CreatedAt = startedAt,
                Note = "Hizli demo sureci baslatildi."
            }
        };
        if (completed)
        {
            audits.Add(new AuditLog
            {
                Id = StableGuid($"sportif-quick-audit:{workflow.Key}:{scenario}:decision"),
                ProcessInstanceId = processId,
                UserId = workflow.EligibleUserId,
                Action = action,
                FromStatus = ProcessStatus.InProgress,
                ToStatus = processStatus,
                CreatedAt = completedAt!.Value,
                Note = $"{workflow.TaskTitle} demo aksiyonu tamamlandi."
            });
        }

        var process = new ProcessInstance
        {
            Id = processId,
            FormDefinitionId = workflow.StartFormId,
            FormDefinitionVersionId = version.FormDefinitionVersionId,
            ProcessDefinitionVersionId = version.Id,
            CommunityId = SportCommunityId,
            StartedByUserId = SportDemoStarterId,
            Status = processStatus,
            FormDataJson = JsonHelpers.Serialize(startData),
            VariablesJson = JsonHelpers.Serialize(new Dictionary<string, object?>
            {
                ["start"] = startData,
                ["steps"] = completed ? new Dictionary<string, object?> { [workflow.TaskNodeKey] = output } : new Dictionary<string, object?>()
            }),
            CurrentNodeKey = currentNode,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Tasks = [task],
            StepExecutions = steps,
            AuditLogs = audits
        };

        var notifications = new List<Notification>();
        if (scenario is Scenario.Open or Scenario.Claimed)
        {
            notifications.Add(new Notification
            {
                Id = StableGuid($"sportif-quick-notification:{workflow.Key}:{scenario}:task"),
                UserId = workflow.EligibleUserId,
                CommunityId = SportCommunityId,
                Type = "Task.Assigned",
                Title = "Yeni demo gorevi",
                Message = $"{workflow.TaskTitle} gorevi aksiyonunuzu bekliyor.",
                EntityType = "ProcessTask",
                EntityId = taskId.ToString(),
                CreatedAt = taskCreatedAt
            });
        }
        else
        {
            notifications.Add(new Notification
            {
                Id = StableGuid($"sportif-quick-notification:{workflow.Key}:{scenario}:outcome"),
                UserId = SportDemoStarterId,
                CommunityId = SportCommunityId,
                Type = scenario == Scenario.Rejected ? "Process.Rejected" : "Process.Completed",
                Title = scenario == Scenario.Rejected ? "Demo sureci reddedildi" : "Demo sureci tamamlandi",
                Message = $"{workflow.Name} sonucu kaydedildi.",
                EntityType = "ProcessInstance",
                EntityId = processId.ToString(),
                CreatedAt = completedAt!.Value
            });
        }

        var systemAudits = new List<SystemAuditLog>
        {
            new()
            {
                Id = StableGuid($"sportif-quick-system-audit:{workflow.Key}:{scenario}:process"),
                ActorUserId = SportDemoStarterId,
                CommunityId = SportCommunityId,
                Category = "processes",
                Action = "Process.Seeded",
                EntityType = "ProcessInstance",
                EntityId = processId.ToString(),
                Description = $"{workflow.Name} icin {scenario} demo sureci olusturuldu.",
                CreatedAt = startedAt
            }
        };
        if (scenario == Scenario.Claimed)
        {
            systemAudits.Add(new SystemAuditLog
            {
                Id = StableGuid($"sportif-quick-system-audit:{workflow.Key}:{scenario}:claim"),
                ActorUserId = workflow.EligibleUserId,
                CommunityId = SportCommunityId,
                Category = "tasks",
                Action = "Task.Claimed",
                EntityType = "ProcessTask",
                EntityId = taskId.ToString(),
                Description = $"{workflow.TaskTitle} demo gorevi sahiplenildi.",
                CreatedAt = task.ClaimedAt!.Value
            });
        }

        return new SeededScenario(process, notifications, systemAudits);
    }

    private static ProcessStepExecution Step(
        Guid processId,
        string nodeKey,
        string nodeTitle,
        ProcessNodeType nodeType,
        ProcessStepStatus status,
        DateTime enteredAt,
        DateTime? completedAt,
        Guid? completedByUserId,
        WorkflowAction? action,
        object? output,
        WorkflowSpec? workflow = null) =>
        new()
        {
            Id = StableGuid($"sportif-quick-step:{processId}:{nodeKey}:{enteredAt.Ticks}"),
            ProcessInstanceId = processId,
            NodeKey = nodeKey,
            NodeTitle = nodeTitle,
            NodeType = nodeType,
            AssignmentType = workflow?.AssignmentType,
            TeamNameSnapshot = workflow is null ? string.Empty : TeamName(workflow.TeamId),
            CommunityRoleNameSnapshot = workflow?.CommunityRoleId is null ? string.Empty : RoleName(workflow.CommunityRoleId.Value),
            Attempt = 1,
            Status = status,
            EnteredAt = enteredAt,
            CompletedAt = completedAt,
            CompletedByUserId = completedByUserId,
            Action = action,
            Note = action is null ? string.Empty : $"{nodeTitle} demo aksiyonu.",
            OutputJson = JsonHelpers.Serialize(output ?? new { })
        };

    private static string TeamName(Guid teamId) => teamId switch
    {
        var id when id == SportScoutTeamId => "Scout Ekibi",
        var id when id == SportTechnicalTeamId => "Teknik Değerlendirme",
        var id when id == SportFinanceTeamId => "Mali İşler",
        var id when id == SportTransferTeamId => "Transfer Operasyon",
        _ => string.Empty
    };

    private static string RoleName(Guid _) => "Onay Sorumlusu";

    private static Guid ProcessId(string key, Scenario scenario) =>
        StableGuid($"sportif-quick-process:{key}:{scenario}");

    private static Guid StableGuid(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }

    private sealed record DemoAccount(
        Guid UserId,
        string Username,
        string DisplayName,
        string RoleTemplateKey,
        Guid? TeamId,
        bool IsLead);

    private sealed record FormSpec(
        Guid FormId,
        Guid VersionId,
        string Name,
        string Description,
        string FieldKey,
        string FieldLabel);

    private sealed record WorkflowSpec(
        string Key,
        Guid DefinitionId,
        Guid VersionId,
        string Name,
        string Description,
        Guid StartFormId,
        Guid TaskFormVersionId,
        string TaskNodeKey,
        string TaskTitle,
        TaskAssignmentType AssignmentType,
        Guid TeamId,
        Guid? CommunityRoleId,
        bool RequiresTeamLead,
        Guid StarterId,
        Guid EligibleUserId);

    private sealed record SeededScenario(
        ProcessInstance Process,
        IReadOnlyList<Notification> Notifications,
        IReadOnlyList<SystemAuditLog> SystemAudits);

    private enum Scenario
    {
        Open,
        Claimed,
        Completed,
        Rejected
    }
}
