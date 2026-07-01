using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Auth;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_Returns_Raw_Token_But_Stores_Hashed_Session_Token()
    {
        await using var db = TestDbFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin User",
            Password = "admin123",
            Role = Role.Admin
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        var result = await service.LoginAsync(new LoginRequest("admin", "admin123"));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!.Token);
        var storedSession = Assert.Single(db.UserSessions);
        Assert.NotEqual(result.Value.Token, storedSession.Token);
        Assert.Equal(64, storedSession.Token.Length);
    }

    [Fact]
    public async Task LoginAsync_Upgrades_Plain_Text_Password_To_Hash()
    {
        await using var db = TestDbFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "user",
            DisplayName = "Process Starter",
            Password = "user123",
            Role = Role.User
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        var result = await service.LoginAsync(new LoginRequest("user", "user123"));

        Assert.True(result.IsSuccess);
        Assert.StartsWith("pbkdf2:v1:", db.Users.Single().Password, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginAsync_Rejects_Invalid_Password()
    {
        await using var db = TestDbFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "approver",
            DisplayName = "Process Approver",
            Password = "approver123",
            Role = Role.Approver
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        var result = await service.LoginAsync(new LoginRequest("approver", "wrong"));

        Assert.False(result.IsSuccess);
        Assert.Empty(db.UserSessions);
    }

    [Fact]
    public async Task LoginAsync_Uses_Long_Duration_When_Remember_Me_Is_Selected()
    {
        await using var db = TestDbFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin User",
            Password = "admin123",
            Role = Role.Admin
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        var result = await service.LoginAsync(new LoginRequest("admin", "admin123", true));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ExpiresAt > DateTime.UtcNow.AddDays(20));
    }

    private static IConfiguration CreateTestConfiguration() => new TestConfiguration();

    private sealed class TestConfiguration : IConfiguration
    {
        public string? this[string key]
        {
            get => key switch
            {
                "Auth:SessionDurationMinutes" => "1",
                "Auth:RememberMeDurationMinutes" => "43200",
                _ => null
            };
            set { }
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];

        public IChangeToken GetReloadToken() => new TestChangeToken();

        public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, this[key]);
    }

    private sealed class TestConfigurationSection(string key, string? value) : IConfigurationSection
    {
        public string? this[string childKey]
        {
            get => null;
            set { }
        }

        public string Key { get; } = key;
        public string Path { get; } = key;
        public string? Value { get; set; } = value;

        public IEnumerable<IConfigurationSection> GetChildren() => [];

        public IChangeToken GetReloadToken() => new TestChangeToken();

        public IConfigurationSection GetSection(string childKey) => new TestConfigurationSection(childKey, null);
    }

    private sealed class TestChangeToken : IChangeToken
    {
        public bool ActiveChangeCallbacks => false;
        public bool HasChanged => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new EmptyDisposable();
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
