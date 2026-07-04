using TechYouthBpm.Application.Auth;
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
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, admin.Email, admin.Role, admin.Status, true);

        var result = await service.ListAsync(adminDto);

        Assert.True(result.IsSuccess);
        var log = Assert.Single(result.Value!);
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
        var userDto = new UserDto(user.Id, user.Username, user.DisplayName, user.Email, user.Role, user.Status, true);

        var result = await service.ListAsync(userDto);

        Assert.False(result.IsSuccess);
    }
}
