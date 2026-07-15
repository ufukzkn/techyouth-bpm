using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Workflow;
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

            Assert.Equal(8, memberships.Count);
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

            Assert.Equal(8, activeMemberships.Count);
        }
    }

    [Fact]
    public async Task SeedAsync_Creates_A_Valid_Multi_Team_Transfer_Workflow()
    {
        await using var db = TestDbFactory.Create();

        await DatabaseSeeder.SeedAsync(db, seedMockData: true);

        var definition = await db.ProcessDefinitions
            .Include(item => item.Versions)
            .SingleAsync(item => item.Name == "Transfer Talep Akisi");
        var version = Assert.Single(definition.Versions);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
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

        var starter = await db.Users
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
            "{\"talepSahibi\":\"Fatih Terim\",\"oyuncuAdi\":\"Mario Gomez\",\"kulup\":\"Besiktas\",\"pozisyon\":\"Forvet\",\"bonservis\":7500000,\"acilMi\":false}");
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
}
