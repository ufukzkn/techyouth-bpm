using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Auth;

public class DatabaseSeederTests
{
    [Fact]
    public async Task SeedAsync_Creates_Current_System_Roles_For_Every_Community()
    {
        await using var db = TestDbFactory.Create();

        await DatabaseSeeder.SeedAsync(db, seedMockData: false);

        var communities = db.Communities.ToList();
        Assert.NotEmpty(communities);
        foreach (var community in communities)
        {
            var roleNames = db.CommunityRoles
                .Where(role => role.CommunityId == community.Id && role.IsSystemRole)
                .Select(role => role.Name)
                .ToList();

            Assert.Contains("Atanmadi", roleNames);
            Assert.Contains("Standart Kullanici", roleNames);
            Assert.Contains("Gozlemci", roleNames);
            Assert.DoesNotContain("Lojistik Gorevlisi", roleNames);
            Assert.Equal(7, roleNames.Count);
        }

        Assert.All(
            db.Users.Where(user => user.Role != Role.SuperAdmin),
            user => Assert.Equal(Role.User, user.Role));
    }

    [Fact]
    public async Task SeedAsync_Populates_Five_Communities_With_Scoped_Bpm_Data()
    {
        await using var db = TestDbFactory.Create();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        Assert.Equal(5, db.Communities.Count());
        Assert.Contains(db.Communities, community => community.Name == "Insan Kaynaklari" && community.InviteCode == "IK001");
        Assert.Contains(db.Communities, community => community.Name == "Satin Alma" && community.InviteCode == "SAT01");
        Assert.Contains(db.FormDefinitions, form => form.Name == "Izin ve Uzaktan Calisma Talep Formu");
        Assert.Contains(db.FormDefinitions, form => form.Name == "Satin Alma Talep Formu");
        Assert.Contains(db.ProcessInstances, process => process.CommunityId == db.Communities.Single(community => community.Name == "Insan Kaynaklari").Id);
        Assert.Contains(db.ProcessInstances, process => process.CommunityId == db.Communities.Single(community => community.Name == "Satin Alma").Id);
        Assert.Contains(db.Notifications, notification => notification.UserId != Guid.Empty && notification.Type == "Task.Assigned");

        foreach (var community in db.Communities)
        {
            var memberships = db.UserCommunityMemberships
                .Where(membership => membership.CommunityId == community.Id && membership.IsActive)
                .ToList();

            Assert.Equal(community.Name == "Sportif Faaliyetler" ? 15 : 8, memberships.Count);
            Assert.Contains(
                memberships,
                membership => db.CommunityRoles.Any(role => role.Id == membership.CommunityRoleId && role.TemplateKey == "unassigned"));
        }
    }

    [Fact]
    public async Task SeedAsync_Is_Idempotent_When_Mock_Users_Are_Added_To_An_Existing_Database()
    {
        await using var db = TestDbFactory.Create();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);
        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        Assert.Equal(5, db.Communities.Count());
        Assert.Equal(
            db.Users.Select(user => user.Username).Distinct().Count(),
            db.Users.Count());

