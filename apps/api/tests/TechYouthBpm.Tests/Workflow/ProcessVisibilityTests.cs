using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Workflow;

public class ProcessVisibilityTests
{
    [Fact]
    public async Task ProcessesViewAll_Controls_CommunityWide_Visibility()
    {
        await using var db = TestDbFactory.Create();
        var firstStarter = TestDbFactory.SeedUser(db, Role.User, "first-starter");
        var secondStarter = TestDbFactory.SeedUser(db, Role.User, "second-starter");
        var (firstProcess, _) = TestDbFactory.SeedOpenApproverTask(db, firstStarter);
        var (secondProcess, _) = TestDbFactory.SeedOpenApproverTask(db, secondStarter);
        var service = new ProcessService(
            db,
            new FormService(db),
            new ProcessStateMachine(),
            new SystemAuditService(db));
        var ownOnly = UserWithPermissions(firstStarter, PermissionNames.ProcessesView);
        var viewAll = UserWithPermissions(
            firstStarter,
            PermissionNames.ProcessesView,
            PermissionNames.ProcessesViewAll);

        var ownResults = await service.ListAsync(ownOnly);
        var allResults = await service.ListAsync(viewAll);

        Assert.Single(ownResults);
        Assert.Equal(firstProcess.Id, ownResults[0].Id);
        Assert.Contains(allResults, process => process.Id == firstProcess.Id);
        Assert.Contains(allResults, process => process.Id == secondProcess.Id);
    }

    private static UserDto UserWithPermissions(TechYouthBpm.Domain.Entities.User user, params string[] permissions) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email,
            user.Role,
            user.Status,
            user.IsEmailVerified,
            CommunityId: TestDbFactory.CommunityId,
            CommunityName: "Test Community",
            CommunityRoleId: TestDbFactory.UserCommunityRoleId,
            CommunityRoleName: "Starter",
            Permissions: permissions);
}
