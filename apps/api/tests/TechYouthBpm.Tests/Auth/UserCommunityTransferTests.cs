using Microsoft.Extensions.Configuration;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Auth;

public class UserCommunityTransferTests
{
    [Fact]
    public async Task Transfer_Is_Atomic_And_Revokes_Sessions()
    {
        await using var db = TestDbFactory.Create();
        var superAdmin = TestDbFactory.SeedSuperAdmin(db, "transfer-superadmin");
        var member = TestDbFactory.SeedUser(db, Role.User, "transfer-member");
        var team = new Team
        {
            Id = Guid.NewGuid(),
            CommunityId = TestDbFactory.CommunityId,
            Name = "Scout Ekibi",
            NormalizedName = "SCOUT EKIBI",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var teamMembership = new TeamMembership
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            UserId = member.Id,
            IsActive = true,
            IsLead = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var target = SeedTargetCommunity(db);
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            UserId = member.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            RememberedDevice = true
        };
        var refresh = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            UserId = member.Id,
            UserSessionId = session.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        db.AddRange(team, teamMembership, session, refresh);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new AuthService(db, new ConfigurationBuilder().Build());
        var currentUser = SuperAdminDto(superAdmin);

        var preview = await service.PreviewCommunityTransferAsync(
            member.Id,
            new CommunityTransferPreviewRequest
            {
                TargetCommunityId = target.CommunityId,
                TargetCommunityRoleId = target.RoleId
            },
            currentUser);
        var result = await service.TransferCommunityAsync(
            member.Id,
            new CommunityTransferRequest(target.CommunityId, target.RoleId),
            currentUser);

        Assert.True(preview.IsSuccess && preview.Value!.CanTransfer);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        Assert.False(db.TeamMemberships.Single(item => item.Id == teamMembership.Id).IsActive);
        Assert.False(db.UserCommunityMemberships.Single(item =>
            item.UserId == member.Id && item.CommunityId == TestDbFactory.CommunityId).IsActive);
        Assert.True(db.UserCommunityMemberships.Single(item =>
            item.UserId == member.Id && item.CommunityId == target.CommunityId).IsActive);
        Assert.NotNull(db.UserSessions.Single(item => item.Id == session.Id).RevokedAt);
        Assert.NotNull(db.RefreshTokens.Single(item => item.Id == refresh.Id).RevokedAt);
        Assert.Contains(db.Notifications, item =>
            item.UserId == member.Id && item.Type == "User.CommunityTransferred");
        Assert.Contains(db.SystemAuditLogs, item =>
            item.ActorUserId == superAdmin.Id
            && item.EntityId == member.Id.ToString()
            && item.Action == "User.CommunityTransferred");
    }

    [Fact]
    public async Task Transfer_With_Direct_Active_Task_Changes_Nothing()
    {
        await using var db = TestDbFactory.Create();
        var superAdmin = TestDbFactory.SeedSuperAdmin(db, "blocking-superadmin");
        var member = TestDbFactory.SeedUser(db, Role.User, "blocking-member");
        var target = SeedTargetCommunity(db);
        var seeded = TestDbFactory.SeedOpenApproverTask(db, member);
        seeded.Task.AssignedUserId = member.Id;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new AuthService(db, new ConfigurationBuilder().Build());

        var result = await service.TransferCommunityAsync(
            member.Id,
            new CommunityTransferRequest(target.CommunityId, target.RoleId),
            SuperAdminDto(superAdmin));

        Assert.False(result.IsSuccess);
        Assert.True(db.UserCommunityMemberships.Single(item =>
            item.UserId == member.Id && item.CommunityId == TestDbFactory.CommunityId).IsActive);
        Assert.DoesNotContain(db.UserCommunityMemberships, item =>
            item.UserId == member.Id && item.CommunityId == target.CommunityId);
        Assert.DoesNotContain(db.Notifications, item =>
            item.UserId == member.Id && item.Type == "User.CommunityTransferred");
    }

    private static (Guid CommunityId, Guid RoleId) SeedTargetCommunity(TechYouthBpm.Infrastructure.Data.AppDbContext db)
    {
        var communityId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        db.Communities.Add(new Community
        {
            Id = communityId,
            Name = $"Target {communityId:N}",
            InviteCode = communityId.ToString("N")[..5].ToUpperInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.CommunityRoles.Add(new CommunityRole
        {
            Id = roleId,
            CommunityId = communityId,
            Name = "Standart Kullanici",
            TemplateKey = "standard-user",
            IsSystemRole = true,
            CreatedAt = DateTime.UtcNow
        });
        return (communityId, roleId);
    }

    private static UserDto SuperAdminDto(User user) =>
        new(user.Id, user.Username, user.DisplayName, user.Email, Role.SuperAdmin, UserStatus.Active, true);
}
