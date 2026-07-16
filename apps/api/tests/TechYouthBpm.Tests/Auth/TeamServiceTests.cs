using Microsoft.Extensions.Configuration;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Teams;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Auth;

public class TeamServiceTests
{
    [Fact]
    public async Task ListAsync_Allows_SuperAdmin_To_View_Teams_Across_Communities()
    {
        await using var db = TestDbFactory.Create();
        TestDbFactory.EnsureCommunityModel(db);
        var defaultTeam = SeedTeam(db, TestDbFactory.CommunityId, "Scout Ekibi");
        var other = SeedOtherCommunity(db);
        var otherTeam = SeedTeam(db, other.CommunityId, "Sevkiyat");
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.ListAsync(
            new TeamSearchRequest { Page = 1, PageSize = 20 },
            SuperAdminDto());

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        Assert.Contains(result.Value!.Items, team => team.Id == defaultTeam.Id);
        Assert.Contains(result.Value.Items, team => team.Id == otherTeam.Id);
    }

    [Fact]
    public async Task ListAsync_Keeps_Community_Admin_Inside_Own_Community()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var ownTeam = SeedTeam(db, TestDbFactory.CommunityId, "Scout Ekibi");
        var other = SeedOtherCommunity(db);
        SeedTeam(db, other.CommunityId, "Sevkiyat");
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var ownResult = await service.ListAsync(new TeamSearchRequest(), TestDbFactory.CommunityAdminDto(admin));
        var otherResult = await service.ListAsync(
            new TeamSearchRequest { CommunityId = other.CommunityId },
            TestDbFactory.CommunityAdminDto(admin));

