using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Domain.Enums;

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
}