        foreach (var community in db.Communities)
        {
            var activeMemberships = db.UserCommunityMemberships
                .Where(membership => membership.CommunityId == community.Id && membership.IsActive)
                .ToList();

            Assert.Equal(community.Name == "Sportif Faaliyetler" ? 15 : 8, activeMemberships.Count);
        }
    }

    [Fact]
    public async Task SeedAsync_Creates_A_Valid_Multi_Team_Transfer_Workflow()
    {
        await using var db = TestDbFactory.Create();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        var definition = await db.ProcessDefinitions
            .Include(item => item.Versions)
            .SingleAsync(item => item.Name == "Transfer Teklif ve Onay Akışı");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var version = definition.Versions.MaxBy(item => item.VersionNumber)!;
        var graph = JsonSerializer.Deserialize<ProcessGraphDto>(version.GraphJson, options)!;
        var validation = await new ProcessGraphValidator(db).ValidateForPublishAsync(
            graph,
            definition.CommunityId,
            version.FormDefinitionVersionId);

        Assert.True(validation.IsSuccess, string.Join(" | ", validation.Errors));
        Assert.Equal(4, graph.Nodes.Count(node => node.Type == ProcessNodeType.TeamSwimlane));
        Assert.Equal(4, graph.Nodes.Count(node => node.Type == ProcessNodeType.UserTask));
        Assert.Contains(graph.Edges, edge =>
            edge.Condition?.Path == "start.bonservis"
            && edge.Condition.Operator == GraphConditionOperator.GreaterThan);
        Assert.Contains(graph.Nodes, node =>
            node.Type == ProcessNodeType.UserTask
            && node.Actions?.Contains(WorkflowAction.Complete) == true);
        Assert.Contains(graph.Nodes, node => node.Key == "financeApproval" && node.RequiresTeamLead);
        Assert.Contains(graph.Nodes, node => node.Key == "transferOperation" && node.RequiresTeamLead);

        var startForm = await db.FormDefinitionVersions
            .AsSplitQuery()
            .Include(formVersion => formVersion.Pages)
            .ThenInclude(page => page.Fields)
            .SingleAsync(formVersion => formVersion.Id == version.FormDefinitionVersionId);
        Assert.Equal(2, startForm.Pages.Count);
        var fieldTypes = startForm.Pages.SelectMany(page => page.Fields).Select(field => field.Type).ToHashSet();
        Assert.Contains(FieldType.Text, fieldTypes);
        Assert.Contains(FieldType.TextArea, fieldTypes);
        Assert.Contains(FieldType.Number, fieldTypes);
        Assert.Contains(FieldType.Email, fieldTypes);
        Assert.Contains(FieldType.Select, fieldTypes);
        Assert.Contains(FieldType.Radio, fieldTypes);
        Assert.Contains(FieldType.Checkbox, fieldTypes);
        Assert.Contains(FieldType.Date, fieldTypes);
        Assert.Contains(FieldType.FileUpload, fieldTypes);

        var starter = await db.Users
            .AsSplitQuery()
            .Include(user => user.CommunityMemberships)
            .ThenInclude(membership => membership.CommunityRole)
            .ThenInclude(role => role!.Permissions)
            .SingleAsync(user => user.Username == "fatih.terim");
        var membership = starter.CommunityMemberships.Single(item => item.IsActive);
        var starterDto = new TechYouthBpm.Application.Auth.UserDto(
            starter.Id,
            starter.Username,
            starter.DisplayName,
            starter.Email,
            starter.Role,
            starter.Status,
            starter.IsEmailVerified,
            starter.MustChangePassword,
            membership.CommunityId,
            "Sportif Faaliyetler",
            membership.CommunityRoleId,
            membership.CommunityRole!.Name,
            membership.CommunityRole.Permissions.Select(permission => permission.Permission).ToArray());
        using var formData = JsonDocument.Parse(
            """
            {
              "talepSahibi": "Fatih Terim",
              "iletisimEmail": "fatih.terim@techyouth.local",
              "oyuncuAdi": "Mario Gomez",
              "kulup": "Beşiktaş",
              "pozisyon": "Forvet",
              "bonservis": 7500000,
              "paraBirimi": "EUR",
              "teklifTarihi": "2026-07-17",
              "acilMi": false,
              "gerekce": "",
              "teklifDosyasi": {
                "name": "transfer-teklifi.pdf",
                "size": 245760,
                "type": "application/pdf",
                "lastModified": 1784246400000
              },
              "veriOnayi": true
            }
            """);
        var started = await new ProcessService(
                db,
                new FormService(db),
                new ProcessStateMachine(),
                new SystemAuditService(db))
            .StartVersionAsync(new(version.Id, formData.RootElement.Clone()), starterDto);

        Assert.True(started.IsSuccess, string.Join(" | ", started.Errors));
        var firstTask = Assert.Single(started.Value!.Tasks.Where(task => task.Status == ProcessTaskStatus.Open));
        Assert.Equal("scoutReview", firstTask.NodeKey);
        Assert.Equal(TaskPriority.High, firstTask.Priority);
        Assert.NotNull(firstTask.TaskForm);
        Assert.Equal("Scout Degerlendirme Formu", firstTask.TaskForm.FormName);
        Assert.Equal(3, firstTask.TaskForm.Pages.SelectMany(page => page.Fields).Count());
    }

    [Fact]
    public async Task SeedAsync_Creates_A_Lead_Gated_Urgent_Logistics_Workflow()
    {
        await using var db = TestDbFactory.Create();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        var definition = await db.ProcessDefinitions
            .Include(item => item.Versions)
            .SingleAsync(item => item.Name == "Acil Sevkiyat ve Teslimat Akışı");
        var version = definition.Versions.MaxBy(item => item.VersionNumber)!;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var graph = JsonSerializer.Deserialize<ProcessGraphDto>(version.GraphJson, options)!;
        var validation = await new ProcessGraphValidator(db).ValidateForPublishAsync(
            graph,
            definition.CommunityId,
            version.FormDefinitionVersionId);

        Assert.True(validation.IsSuccess, string.Join(" | ", validation.Errors));
        Assert.Contains(graph.Nodes, node => node.Type == ProcessNodeType.ExclusiveGateway);
        Assert.Contains(graph.Edges, edge =>
            edge.Condition?.Path == "start.acilSevkiyat"
            && edge.Condition.Operator == GraphConditionOperator.Equals);
        Assert.Contains(graph.Edges, edge => edge.Source == "urgencyGateway" && edge.IsDefault);
        Assert.All(
            graph.Nodes.Where(node => node.Type == ProcessNodeType.UserTask),
            node => Assert.True(node.RequiresTeamLead));

        var startForm = await db.FormDefinitionVersions
            .AsSplitQuery()
            .Include(formVersion => formVersion.Pages)
            .ThenInclude(page => page.Fields)
            .SingleAsync(formVersion => formVersion.Id == version.FormDefinitionVersionId);
        Assert.Equal(2, startForm.Pages.Count);
        Assert.Contains(startForm.Pages.SelectMany(page => page.Fields), field => field.Type == FieldType.FileUpload);
    }

    [Fact]
    public async Task SeedAsync_Creates_Five_Versioned_Workflows_With_Deadline_Scenarios()
    {
        await using var db = TestDbFactory.Create();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        var workflowNames = new[]
        {
            "Transfer Teklif ve Onay Akışı",
            "Acil Sevkiyat ve Teslimat Akışı",
            "Siparis Karsilama Akisi",
            "Izin ve Uzaktan Calisma Akisi",
            "Talep Tedarikci ve Butce Akisi"
        };
        var definitions = await db.ProcessDefinitions
            .Where(definition => workflowNames.Contains(definition.Name))
            .Include(definition => definition.Versions)
            .ToListAsync();

        Assert.Equal(5, definitions.Count);
        foreach (var definition in definitions)
        {
            var versionIds = definition.Versions.Select(version => version.Id).ToArray();
            var processes = await db.ProcessInstances
                .Where(process => process.ProcessDefinitionVersionId.HasValue
                    && versionIds.Contains(process.ProcessDefinitionVersionId.Value))
                .AsSplitQuery()
                .Include(process => process.Tasks)
                .Include(process => process.StepExecutions)
                .ToListAsync();

            Assert.True(processes.Count >= 5, $"{definition.Name} has only {processes.Count} demo processes.");
            Assert.Contains(processes, process => process.Status == ProcessStatus.Completed);
            Assert.Contains(processes, process => process.Status == ProcessStatus.Rejected);
            Assert.Contains(processes, process => process.Status == ProcessStatus.InProgress);
            Assert.Contains(processes.SelectMany(process => process.Tasks), task => task.DueAt < DateTime.UtcNow);
            Assert.Contains(processes.SelectMany(process => process.Tasks), task => task.DueAt > DateTime.UtcNow);
            Assert.All(
                processes.SelectMany(process => process.Tasks),
                task => Assert.NotNull(task.FormDefinitionVersionId));
            Assert.Contains(
                processes.SelectMany(process => process.StepExecutions),
                step => step.Action == WorkflowAction.SendBack);
        }

        var legacyIds = Enumerable.Range(1, 14)
            .Select(index => Guid.Parse($"cccccccc-0000-0000-0000-{index:000000000000}"))
            .ToArray();
        Assert.False(await db.ProcessInstances.AnyAsync(process => legacyIds.Contains(process.Id)));
    }

    [Fact]
    public async Task SeedAsync_Preserves_A_User_Created_Process_On_The_Next_Run()
    {
        await using var db = TestDbFactory.Create();
        await DatabaseSeeder.SeedAsync(db, seedMockData: true);
        var form = await db.FormDefinitions.FirstAsync();
        var starterId = await db.UserCommunityMemberships
            .Where(membership => membership.CommunityId == form.CommunityId && membership.IsActive)
            .Select(membership => membership.UserId)
            .FirstAsync();
        var userProcessId = Guid.NewGuid();
        db.ProcessInstances.Add(new TechYouthBpm.Domain.Entities.ProcessInstance
        {
            Id = userProcessId,
            FormDefinitionId = form.Id,
            CommunityId = form.CommunityId,
            StartedByUserId = starterId,
            Status = ProcessStatus.InProgress,
            FormDataJson = "{}",
            VariablesJson = "{}",
            StartedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        Assert.True(await db.ProcessInstances.AnyAsync(process => process.Id == userProcessId));
    }

    [Fact]
    public async Task SeedAsync_Resolves_System_Roles_By_Template_When_An_Older_Database_Uses_Different_Ids()
    {
        await using var db = TestDbFactory.Create();
        await DatabaseSeeder.SeedAsync(db, seedMockData: false);

        var logisticsCommunityId = await db.Communities
            .Where(community => community.Name == "Lojistik")
            .Select(community => community.Id)
            .SingleAsync();
        var originalRole = await db.CommunityRoles
            .Include(role => role.Permissions)
            .SingleAsync(role => role.CommunityId == logisticsCommunityId
                && role.TemplateKey == "approver");
        var replacementRoleId = Guid.NewGuid();
        var replacementRole = new CommunityRole
        {
            Id = replacementRoleId,
            CommunityId = logisticsCommunityId,
            Name = originalRole.Name,
            Description = originalRole.Description,
            TemplateKey = "approver",
            IsSystemRole = true,
            CreatedAt = originalRole.CreatedAt,
            Permissions = originalRole.Permissions.Select(permission => new CommunityRolePermission
            {
                Id = Guid.NewGuid(),
                Permission = permission.Permission
            }).ToList()
        };

        originalRole.Name = "Legacy Logistics Approver";
        originalRole.TemplateKey = "legacy-approver";
        originalRole.IsSystemRole = false;
        db.CommunityRoles.Add(replacementRole);
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        Assert.True(await db.ProcessTasks.AnyAsync(task =>
            task.ProcessInstance!.CommunityId == logisticsCommunityId
            && task.CandidateCommunityRoleId == replacementRoleId));
    }

    [Fact]
    public async Task SeedAsync_Adds_Idempotent_Sportif_Quick_Workflow_Fixtures_Without_Removing_User_Data()
    {
        await using var db = TestDbFactory.Create();
        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        var community = await db.Communities.SingleAsync(item => item.Name == "Sportif Faaliyetler");
        var demoUsers = await db.Users
            .Where(user => user.Username.StartsWith("sport."))
            .OrderBy(user => user.Username)
            .ToListAsync();
        Assert.Equal(
            ["sport.admin", "sport.approver", "sport.finance", "sport.operations", "sport.scout", "sport.starter", "sport.viewer"],
            demoUsers.Select(user => user.Username));

        var memberships = await db.UserCommunityMemberships
            .Include(item => item.CommunityRole)
            .Where(item => demoUsers.Select(user => user.Id).Contains(item.UserId) && item.IsActive)
            .ToListAsync();
        Assert.All(memberships, membership => Assert.Equal(community.Id, membership.CommunityId));
        var commonApproverRole = await db.CommunityRoles.SingleAsync(role =>
            role.CommunityId == community.Id && role.IsSystemRole && role.TemplateKey == "approver");
        var teamSpecialists = demoUsers
            .Where(user => user.Username is "sport.scout" or "sport.approver" or "sport.finance" or "sport.operations")
            .Select(user => user.Id)
            .ToHashSet();
        Assert.All(
            memberships.Where(membership => teamSpecialists.Contains(membership.UserId)),
            membership => Assert.Equal(commonApproverRole.Id, membership.CommunityRoleId));
        Assert.Contains(memberships, membership => membership.CommunityRole!.TemplateKey == "read-only");
        var legacyRoleIds = new[]
        {
            Guid.Parse("20202020-0000-0000-0000-000000000030"),
            Guid.Parse("20202020-0000-0000-0000-000000000031"),
            Guid.Parse("20202020-0000-0000-0000-000000000032"),
            Guid.Parse("20202020-0000-0000-0000-000000000033")
        };
        Assert.False(await db.CommunityRoles.AnyAsync(role => legacyRoleIds.Contains(role.Id)));

        var leads = await db.TeamMemberships
            .Where(item => demoUsers.Select(user => user.Id).Contains(item.UserId) && item.IsActive && item.IsLead)
            .ToListAsync();
        Assert.Contains(await db.Teams.Where(team => team.CommunityId == community.Id).Select(team => team.Name).ToListAsync(), name => name == "Teknik Değerlendirme");
        Assert.Contains(await db.Teams.Where(team => team.CommunityId == community.Id).Select(team => team.Name).ToListAsync(), name => name == "Mali İşler");
        Assert.Contains(leads, item => item.UserId == demoUsers.Single(user => user.Username == "sport.scout").Id);
        Assert.Contains(leads, item => item.UserId == demoUsers.Single(user => user.Username == "sport.approver").Id);
        Assert.Contains(leads, item => item.UserId == demoUsers.Single(user => user.Username == "sport.finance").Id);
        Assert.Contains(leads, item => item.UserId == demoUsers.Single(user => user.Username == "sport.operations").Id);

        var definitions = await db.ProcessDefinitions
            .Include(item => item.Versions)
            .Where(item => item.Name.StartsWith("Hizli "))
            .ToListAsync();
        Assert.Equal(3, definitions.Count);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var graphs = definitions.ToDictionary(
            definition => definition.Name,
            definition => JsonSerializer.Deserialize<ProcessGraphDto>(definition.Versions.Single().GraphJson, options)!);
        Assert.Equal(TaskAssignmentType.Team, graphs["Hizli Scout Incelemesi"].Nodes.Single(node => node.Type == ProcessNodeType.UserTask).Assignment!.Type);
        var technicalTask = graphs["Hizli Teknik Onay"].Nodes.Single(node => node.Type == ProcessNodeType.UserTask);
        Assert.Equal("İlk İnceleme", technicalTask.Title);
        Assert.Equal(TaskAssignmentType.TeamAndCommunityRole, technicalTask.Assignment!.Type);
        Assert.Equal(commonApproverRole.Id, technicalTask.Assignment.CommunityRoleId);
        Assert.Equal("Scout Değerlendirmesi", graphs["Hizli Scout Incelemesi"].Nodes.Single(node => node.Type == ProcessNodeType.UserTask).Title);
        Assert.Equal("Mali Lider Onayı", graphs["Hizli Lider Onayi"].Nodes.Single(node => node.Type == ProcessNodeType.UserTask).Title);
        Assert.True(graphs["Hizli Lider Onayi"].Nodes.Single(node => node.Type == ProcessNodeType.UserTask).RequiresTeamLead);
        var validator = new ProcessGraphValidator(db);
        foreach (var definition in definitions)
        {
            var version = definition.Versions.Single();
            var validation = await validator.ValidateForPublishAsync(
                graphs[definition.Name],
                definition.CommunityId,
                version.FormDefinitionVersionId);
            Assert.True(validation.IsSuccess, string.Join(" | ", validation.Errors));
        }

        var quickVersionIds = definitions.SelectMany(definition => definition.Versions).Select(version => version.Id).ToArray();
        var fixtures = await db.ProcessInstances
            .Where(process => process.ProcessDefinitionVersionId.HasValue && quickVersionIds.Contains(process.ProcessDefinitionVersionId.Value))
            .AsSplitQuery()
            .Include(process => process.Tasks)
            .Include(process => process.StepExecutions)
            .Include(process => process.AuditLogs)
            .ToListAsync();
        Assert.Equal(12, fixtures.Count);
        Assert.Equal(6, fixtures.SelectMany(process => process.Tasks).Count(task => task.Status is ProcessTaskStatus.Open or ProcessTaskStatus.Claimed));
        Assert.Equal(6, fixtures.SelectMany(process => process.Tasks).Count(task => task.Status == ProcessTaskStatus.Completed));
        Assert.All(fixtures, process => Assert.NotEmpty(process.StepExecutions));
        Assert.All(fixtures, process => Assert.NotEmpty(process.AuditLogs));

        var candidateIdsByWorkflow = new Dictionary<string, Guid>
        {
            ["Hizli Scout Incelemesi"] = demoUsers.Single(user => user.Username == "sport.scout").Id,
            ["Hizli Teknik Onay"] = demoUsers.Single(user => user.Username == "sport.approver").Id,
            ["Hizli Lider Onayi"] = demoUsers.Single(user => user.Username == "sport.finance").Id
        };
        var workflowsByVersion = definitions
            .SelectMany(definition => definition.Versions.Select(version => new { version.Id, definition.Name }))
            .ToDictionary(item => item.Id, item => item.Name);
        foreach (var task in fixtures.SelectMany(process => process.Tasks).Where(task => task.Status is ProcessTaskStatus.Open or ProcessTaskStatus.Claimed))
        {
            var process = fixtures.Single(item => item.Id == task.ProcessInstanceId);
            var candidateId = candidateIdsByWorkflow[workflowsByVersion[process.ProcessDefinitionVersionId!.Value]];
            var candidateMembership = await db.UserCommunityMemberships.SingleAsync(item =>
                item.UserId == candidateId && item.CommunityId == community.Id && item.IsActive);
            Assert.True(await db.CommunityRolePermissions.AnyAsync(item =>
                item.CommunityRoleId == candidateMembership.CommunityRoleId
                && item.Permission == PermissionNames.TasksAct));
            if (task.CandidateTeamId.HasValue)
            {
                var teamMembership = await db.TeamMemberships.SingleAsync(item =>
                    item.UserId == candidateId && item.TeamId == task.CandidateTeamId.Value && item.IsActive);
                if (task.RequiresTeamLead)
                {
                    Assert.True(teamMembership.IsLead);
                }
            }
            if (task.CandidateCommunityRoleId.HasValue)
            {
                Assert.Equal(task.CandidateCommunityRoleId, candidateMembership.CommunityRoleId);
            }
        }

        var userCreatedId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userCreatedId,
            Username = "preserved-user",
            DisplayName = "Preserved User",
            Email = "preserved-user@example.test",
            Password = "test-hash",
            Role = Role.User,
            Status = UserStatus.Active,
            IsEmailVerified = true
        });
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        Assert.True(await db.Users.AnyAsync(user => user.Id == userCreatedId));
        Assert.Equal(12, await db.ProcessInstances.CountAsync(process =>
            process.ProcessDefinitionVersionId.HasValue && quickVersionIds.Contains(process.ProcessDefinitionVersionId.Value)));
    }

    [Fact]
    public async Task SeedAsync_Adds_Two_Idempotent_Action_Labs_With_All_Assignments_And_Actions()
    {
        await using var db = TestDbFactory.Create();
        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        var names = new[] { "Transfer Aksiyon Laboratuvarı", "Operasyon Aksiyon Laboratuvarı" };
        var definitions = await db.ProcessDefinitions
            .Where(definition => names.Contains(definition.Name))
            .Include(definition => definition.Versions)
            .ToListAsync();
        Assert.Equal(2, definitions.Count);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var validator = new ProcessGraphValidator(db);
        foreach (var definition in definitions)
        {
            var version = Assert.Single(definition.Versions);
            var graph = JsonSerializer.Deserialize<ProcessGraphDto>(version.GraphJson, options)!;
            var validation = await validator.ValidateForPublishAsync(graph, definition.CommunityId, version.FormDefinitionVersionId);

            Assert.True(validation.IsSuccess, string.Join(" | ", validation.Errors));
            Assert.Contains(graph.Nodes, node => node.Assignment?.Type == TaskAssignmentType.Team && !node.RequiresTeamLead);
            Assert.Contains(graph.Nodes, node => node.Assignment?.Type == TaskAssignmentType.TeamAndCommunityRole);
            Assert.Contains(graph.Nodes, node => node.Assignment?.Type == TaskAssignmentType.SpecificUser);
            Assert.Contains(graph.Nodes, node => node.Assignment?.Type == TaskAssignmentType.Team && node.RequiresTeamLead);
            var actions = graph.Nodes
                .Where(node => node.Type == ProcessNodeType.UserTask)
                .SelectMany(node => node.Actions ?? [])
                .ToHashSet();
            Assert.All(
                new[] { WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.Complete, WorkflowAction.SendBack, WorkflowAction.Escalate },
                action => Assert.Contains(action, actions));
        }

        var versionIds = definitions.SelectMany(definition => definition.Versions).Select(version => version.Id).ToArray();
        var instances = await db.ProcessInstances
            .Where(process => process.ProcessDefinitionVersionId.HasValue && versionIds.Contains(process.ProcessDefinitionVersionId.Value))
            .AsSplitQuery()
            .Include(process => process.Tasks)
            .Include(process => process.StepExecutions)
            .Include(process => process.AuditLogs)
            .ToListAsync();
        Assert.Equal(10, instances.Count);
        foreach (var versionId in versionIds)
        {
            var workflowInstances = instances.Where(process => process.ProcessDefinitionVersionId == versionId).ToList();
            Assert.Equal(5, workflowInstances.Count);
            var actions = workflowInstances.SelectMany(process => process.AuditLogs).Select(audit => audit.Action).ToHashSet();
            Assert.All(
                new[] { WorkflowAction.Approve, WorkflowAction.Reject, WorkflowAction.Complete, WorkflowAction.SendBack, WorkflowAction.Escalate },
                action => Assert.Contains(action, actions));
        }

        Assert.Equal(2, instances.Count(process => process.AuditLogs.Any(audit => audit.Action == WorkflowAction.SendBack)
            && process.Tasks.Any(task => task.NodeKey == "teamPool" && task.Attempt == 2 && task.Status == ProcessTaskStatus.Open)));
        Assert.Equal(2, instances.Count(process => process.AuditLogs.Any(audit => audit.Action == WorkflowAction.Escalate)
            && process.Tasks.Any(task => task.NodeKey == "teamLead" && task.Status == ProcessTaskStatus.Open && task.RequiresTeamLead)));

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        Assert.Equal(2, await db.ProcessDefinitions.CountAsync(definition => names.Contains(definition.Name)));
        Assert.Equal(10, await db.ProcessInstances.CountAsync(process =>
            process.ProcessDefinitionVersionId.HasValue && versionIds.Contains(process.ProcessDefinitionVersionId.Value)));
    }

    [Fact]
    public async Task SeedAsync_Adds_Missing_Quick_Workflow_Versions_To_Existing_Definitions()
    {
        await using var db = TestDbFactory.Create();
        await DatabaseSeeder.SeedAsync(db, seedMockData: false);

        var communityId = await db.Communities
            .Where(community => community.Name == "Sportif Faaliyetler")
            .Select(community => community.Id)
            .SingleAsync();
        var ownerId = await db.Users
            .Where(user => user.Role == Role.SuperAdmin)
            .Select(user => user.Id)
            .SingleAsync();
        var existingDefinitionIds = new[]
        {
            Guid.Parse("16161616-0000-0000-0000-000000000001"),
            Guid.Parse("16161616-0000-0000-0000-000000000002"),
            Guid.Parse("16161616-0000-0000-0000-000000000003")
        };

        db.ProcessDefinitions.AddRange(existingDefinitionIds.Select((id, index) => new ProcessDefinition
        {
            Id = id,
            CommunityId = communityId,
            Name = $"Legacy quick workflow {index + 1}",
            Description = "Existing definition without a published quick-demo version.",
            CreatedByUserId = ownerId,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        }));
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        var expectedVersionIds = new[]
        {
            Guid.Parse("16161616-2000-0000-0000-000000000001"),
            Guid.Parse("16161616-2000-0000-0000-000000000002"),
            Guid.Parse("16161616-2000-0000-0000-000000000003")
        };
        Assert.Equal(3, await db.ProcessDefinitionVersions.CountAsync(version =>
            expectedVersionIds.Contains(version.Id)));
    }

    [Fact]
    public async Task SeedAsync_Upgrades_Only_Known_Sportif_Legacy_Role_References()
    {
        await using var db = TestDbFactory.Create();
        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        var community = await db.Communities.SingleAsync(item => item.Name == "Sportif Faaliyetler");
        var commonApproverRole = await db.CommunityRoles.SingleAsync(role =>
            role.CommunityId == community.Id && role.IsSystemRole && role.TemplateKey == "approver");
        var legacyRoleId = Guid.Parse("20202020-0000-0000-0000-000000000032");
        var legacyRole = new CommunityRole
        {
            Id = legacyRoleId,
            CommunityId = community.Id,
            Name = "Mali Onay Sorumlusu",
            Description = "Legacy deterministic demo role.",
            TemplateKey = "custom-finance-approver",
            IsSystemRole = false,
            CreatedAt = DateTime.UtcNow,
            Permissions =
            [
                new CommunityRolePermission { Id = Guid.NewGuid(), Permission = PermissionNames.ProcessesView },
                new CommunityRolePermission { Id = Guid.NewGuid(), Permission = PermissionNames.TasksView },
                new CommunityRolePermission { Id = Guid.NewGuid(), Permission = PermissionNames.TasksAct }
            ]
        };
        db.CommunityRoles.Add(legacyRole);

        var financeUserId = await db.Users
            .Where(user => user.Username == "sport.finance")
            .Select(user => user.Id)
            .SingleAsync();
        var financeMembership = await db.UserCommunityMemberships.SingleAsync(membership =>
            membership.UserId == financeUserId && membership.CommunityId == community.Id && membership.IsActive);
        financeMembership.CommunityRoleId = legacyRoleId;

        var technicalDefinition = await db.ProcessDefinitions
            .Include(definition => definition.Versions)
            .SingleAsync(definition => definition.Name == "Hizli Teknik Onay");
        var currentVersion = technicalDefinition.Versions.Single();
        var legacyVersionId = Guid.Parse("16161616-1000-0000-0000-000000000002");
        db.ProcessDefinitionVersions.Add(new ProcessDefinitionVersion
        {
            Id = legacyVersionId,
            ProcessDefinitionId = technicalDefinition.Id,
            VersionNumber = currentVersion.VersionNumber + 1,
            Status = DefinitionVersionStatus.Published,
            FormDefinitionVersionId = currentVersion.FormDefinitionVersionId,
            GraphJson = currentVersion.GraphJson.Replace(commonApproverRole.Id.ToString(), legacyRoleId.ToString(), StringComparison.OrdinalIgnoreCase),
            CreatedByUserId = currentVersion.CreatedByUserId,
            CreatedAt = currentVersion.CreatedAt,
            PublishedByUserId = currentVersion.PublishedByUserId,
            PublishedAt = currentVersion.PublishedAt
        });

        var deterministicProcess = await db.ProcessInstances
            .Include(process => process.Tasks)
            .SingleAsync(process => process.ProcessDefinitionVersionId == currentVersion.Id
                && process.Tasks.Any(task => task.NodeKey == "technicalApproval" && task.Status == ProcessTaskStatus.Open));
        deterministicProcess.ProcessDefinitionVersionId = legacyVersionId;
        var deterministicTask = Assert.Single(deterministicProcess.Tasks);
        deterministicTask.AssignedCommunityRoleId = legacyRoleId;
        deterministicTask.CandidateCommunityRoleId = legacyRoleId;

        var userCreatedRole = new CommunityRole
        {
            Id = Guid.NewGuid(),
            CommunityId = community.Id,
            Name = "Kullaniciya Ait Es Rol",
            Description = "Must not be merged by permission equality.",
            TemplateKey = "user-created-same-permissions",
            IsSystemRole = false,
            CreatedAt = DateTime.UtcNow
        };
        db.CommunityRoles.Add(userCreatedRole);
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        Assert.Equal(commonApproverRole.Id, await db.UserCommunityMemberships
            .Where(membership => membership.UserId == financeUserId && membership.IsActive)
            .Select(membership => membership.CommunityRoleId)
            .SingleAsync());
        Assert.Equal(commonApproverRole.Id, await db.ProcessTasks
            .Where(task => task.Id == deterministicTask.Id)
            .Select(task => task.CandidateCommunityRoleId)
            .SingleAsync());
        Assert.Equal(currentVersion.Id, await db.ProcessInstances
            .Where(process => process.Id == deterministicProcess.Id)
            .Select(process => process.ProcessDefinitionVersionId)
            .SingleAsync());
        Assert.False(await db.CommunityRoles.AnyAsync(role => role.Id == legacyRoleId));
        Assert.True(await db.CommunityRoles.AnyAsync(role => role.Id == userCreatedRole.Id));
    }
}
