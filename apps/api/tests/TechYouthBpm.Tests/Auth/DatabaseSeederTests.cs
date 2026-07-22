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
        Assert.Contains(memberships, membership => membership.CommunityRole!.Name == "Scout Sorumlusu");
        Assert.Contains(memberships, membership => membership.CommunityRole!.Name == "Teknik Onay Sorumlusu");
        Assert.Contains(memberships, membership => membership.CommunityRole!.Name == "Mali Onay Sorumlusu");
        Assert.Contains(memberships, membership => membership.CommunityRole!.TemplateKey == "read-only");

        var leads = await db.TeamMemberships
            .Where(item => demoUsers.Select(user => user.Id).Contains(item.UserId) && item.IsActive && item.IsLead)
            .ToListAsync();
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
        Assert.Equal(TaskAssignmentType.TeamAndCommunityRole, graphs["Hizli Teknik Onay"].Nodes.Single(node => node.Type == ProcessNodeType.UserTask).Assignment!.Type);
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
}
