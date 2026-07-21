using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class NotificationService(AppDbContext db) : INotificationService
{
    public async Task<NotificationPageDto> ListAsync(
        NotificationListRequest request,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var userNotifications = db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == currentUser.Id);

        var filtered = ApplyFilters(userNotifications, request);

        var globalCounts = await userNotifications
            .GroupBy(notification => 1)
            .Select(grouped => new
            {
                AllCount = grouped.Count(),
                UnreadCount = grouped.Count(notification => notification.ReadAt == null)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var notifications = await filtered
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new NotificationPageDto(
            notifications.Select(notification => notification.ToDto()).ToArray(),
            page,
            pageSize,
            totalCount,
            globalCounts?.AllCount ?? 0,
            globalCounts?.UnreadCount ?? 0);
    }

    public async Task<Result> MarkReadAsync(Guid notificationId, UserDto currentUser, CancellationToken cancellationToken = default)
        => await SetReadStateAsync(notificationId, true, currentUser, cancellationToken);

    public async Task<Result> SetReadStateAsync(
        Guid notificationId,
        bool isRead,
        UserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.UserId == currentUser.Id,
            cancellationToken);
        if (notification is null)
        {
            return Result.Failure("Notification was not found.");
        }

        notification.ReadAt = isRead ? notification.ReadAt ?? DateTime.UtcNow : null;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(UserDto currentUser, CancellationToken cancellationToken = default)
    {
        var notifications = await db.Notifications
            .Where(notification => notification.UserId == currentUser.Id && notification.ReadAt == null)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.ReadAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Type = request.Type.Trim(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Notification> ApplyFilters(
        IQueryable<Notification> query,
        NotificationListRequest request)
    {
        var search = request.Query?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(notification =>
                notification.Title.Contains(search)
                || notification.Message.Contains(search)
                || notification.Type.Contains(search));
        }

        query = request.ReadStatus?.Trim().ToLowerInvariant() switch
        {
            "unread" => query.Where(notification => notification.ReadAt == null),
            "read" => query.Where(notification => notification.ReadAt != null),
            _ => query
        };

        return request.Category?.Trim().ToLowerInvariant() switch
        {
            "task" => query.Where(notification => notification.Type.StartsWith("Task.")),
            "process" => query.Where(notification => notification.Type.StartsWith("Process.")),
            "access" => query.Where(notification =>
                notification.Type == "User.AccessUpdated"
                || notification.Type == "User.PendingApproval"
                || notification.Type.StartsWith("Community.")
                || notification.Type.StartsWith("Team.")),
            "account" => query.Where(notification =>
                notification.Type.StartsWith("User.")
                && notification.Type != "User.AccessUpdated"
                && notification.Type != "User.PendingApproval"),
            _ => query
        };
    }
}
