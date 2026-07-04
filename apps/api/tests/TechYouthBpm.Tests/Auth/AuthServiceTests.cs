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
            Email = "admin@test.local",
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
            Email = "user@test.local",
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
            Email = "approver@test.local",
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
            Email = "admin@test.local",
            Password = "admin123",
            Role = Role.Admin
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        var result = await service.LoginAsync(new LoginRequest("admin", "admin123", true));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ExpiresAt > DateTime.UtcNow.AddDays(20));
    }

    [Fact]
    public async Task RegisterAsync_Creates_Pending_Unverified_User()
    {
        await using var db = TestDbFactory.Create();
        var service = new AuthService(db, CreateTestConfiguration());

        var result = await service.RegisterAsync(new RegisterRequest(
            "newuser",
            "New User",
            "newuser@test.local",
            "password123"));

        Assert.True(result.IsSuccess);
        var user = Assert.Single(db.Users);
        Assert.Equal(UserStatus.PendingApproval, user.Status);
        Assert.False(user.IsEmailVerified);
        Assert.StartsWith("pbkdf2:v1:", user.Password, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginAsync_Rejects_Pending_Approval_User()
    {
        await using var db = TestDbFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "pending",
            DisplayName = "Pending User",
            Email = "pending@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.PendingApproval
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        var result = await service.LoginAsync(new LoginRequest("pending", "password123"));

        Assert.False(result.IsSuccess);
        Assert.Empty(db.UserSessions);
    }

    [Fact]
    public async Task LoginAsync_Locks_Account_After_Configured_Failed_Attempts()
    {
        await using var db = TestDbFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "locked",
            DisplayName = "Locked User",
            Email = "locked@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        await service.LoginAsync(new LoginRequest("locked", "wrong"));
        await service.LoginAsync(new LoginRequest("locked", "wrong-again"));

        var user = db.Users.Single();
        Assert.Equal(2, user.FailedLoginCount);
        Assert.NotNull(user.LockedUntil);
    }

    [Fact]
    public async Task LogoutAsync_Revokes_Current_Session()
    {
        await using var db = TestDbFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin User",
            Email = "admin@test.local",
            Password = "admin123",
            Role = Role.Admin,
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());
        var login = await service.LoginAsync(new LoginRequest("admin", "admin123"));

        var logout = await service.LogoutAsync(login.Value!.Token);
        var user = await service.GetUserByTokenAsync(login.Value.Token);

        Assert.True(logout.IsSuccess);
        Assert.Null(user);
        Assert.NotNull(db.UserSessions.Single().RevokedAt);
    }

    [Fact]
    public async Task ListUserSessionsAsync_Allows_Admin_To_View_Target_User_Sessions()
    {
        await using var db = TestDbFactory.Create();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin User",
            Email = "admin@test.local",
            Password = "admin123",
            Role = Role.Admin,
            Status = UserStatus.Active
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "user",
            DisplayName = "Regular User",
            Email = "user@test.local",
            Password = "user123",
            Role = Role.User,
            Status = UserStatus.Active
        };
        db.Users.AddRange(admin, user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());
        var adminLogin = await service.LoginAsync(new LoginRequest("admin", "admin123"));
        await service.LoginAsync(new LoginRequest("user", "user123"));
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, admin.Email, admin.Role, admin.Status, admin.IsEmailVerified);

        var sessions = await service.ListUserSessionsAsync(user.Id, adminDto, adminLogin.Value!.Token);

        Assert.True(sessions.IsSuccess);
        var session = Assert.Single(sessions.Value!);
        Assert.False(session.IsCurrent);
    }

    [Fact]
    public async Task RevokeUserSessionAsync_Allows_Admin_To_Revoke_Target_User_Session()
    {
        await using var db = TestDbFactory.Create();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin User",
            Email = "admin@test.local",
            Password = "admin123",
            Role = Role.Admin,
            Status = UserStatus.Active
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "user",
            DisplayName = "Regular User",
            Email = "user@test.local",
            Password = "user123",
            Role = Role.User,
            Status = UserStatus.Active
        };
        db.Users.AddRange(admin, user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());
        await service.LoginAsync(new LoginRequest("user", "user123"));
        var session = db.UserSessions.Single(item => item.UserId == user.Id);

        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, admin.Email, admin.Role, admin.Status, admin.IsEmailVerified);

        var revoke = await service.RevokeUserSessionAsync(user.Id, session.Id, adminDto);

        Assert.True(revoke.IsSuccess);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task ListUserSessionsAsync_Rejects_Non_Admin()
    {
        await using var db = TestDbFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "user",
            DisplayName = "Regular User",
            Email = "user@test.local",
            Password = "user123",
            Role = Role.User,
            Status = UserStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        var userDto = new UserDto(user.Id, user.Username, user.DisplayName, user.Email, user.Role, user.Status, user.IsEmailVerified);

        var sessions = await service.ListUserSessionsAsync(user.Id, userDto, "token");

        Assert.False(sessions.IsSuccess);
        Assert.Contains("Only Admin users", sessions.Errors[0]);
    }

    [Fact]
    public async Task LoginAsync_Stores_Session_Metadata()
    {
        await using var db = TestDbFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin User",
            Email = "admin@test.local",
            Password = "admin123",
            Role = Role.Admin,
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        var result = await service.LoginAsync(new LoginRequest("admin", "admin123"), "127.0.0.1", "Chrome on Windows");

        Assert.True(result.IsSuccess);
        var session = Assert.Single(db.UserSessions);
        Assert.Equal("127.0.0.1", session.IpAddress);
        Assert.Equal("Chrome on Windows", session.UserAgent);
    }

    [Fact]
    public async Task CreateUserAsync_Allows_Admin_To_Create_Temporary_Password_User()
    {
        await using var db = TestDbFactory.Create();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin User",
            Email = "admin@test.local",
            Password = "admin123",
            Role = Role.Admin,
            Status = UserStatus.Active
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, admin.Email, admin.Role, admin.Status, true);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("newuser", "New User", "new@test.local", Role.Approver, UserStatus.Active, "password123"),
            adminDto);

        Assert.True(result.IsSuccess);
        var created = db.Users.Single(user => user.Username == "newuser");
        Assert.True(created.MustChangePassword);
        Assert.False(created.IsEmailVerified);
        Assert.StartsWith("pbkdf2:v1:", created.Password, StringComparison.Ordinal);
        Assert.Contains(db.SystemAuditLogs, log => log.Action == "User.CreatedByAdmin");
    }

    [Fact]
    public async Task CreateUserAsync_Rejects_Non_Admin()
    {
        await using var db = TestDbFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "user",
            DisplayName = "Regular User",
            Email = "user@test.local",
            Password = "user123",
            Role = Role.User,
            Status = UserStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());
        var userDto = new UserDto(user.Id, user.Username, user.DisplayName, user.Email, user.Role, user.Status, true);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("newuser", "New User", "new@test.local", Role.User, UserStatus.Active, "password123"),
            userDto);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(db.Users, item => item.Username == "newuser");
    }

    [Fact]
    public async Task ChangePasswordAsync_Clears_Temporary_Password_Requirement()
    {
        await using var db = TestDbFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "temp",
            DisplayName = "Temp User",
            Email = "temp@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.Active,
            MustChangePassword = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());
        var userDto = new UserDto(user.Id, user.Username, user.DisplayName, user.Email, user.Role, user.Status, true, true);

        var result = await service.ChangePasswordAsync(new ChangePasswordRequest("password123", "newpass123"), userDto);

        Assert.True(result.IsSuccess);
        Assert.False(user.MustChangePassword);
        Assert.StartsWith("pbkdf2:v1:", user.Password, StringComparison.Ordinal);
        Assert.Contains(db.SystemAuditLogs, log => log.Action == "Auth.TemporaryPasswordChanged");
    }

    [Fact]
    public async Task ChangePasswordAsync_Rejects_Wrong_Current_Password()
    {
        await using var db = TestDbFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "user",
            DisplayName = "Regular User",
            Email = "user@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());
        var userDto = new UserDto(user.Id, user.Username, user.DisplayName, user.Email, user.Role, user.Status, true);

        var result = await service.ChangePasswordAsync(new ChangePasswordRequest("wrong", "newpass123"), userDto);

        Assert.False(result.IsSuccess);
        Assert.Equal("password123", user.Password);
    }

    [Fact]
    public async Task UpdateProfileAsync_Resets_Email_Verification_When_Email_Changes()
    {
        await using var db = TestDbFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "user",
            DisplayName = "Regular User",
            Email = "user@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.Active,
            IsEmailVerified = true,
            EmailVerificationCode = "old-code",
            EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());
        var userDto = new UserDto(user.Id, user.Username, user.DisplayName, user.Email, user.Role, user.Status, true);

        var result = await service.UpdateProfileAsync(new UpdateProfileRequest("Updated User", "updated@test.local"), userDto);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated User", user.DisplayName);
        Assert.Equal("updated@test.local", user.Email);
        Assert.False(user.IsEmailVerified);
        Assert.Null(user.EmailVerificationCode);
        Assert.Null(user.EmailVerificationCodeExpiresAt);
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
                "Auth:MaxFailedLoginAttempts" => "2",
                "Auth:LockoutMinutes" => "10",
                "Auth:EmailVerificationMinutes" => "10",
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
