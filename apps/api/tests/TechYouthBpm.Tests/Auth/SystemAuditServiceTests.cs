using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Audit;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Auth;

public class SystemAuditServiceTests
{
    [Fact]
    public async Task ListAsync_Returns_System_Audit_Logs_For_Admin()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "admin-audit");
        var service = new SystemAuditService(db);
        await service.LogAsync(admin.Id, "Test.Action", "TestEntity", "entity-1", "Test description");
        var adminDto = TestDbFactory.ToDto(admin);

        var result = await service.ListAsync(adminDto, new SystemAuditSearchRequest(Page: 1, PageSize: 5));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalCount);
        var log = Assert.Single(result.Value.Items);
        Assert.Equal("Test.Action", log.Action);
        Assert.Equal(admin.Username, log.ActorUsername);
    }

    [Fact]
    public async Task ListAsync_Rejects_Non_Admin_User()
    {
        await using var db = TestDbFactory.Create();
        var user = TestDbFactory.SeedUser(db, Role.User, "user-audit");
        var service = new SystemAuditService(db);
        await service.LogAsync(user.Id, "Test.Action", "TestEntity", "entity-1", "Test description");
        var userDto = TestDbFactory.ToDto(user);

        var result = await service.ListAsync(userDto, new SystemAuditSearchRequest(Page: 1, PageSize: 5));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ListAsync_Scopes_Community_Admin_To_Its_Own_Community_Audit()
    {
        await using var db = TestDbFactory.Create();
        var communityAdmin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var otherCommunityId = Guid.NewGuid();
        var otherRoleId = Guid.NewGuid();
        var otherUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "other-user",
            DisplayName = "Other User",
            Email = "other-user@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        db.Communities.Add(new Community
        {
            Id = otherCommunityId,
            Name = "Other Community",
            Description = "Other test scope",
            InviteCode = "OTHER",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.CommunityRoles.Add(new CommunityRole
        {
            Id = otherRoleId,
            CommunityId = otherCommunityId,
            Name = "Other Role",
            Description = "Other role",
            TemplateKey = "other-role",
            IsSystemRole = false,
            CreatedAt = DateTime.UtcNow
        });
        otherUser.CommunityMemberships.Add(new UserCommunityMembership
        {
            Id = Guid.NewGuid(),
            CommunityId = otherCommunityId,
            CommunityRoleId = otherRoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Users.Add(otherUser);
        await db.SaveChangesAsync();
        var service = new SystemAuditService(db);
        await service.LogAsync(communityAdmin.Id, "User.AccessUpdated", "User", communityAdmin.Id.ToString(), "Own community action");
        await service.LogAsync(otherUser.Id, "User.AccessUpdated", "User", otherUser.Id.ToString(), "Other community action");

        var result = await service.ListAsync(TestDbFactory.ToDto(communityAdmin), new SystemAuditSearchRequest(Page: 1, PageSize: 10));

        Assert.True(result.IsSuccess);
        var log = Assert.Single(result.Value!.Items);
        Assert.Equal(communityAdmin.Id, log.ActorUserId);
    }

    [Fact]
    public async Task ListAsync_Sorts_By_Action_In_Ascending_Order()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "admin-sort");
        var service = new SystemAuditService(db);
        await service.LogAsync(admin.Id, "Zeta.Action", "TestEntity", "z", "Zeta");
        await service.LogAsync(admin.Id, "Alpha.Action", "TestEntity", "a", "Alpha");

        var result = await service.ListAsync(
            TestDbFactory.ToDto(admin),
            new SystemAuditSearchRequest(Page: 1, PageSize: 10, SortBy: "action", SortDirection: "asc"));

        Assert.True(result.IsSuccess);
        Assert.Equal(["Alpha.Action", "Zeta.Action"], result.Value!.Items.Select(item => item.Action));
    }
}
