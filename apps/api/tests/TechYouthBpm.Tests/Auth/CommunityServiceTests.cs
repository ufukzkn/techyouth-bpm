using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Auth;

public class CommunityServiceTests
{
    [Fact]
    public async Task ListRolesAsync_Allows_SuperAdmin_To_See_Community_Roles()
    {
        await using var db = TestDbFactory.Create();
        TestDbFactory.EnsureCommunityModel(db);
        await db.SaveChangesAsync();
        var service = new CommunityService(db, null!, new SystemAuditService(db));
        var superAdmin = new UserDto(Guid.NewGuid(), "superadmin", "Super Admin", "super@test.local", Role.SuperAdmin, UserStatus.Active, true);

        var result = await service.ListRolesAsync(TestDbFactory.CommunityId, superAdmin);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!, role => role.Name == "Topluluk Admin");
        Assert.Contains(result.Value!, role => role.Name == "Atanmadi");
    }

    [Fact]
    public async Task UpdateAsync_Allows_Community_Admin_To_Deactivate_Own_Community_And_Revokes_Sessions()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var member = TestDbFactory.SeedUser(db, Role.User, "community-member");
        db.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            Token = "community-session-token",
            UserId = member.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();

        var service = new CommunityService(db, null!, new SystemAuditService(db));
        var adminDto = TestDbFactory.ToDto(admin);
        var result = await service.UpdateAsync(
            TestDbFactory.CommunityId,
            new UpdateCommunityRequest("Test Community", "Unit test community", "TEST1", false),
            adminDto);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
        Assert.NotNull(db.UserSessions.Single(session => session.UserId == member.Id).RevokedAt);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Community_Admin_Reactivation()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var community = db.Communities.Single(item => item.Id == TestDbFactory.CommunityId);
        community.IsActive = false;
        await db.SaveChangesAsync();

        var service = new CommunityService(db, null!, new SystemAuditService(db));
        var result = await service.UpdateAsync(
            TestDbFactory.CommunityId,
            new UpdateCommunityRequest("Test Community", "Unit test community", "TEST1", true),
            TestDbFactory.ToDto(admin));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ListRolesAsync_Rejects_Community_Admin_From_Another_Community()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var otherCommunity = SeedOtherCommunity(db);
        await db.SaveChangesAsync();
        var service = new CommunityService(db, null!, new SystemAuditService(db));

        var result = await service.ListRolesAsync(otherCommunity.Id, TestDbFactory.ToDto(admin));

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot view community roles", result.Errors.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateMembershipAsync_Rejects_Community_Admin_Moving_User_From_Another_Community()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var otherCommunity = SeedOtherCommunity(db);
        var externalUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "external-member",
            DisplayName = "External Member",
            Email = "external-member@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        externalUser.CommunityMemberships.Add(new UserCommunityMembership
        {
            Id = Guid.NewGuid(),
            CommunityId = otherCommunity.Id,
            CommunityRoleId = otherCommunity.UserRoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Users.Add(externalUser);
        await db.SaveChangesAsync();
        var service = new CommunityService(db, null!, new SystemAuditService(db));

        var result = await service.UpdateMembershipAsync(
            TestDbFactory.CommunityId,
            externalUser.Id,
            new UpdateUserMembershipRequest(TestDbFactory.CommunityId, TestDbFactory.UserCommunityRoleId),
            TestDbFactory.ToDto(admin));

        Assert.False(result.IsSuccess);
        Assert.Contains("outside this community", result.Errors.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(otherCommunity.Id, db.UserCommunityMemberships.Single(membership => membership.UserId == externalUser.Id && membership.IsActive).CommunityId);
    }

    [Fact]
    public async Task CreateRoleAsync_Allows_Empty_Custom_Template()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var service = new CommunityService(db, null!, new SystemAuditService(db));

        var result = await service.CreateRoleAsync(
            TestDbFactory.CommunityId,
            new CreateCommunityRoleRequest("Ayakkabici", "Topluluga ozel bos rol", "custom", []),
            TestDbFactory.ToDto(admin));

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        Assert.False(result.Value!.IsSystemRole);
        Assert.Empty(result.Value.Permissions);
    }

    [Fact]
    public async Task CreateRoleAsync_Stores_Ready_Template_Copy_As_Custom_Role()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var service = new CommunityService(db, null!, new SystemAuditService(db));

        var result = await service.CreateRoleAsync(
            TestDbFactory.CommunityId,
            new CreateCommunityRoleRequest("Surec Baslatici*", "Hazir sablondan duzenlendi.", "process-starter", []),
            TestDbFactory.ToDto(admin));

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        Assert.False(result.Value!.IsSystemRole);
        Assert.Equal("custom", result.Value.TemplateKey);
        Assert.Contains(PermissionNames.ProcessesStart, result.Value.Permissions);
    }

    [Fact]
    public async Task DeleteRoleAsync_Moves_Members_To_Replacement_Role()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var member = TestDbFactory.SeedUser(db, Role.User, "community-member");
        var service = new CommunityService(db, null!, new SystemAuditService(db));
        var createdRole = await service.CreateRoleAsync(
            TestDbFactory.CommunityId,
            new CreateCommunityRoleRequest("Ayakkabici", "Topluluga ozel rol", "custom", [PermissionNames.ProcessesView]),
            TestDbFactory.ToDto(admin));
        Assert.True(createdRole.IsSuccess, string.Join(" | ", createdRole.Errors));

        foreach (var membership in db.UserCommunityMemberships.Where(membership => membership.UserId == member.Id))
        {
            membership.IsActive = false;
        }
        db.UserCommunityMemberships.Add(new UserCommunityMembership
        {
            Id = Guid.NewGuid(),
            UserId = member.Id,
            CommunityId = TestDbFactory.CommunityId,
            CommunityRoleId = createdRole.Value!.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var deleted = await service.DeleteRoleAsync(
            TestDbFactory.CommunityId,
            createdRole.Value.Id,
            new DeleteCommunityRoleRequest(TestDbFactory.UnassignedCommunityRoleId),
            TestDbFactory.ToDto(admin));

        Assert.True(deleted.IsSuccess, string.Join(" | ", deleted.Errors));
        Assert.DoesNotContain(db.CommunityRoles, role => role.Id == createdRole.Value.Id);
        Assert.Equal(
            TestDbFactory.UnassignedCommunityRoleId,
            db.UserCommunityMemberships.Single(membership => membership.UserId == member.Id && membership.IsActive).CommunityRoleId);
    }

    private static (Guid Id, Guid UserRoleId) SeedOtherCommunity(AppDbContext db)
    {
        var communityId = Guid.NewGuid();
        var userRoleId = Guid.NewGuid();
        db.Communities.Add(new Community
        {
            Id = communityId,
            Name = "Other Community",
            Description = "Second test community",
            InviteCode = "OTHER",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.CommunityRoles.Add(new CommunityRole
        {
            Id = userRoleId,
            CommunityId = communityId,
            Name = "Other User",
            Description = "Other community user role",
            TemplateKey = "other-user",
            IsSystemRole = false,
            CreatedAt = DateTime.UtcNow,
            Permissions = [new CommunityRolePermission { Id = Guid.NewGuid(), Permission = PermissionNames.ProcessesView }]
        });

        return (communityId, userRoleId);
    }
}
