using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Auth;

public class NotificationServiceTests
{
    [Fact]
    public async Task ListAsync_Returns_Only_Current_User_Notifications_And_Prioritizes_Unread()
    {
        await using var db = TestDbFactory.Create();
        var currentUser = CreateUser("current-user");
        var otherUser = CreateUser("other-user");
        var olderUnread = CreateNotification(currentUser.Id, "Older unread", DateTime.UtcNow.AddMinutes(-10));
        var newerRead = CreateNotification(currentUser.Id, "Newer read", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
        var otherNotification = CreateNotification(otherUser.Id, "Other user", DateTime.UtcNow);
        db.Users.AddRange(currentUser, otherUser);
        db.Notifications.AddRange(olderUnread, newerRead, otherNotification);
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        var result = await service.ListAsync(ToDto(currentUser));

        Assert.Equal(2, result.Count);
        Assert.Equal(olderUnread.Id, result[0].Id);
        Assert.Equal(newerRead.Id, result[1].Id);
        Assert.DoesNotContain(result, notification => notification.Id == otherNotification.Id);
    }

    [Fact]
    public async Task MarkReadAsync_Rejects_Another_Users_Notification()
    {
        await using var db = TestDbFactory.Create();
        var currentUser = CreateUser("current-user");
        var otherUser = CreateUser("other-user");
        var otherNotification = CreateNotification(otherUser.Id, "Other user", DateTime.UtcNow);
        db.Users.AddRange(currentUser, otherUser);
        db.Notifications.Add(otherNotification);
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        var result = await service.MarkReadAsync(otherNotification.Id, ToDto(currentUser));

        Assert.False(result.IsSuccess);
        Assert.Null(db.Notifications.Single().ReadAt);
    }

    [Fact]
    public async Task MarkAllReadAsync_Only_Marks_Current_Users_Unread_Notifications()
    {
        await using var db = TestDbFactory.Create();
        var currentUser = CreateUser("current-user");
        var otherUser = CreateUser("other-user");
        var currentUnread = CreateNotification(currentUser.Id, "Current unread", DateTime.UtcNow);
        var otherUnread = CreateNotification(otherUser.Id, "Other unread", DateTime.UtcNow);
        db.Users.AddRange(currentUser, otherUser);
        db.Notifications.AddRange(currentUnread, otherUnread);
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        var result = await service.MarkAllReadAsync(ToDto(currentUser));

        Assert.True(result.IsSuccess);
        Assert.NotNull(currentUnread.ReadAt);
        Assert.Null(otherUnread.ReadAt);
    }

    private static User CreateUser(string username) => new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        DisplayName = username,
        Email = $"{username}@test.local",
        Password = "password123",
        Role = Role.User,
        Status = UserStatus.Active,
        IsEmailVerified = true
    };

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.Username,
        user.DisplayName,
        user.Email,
        user.Role,
        user.Status,
        user.IsEmailVerified);

    private static Notification CreateNotification(Guid userId, string title, DateTime createdAt, DateTime? readAt = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Type = "Process.Updated",
        Title = title,
        Message = title,
        CreatedAt = createdAt,
        ReadAt = readAt
    };
}