        Assert.True(ownResult.IsSuccess, string.Join(" | ", ownResult.Errors));
        Assert.Single(ownResult.Value!.Items);
        Assert.Equal(ownTeam.Id, ownResult.Value.Items[0].Id);
        Assert.False(otherResult.IsSuccess);
    }

    [Fact]
    public async Task AddMemberAsync_Rejects_User_From_Another_Community()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var team = SeedTeam(db, TestDbFactory.CommunityId, "Scout Ekibi");
        var other = SeedOtherCommunity(db);
        var externalUser = SeedUserInCommunity(db, other.CommunityId, other.RoleId, "external-user");
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.AddMemberAsync(
            team.Id,
            new AddTeamMemberRequest(externalUser.Id),
            TestDbFactory.CommunityAdminDto(admin));

        Assert.False(result.IsSuccess);
        Assert.Empty(db.TeamMemberships.Where(membership => membership.UserId == externalUser.Id));
    }

    [Fact]
    public async Task Memberships_Support_Multiple_Teams_And_Unassigned_Remains_Virtual()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var multiTeamUser = TestDbFactory.SeedUser(db, Role.User, "multi-team-user");
        var unassignedUser = TestDbFactory.SeedUser(db, Role.User, "unassigned-user");
        var firstTeam = SeedTeam(db, TestDbFactory.CommunityId, "Scout Ekibi");
        var secondTeam = SeedTeam(db, TestDbFactory.CommunityId, "Teknik Degerlendirme");
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var adminDto = TestDbFactory.CommunityAdminDto(admin);

        Assert.True((await service.AddMemberAsync(firstTeam.Id, new AddTeamMemberRequest(multiTeamUser.Id), adminDto)).IsSuccess);
        Assert.True((await service.AddMemberAsync(secondTeam.Id, new AddTeamMemberRequest(multiTeamUser.Id), adminDto)).IsSuccess);

        var firstMembers = await service.ListMembersAsync(firstTeam.Id, new TeamMemberSearchRequest(), adminDto);
        var secondMembers = await service.ListMembersAsync(secondTeam.Id, new TeamMemberSearchRequest(), adminDto);
        var unassigned = await service.ListUnassignedAsync(
            new UnassignedTeamMemberSearchRequest { CommunityId = TestDbFactory.CommunityId, PageSize = 50 },
            adminDto);

        Assert.Contains(firstMembers.Value!.Items, member => member.UserId == multiTeamUser.Id);
        Assert.Contains(secondMembers.Value!.Items, member => member.UserId == multiTeamUser.Id);
        Assert.DoesNotContain(unassigned.Value!.Items, member => member.UserId == multiTeamUser.Id);
        Assert.Contains(unassigned.Value.Items, member => member.UserId == unassignedUser.Id);
        Assert.Empty(db.Teams.Where(team => team.Name == "Takimsiz"));
    }

    [Fact]
    public async Task Team_Lead_Does_Not_Grant_Team_Management_Permission()
    {
        await using var db = TestDbFactory.Create();
        var lead = TestDbFactory.SeedUser(db, Role.User, "team-lead");
        var team = SeedTeam(db, TestDbFactory.CommunityId, "Scout Ekibi");
        db.TeamMemberships.Add(new TeamMembership
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            UserId = lead.Id,
            IsLead = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var leadDto = CommunityUserDto(lead, [PermissionNames.TeamsView]);

        var listResult = await service.ListAsync(new TeamSearchRequest(), leadDto);
        var createResult = await service.CreateAsync(
            new CreateTeamRequest(TestDbFactory.CommunityId, "Yetkisiz Takim", "Should not be created"),
            leadDto);

        Assert.True(listResult.IsSuccess);
        Assert.False(createResult.IsSuccess);
        Assert.DoesNotContain(db.Teams, item => item.Name == "Yetkisiz Takim");
    }

    [Fact]
    public async Task Membership_Mutations_Write_Audit_And_Target_Notifications()
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin, "community-admin");
        var member = TestDbFactory.SeedUser(db, Role.User, "team-member");
        var team = SeedTeam(db, TestDbFactory.CommunityId, "Scout Ekibi");
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var adminDto = TestDbFactory.CommunityAdminDto(admin);

        var added = await service.AddMemberAsync(team.Id, new AddTeamMemberRequest(member.Id), adminDto);
        var promoted = await service.UpdateMemberAsync(team.Id, member.Id, new UpdateTeamMemberRequest(true), adminDto);
        var removed = await service.RemoveMemberAsync(team.Id, member.Id, adminDto);

        Assert.True(added.IsSuccess && promoted.IsSuccess && removed.IsSuccess);
        Assert.Contains(db.Notifications, item => item.UserId == member.Id && item.Type == "Team.MembershipAdded");
        Assert.Contains(db.Notifications, item => item.UserId == member.Id && item.Type == "Team.LeadershipUpdated");
        Assert.Contains(db.Notifications, item => item.UserId == member.Id && item.Type == "Team.MembershipRemoved");
        Assert.Contains(db.SystemAuditLogs, item => item.Action == "Team.MemberAdded" && item.EntityId == member.Id.ToString());
        Assert.Contains(db.SystemAuditLogs, item => item.Action == "Team.MemberUpdated" && item.EntityId == member.Id.ToString());
        Assert.Contains(db.SystemAuditLogs, item => item.Action == "Team.MemberRemoved" && item.EntityId == member.Id.ToString());
        Assert.False(db.TeamMemberships.Single(item => item.TeamId == team.Id && item.UserId == member.Id).IsActive);
    }

    [Fact]
    public async Task Moving_User_To_Another_Community_Deactivates_Old_Team_Memberships()
    {
        await using var db = TestDbFactory.Create();
        var superAdmin = TestDbFactory.SeedSuperAdmin(db, "team-superadmin");
        var member = TestDbFactory.SeedUser(db, Role.User, "moving-user");
        var team = SeedTeam(db, TestDbFactory.CommunityId, "Scout Ekibi");
        db.TeamMemberships.Add(new TeamMembership
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            UserId = member.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        var other = SeedOtherCommunity(db);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var authService = new AuthService(db, new ConfigurationBuilder().Build());

        var result = await authService.UpdateCommunityMembershipAsync(
            other.CommunityId,
            member.Id,
            new UpdateUserMembershipRequest(other.CommunityId, other.RoleId),
            SuperAdminDto(superAdmin.Id));

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        Assert.False(db.TeamMemberships.Single(item => item.TeamId == team.Id && item.UserId == member.Id).IsActive);
    }

    [Fact]
    public async Task ListRosterAsync_Allows_Member_Without_TeamsView_Only_For_Own_Team()
    {
        await using var db = TestDbFactory.Create();
        var member = TestDbFactory.SeedUser(db, Role.User, "roster-member");
        var teammate = TestDbFactory.SeedUser(db, Role.User, "roster-teammate");
        var ownTeam = SeedTeam(db, TestDbFactory.CommunityId, "Scout Ekibi");
        var otherTeam = SeedTeam(db, TestDbFactory.CommunityId, "Mali Isler");
        db.TeamMemberships.AddRange(
            Membership(ownTeam.Id, member.Id),
            Membership(ownTeam.Id, teammate.Id));
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var memberDto = CommunityUserDto(member, []);

        var ownRoster = await service.ListRosterAsync(ownTeam.Id, new TeamMemberSearchRequest(), memberDto);
        var otherRoster = await service.ListRosterAsync(otherTeam.Id, new TeamMemberSearchRequest(), memberDto);

        Assert.True(ownRoster.IsSuccess, string.Join(" | ", ownRoster.Errors));
        Assert.Equal(2, ownRoster.Value!.TotalCount);
        Assert.Contains(ownRoster.Value.Items, item => item.UserId == teammate.Id);
        Assert.False(otherRoster.IsSuccess);
    }

    [Fact]
    public async Task ListUserMembershipsAsync_Uses_Self_Global_And_Community_Management_Scope()
    {
        await using var db = TestDbFactory.Create();
        var communityAdmin = TestDbFactory.SeedUser(db, Role.Admin, "membership-admin");
        var member = TestDbFactory.SeedUser(db, Role.User, "membership-target");
        var team = SeedTeam(db, TestDbFactory.CommunityId, "Teknik Degerlendirme");
        db.TeamMemberships.Add(Membership(team.Id, member.Id, isLead: true));
        var other = SeedOtherCommunity(db);
        var externalUser = SeedUserInCommunity(db, other.CommunityId, other.RoleId, "membership-external");
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var selfResult = await service.ListUserMembershipsAsync(member.Id, CommunityUserDto(member, []));
        var adminResult = await service.ListUserMembershipsAsync(member.Id, TestDbFactory.CommunityAdminDto(communityAdmin));
        var deniedResult = await service.ListUserMembershipsAsync(externalUser.Id, TestDbFactory.CommunityAdminDto(communityAdmin));
        var globalResult = await service.ListUserMembershipsAsync(externalUser.Id, SuperAdminDto());

        Assert.True(selfResult.IsSuccess);
        Assert.True(adminResult.IsSuccess);
        Assert.Single(adminResult.Value!);
        Assert.True(adminResult.Value![0].IsLead);
        Assert.False(deniedResult.IsSuccess);
        Assert.True(globalResult.IsSuccess);
    }

    private static TeamService CreateService(AppDbContext db) =>
        new(db, new SystemAuditService(db), new NotificationService(db));

    private static Team SeedTeam(AppDbContext db, Guid communityId, string name)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            CommunityId = communityId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Description = $"{name} test team",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Teams.Add(team);
        return team;
    }

    private static TeamMembership Membership(Guid teamId, Guid userId, bool isLead = false) => new()
    {
        Id = Guid.NewGuid(),
        TeamId = teamId,
        UserId = userId,
        IsLead = isLead,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static (Guid CommunityId, Guid RoleId) SeedOtherCommunity(AppDbContext db)
    {
        var communityId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        db.Communities.Add(new Community
        {
            Id = communityId,
            Name = $"Other Community {communityId:N}",
            Description = "Second test community",
            InviteCode = $"{communityId:N}"[..5].ToUpperInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.CommunityRoles.Add(new CommunityRole
        {
            Id = roleId,
            CommunityId = communityId,
            Name = "Other User",
            Description = "Other community user role",
            TemplateKey = "other-user",
            IsSystemRole = false,
            CreatedAt = DateTime.UtcNow,
            Permissions = [new CommunityRolePermission { Id = Guid.NewGuid(), Permission = PermissionNames.TeamsView }]
        });
        return (communityId, roleId);
    }

    private static User SeedUserInCommunity(AppDbContext db, Guid communityId, Guid roleId, string username)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = username,
            Email = $"{username}@test.local",
            Password = "test-hash",
            Role = Role.User,
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        user.CommunityMemberships.Add(new UserCommunityMembership
        {
            Id = Guid.NewGuid(),
            CommunityId = communityId,
            CommunityRoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Users.Add(user);
        return user;
    }

    private static UserDto SuperAdminDto(Guid? userId = null) => new(
        userId ?? Guid.NewGuid(),
        "superadmin",
        "Super Admin",
        "superadmin@test.local",
        Role.SuperAdmin,
        UserStatus.Active,
        true,
        false,
        null,
        string.Empty,
        null,
        "SuperAdmin",
        PermissionNames.All);

    private static UserDto CommunityUserDto(User user, IReadOnlyList<string> permissions) => new(
        user.Id,
        user.Username,
        user.DisplayName,
        user.Email,
        Role.User,
        user.Status,
        user.IsEmailVerified,
        false,
        TestDbFactory.CommunityId,
        "Test Community",
        TestDbFactory.UserCommunityRoleId,
        "Test User",
        permissions);
}
