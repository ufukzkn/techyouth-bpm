using TechYouthBpm.Application.Auth;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Auth;

public class NotificationServiceTests
{
    [Fact]
    public async Task ListAsync_Returns_Only_Current_User_Notifications_In_Newest_First_Order()
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

        var result = await service.ListAsync(new NotificationListRequest(), ToDto(currentUser));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.AllCount);
        Assert.Equal(1, result.UnreadCount);
        Assert.Equal(newerRead.Id, result.Items[0].Id);
        Assert.Equal(olderUnread.Id, result.Items[1].Id);
        Assert.DoesNotContain(result.Items, notification => notification.Id == otherNotification.Id);
    }

    [Fact]
    public async Task ListAsync_Applies_Search_Category_Read_State_And_Pagination()
    {
        await using var db = TestDbFactory.Create();
        var currentUser = CreateUser("current-user");
        db.Users.Add(currentUser);
        db.Notifications.AddRange(
            CreateNotification(currentUser.Id, "Transfer approval", DateTime.UtcNow.AddMinutes(-1), type: "Task.Assigned"),
            CreateNotification(currentUser.Id, "Transfer completed", DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow, "Process.Completed"),
            CreateNotification(currentUser.Id, "Another task", DateTime.UtcNow.AddMinutes(-3), type: "Task.Assigned"));
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        var result = await service.ListAsync(new NotificationListRequest
        {
            Page = 1,
            PageSize = 1,
            Query = "Transfer",
            ReadStatus = "unread",
            Category = "task"
        }, ToDto(currentUser));

        Assert.Single(result.Items);
        Assert.Equal("Transfer approval", result.Items[0].Title);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(3, result.AllCount);
        Assert.Equal(2, result.UnreadCount);
        Assert.Equal(1, result.PageSize);
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

    [Fact]
    public async Task SetReadStateAsync_Can_Mark_Own_Notification_Unread()
    {
        await using var db = TestDbFactory.Create();
        var currentUser = CreateUser("current-user");
        var notification = CreateNotification(currentUser.Id, "Read item", DateTime.UtcNow, DateTime.UtcNow);
        db.Users.Add(currentUser);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        var result = await service.SetReadStateAsync(notification.Id, false, ToDto(currentUser));

        Assert.True(result.IsSuccess);
        Assert.Null(notification.ReadAt);
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

    private static Notification CreateNotification(
        Guid userId,
        string title,
        DateTime createdAt,
        DateTime? readAt = null,
        string type = "Process.Updated") => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Type = type,
        Title = title,
        Message = title,
        CreatedAt = createdAt,
        ReadAt = readAt
    };
}
