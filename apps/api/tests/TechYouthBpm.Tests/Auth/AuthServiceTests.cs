using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Text.RegularExpressions;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Services;
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
    public async Task LoginAsync_Keeps_Access_Session_Short_When_Remember_Me_Is_Selected()
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
        Assert.True(result.Value!.ExpiresAt < DateTime.UtcNow.AddMinutes(2));
        Assert.True(db.RefreshTokens.Single().ExpiresAt > DateTime.UtcNow.AddDays(20));
    }

    [Fact]
    public async Task LoginAsync_RememberMe_Creates_Hashed_Refresh_Token_And_Remembered_Session()
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

        var result = await service.LoginAsync(new LoginRequest("admin", "admin123", true));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!.RefreshToken);
        Assert.NotEmpty(result.Value.CsrfToken);
        var storedRefreshToken = Assert.Single(db.RefreshTokens);
        var storedSession = Assert.Single(db.UserSessions);
        Assert.True(storedSession.RememberedDevice);
        Assert.NotEqual(result.Value.RefreshToken, storedRefreshToken.Token);
        Assert.Equal(64, storedRefreshToken.Token.Length);
    }

    [Fact]
    public async Task RefreshSessionAsync_Rotates_Refresh_Token_And_Replaces_Access_Session()
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
        var login = await service.LoginAsync(new LoginRequest("admin", "admin123", true));

        var refresh = await service.RefreshSessionAsync(login.Value!.RefreshToken);

        Assert.True(refresh.IsSuccess);
        Assert.NotEqual(login.Value.Token, refresh.Value!.Token);
        Assert.NotEqual(login.Value.RefreshToken, refresh.Value.RefreshToken);
        Assert.Equal(2, db.UserSessions.Count());
        Assert.Equal(2, db.RefreshTokens.Count());
        Assert.NotNull(db.UserSessions.OrderBy(session => session.CreatedAt).First().RevokedAt);
        Assert.NotNull(db.RefreshTokens.OrderBy(token => token.CreatedAt).First().RevokedAt);
    }

    [Fact]
    public async Task RefreshSessionAsync_Reused_Refresh_Token_Revokes_Active_Sessions()
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
        var login = await service.LoginAsync(new LoginRequest("admin", "admin123", true));
        var firstRefresh = await service.RefreshSessionAsync(login.Value!.RefreshToken);

        var reuse = await service.RefreshSessionAsync(login.Value.RefreshToken);

        Assert.True(firstRefresh.IsSuccess);
        Assert.False(reuse.IsSuccess);
        Assert.All(db.UserSessions, session => Assert.NotNull(session.RevokedAt));
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
    public async Task CreateUserAsync_Sends_Temporary_Password_Email()
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
        var emailSender = new CapturingEmailSender();
        var service = new AuthService(db, CreateTestConfiguration(), new SystemAuditService(db), new OtpService(), emailSender);
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, admin.Email, admin.Role, admin.Status, true);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("newuser", "New User", "new@test.local", Role.User, UserStatus.Active, "TempPass123!"),
            adminDto);

        Assert.True(result.IsSuccess);
        var message = Assert.Single(emailSender.Messages);
        Assert.Equal("new@test.local", message.To);
        Assert.Contains("TempPass123!", message.Body, StringComparison.Ordinal);
        Assert.Contains("newuser", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserAsync_Does_Not_Save_User_When_Temporary_Password_Email_Fails()
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
        var service = new AuthService(db, CreateTestConfiguration(), new SystemAuditService(db), new OtpService(), new FailingEmailSender());
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, admin.Email, admin.Role, admin.Status, true);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("newuser", "New User", "new@test.local", Role.User, UserStatus.Active, "TempPass123!"),
            adminDto);

        Assert.False(result.IsSuccess);
        Assert.Contains("Temporary password email could not be sent.", result.Errors);
        Assert.DoesNotContain(db.Users, user => user.Username == "newuser");
    }

    [Fact]
    public async Task CreateUserAsync_Generates_Temporary_Password_When_Request_Is_Blank()
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
        var emailSender = new CapturingEmailSender();
        var service = new AuthService(db, CreateTestConfiguration(), new SystemAuditService(db), new OtpService(), emailSender);
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, admin.Email, admin.Role, admin.Status, true);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("autouser", "Auto User", "auto@test.local", Role.User, UserStatus.Active, ""),
            adminDto);

        Assert.True(result.IsSuccess);
        Assert.True(db.Users.Single(user => user.Username == "autouser").MustChangePassword);
        var message = Assert.Single(emailSender.Messages);
        Assert.Contains("Gecici sifre", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Gecici sifre</div>\r\n                            <div style=\"margin-top:6px;font-size:24px;font-weight:800;color:#d95f05;letter-spacing:.04em;\"></div>", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteUserAsync_Allows_Admin_To_Delete_User_Without_Workflow_History()
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
            Username = "delete-me",
            DisplayName = "Delete Me",
            Email = "delete@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.Active
        };
        db.Users.AddRange(admin, user);
        db.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "token-hash",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration(), new SystemAuditService(db), new OtpService(), new DemoEmailSender());
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, admin.Email, admin.Role, admin.Status, true);

        var result = await service.DeleteUserAsync(user.Id, adminDto);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(db.Users, item => item.Id == user.Id);
        Assert.DoesNotContain(db.UserSessions, session => session.UserId == user.Id);
        Assert.Contains(db.SystemAuditLogs, log => log.Action == "User.DeletedByAdmin");
    }

    [Fact]
    public async Task DeleteUserAsync_Rejects_User_With_Workflow_History()
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
            Username = "starter",
            DisplayName = "Starter",
            Email = "starter@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.Active
        };
        db.Users.AddRange(admin, user);
        await db.SaveChangesAsync();
        TestDbFactory.SeedOpenApproverTask(db, user);
        var service = new AuthService(db, CreateTestConfiguration(), new SystemAuditService(db), new OtpService(), new DemoEmailSender());
        var adminDto = new UserDto(admin.Id, admin.Username, admin.DisplayName, admin.Email, admin.Role, admin.Status, true);

        var result = await service.DeleteUserAsync(user.Id, adminDto);

        Assert.False(result.IsSuccess);
        Assert.Contains("User has workflow history and cannot be deleted.", result.Errors);
        Assert.Contains(db.Users, item => item.Id == user.Id);
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

    [Fact]
    public async Task StartEmailVerificationAsync_Rejects_Immediate_Resend()
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
            IsEmailVerified = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var emailSender = new CapturingEmailSender();
        var service = new AuthService(db, CreateTestConfiguration(), new SystemAuditService(db), new OtpService(), emailSender);
        var userDto = new UserDto(user.Id, user.Username, user.DisplayName, user.Email, user.Role, user.Status, false);

        var first = await service.StartEmailVerificationAsync(userDto);
        var second = await service.StartEmailVerificationAsync(userDto);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Contains("Verification code was sent recently. Please wait before requesting another code.", second.Errors);
        Assert.Single(emailSender.Messages);
    }

    [Fact]
    public async Task PublicEmailVerification_Allows_Pending_User_To_Verify_Email()
    {
        await using var db = TestDbFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "pending",
            DisplayName = "Pending User",
            Email = "pending@test.local",
            Password = "password123",
            Role = Role.User,
            Status = UserStatus.PendingApproval,
            IsEmailVerified = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AuthService(db, CreateTestConfiguration());

        var start = await service.StartPublicEmailVerificationAsync(new PublicEmailVerificationStartRequest("pending"));
        var confirm = await service.ConfirmPublicEmailVerificationAsync(
            new PublicEmailVerificationConfirmRequest("pending", start.Value!.DemoCode));

        Assert.True(start.IsSuccess);
        Assert.True(confirm.IsSuccess);
        Assert.True(db.Users.Single().IsEmailVerified);
        Assert.Equal(UserStatus.PendingApproval, confirm.Value!.Status);
    }

    [Fact]
    public async Task ResetPasswordAsync_Changes_Password_And_Revokes_Active_Sessions()
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
        var emailSender = new CapturingEmailSender();
        var service = new AuthService(db, CreateTestConfiguration(), new SystemAuditService(db), new OtpService(), emailSender);
        var login = await service.LoginAsync(new LoginRequest("user", "password123", true));

        var forgot = await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@test.local"));
        var token = ExtractSecurityToken(emailSender.Messages.Single().Body);
        var reset = await service.ResetPasswordAsync(new ResetPasswordRequest("user", token, "new-password-123"));
        var oldSessionUser = await service.GetUserByTokenAsync(login.Value!.Token);
        var newLogin = await service.LoginAsync(new LoginRequest("user", "new-password-123"));

        Assert.True(forgot.IsSuccess);
        Assert.True(reset.IsSuccess);
        Assert.Null(oldSessionUser);
        Assert.True(newLogin.IsSuccess);
    }

    [Fact]
    public async Task ForgotPasswordAsync_Does_Not_Reveal_Unknown_User()
    {
        await using var db = TestDbFactory.Create();
        var emailSender = new CapturingEmailSender();
        var service = new AuthService(db, CreateTestConfiguration(), new SystemAuditService(db), new OtpService(), emailSender);

        var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest("missing@test.local"));

        Assert.True(result.IsSuccess);
        Assert.Empty(emailSender.Messages);
    }

    private static IConfiguration CreateTestConfiguration() => new TestConfiguration();

    private static string ExtractSecurityToken(string htmlBody)
    {
        var match = Regex.Matches(htmlBody, "[A-Za-z0-9_-]{40,}")
            .Select(item => item.Value)
            .FirstOrDefault();

        Assert.False(string.IsNullOrWhiteSpace(match));
        return match!;
    }

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
                "Auth:EmailVerificationResendCooldownMinutes" => "5",
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

    private sealed class CapturingEmailSender : IEmailSender
    {
        public bool ExposesVerificationCode => false;

        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public bool ExposesVerificationCode => false;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("SMTP is not configured.");
    }
}
