using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class NotificationService(AppDbContext db) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(UserDto currentUser, CancellationToken cancellationToken = default)
    {
        var notifications = await db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == currentUser.Id)
            .OrderBy(notification => notification.ReadAt != null)
            .ThenByDescending(notification => notification.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        return notifications.Select(notification => notification.ToDto()).ToArray();
    }

    public async Task<Result> MarkReadAsync(Guid notificationId, UserDto currentUser, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.UserId == currentUser.Id,
            cancellationToken);
        if (notification is null)
        {
            return Result.Failure("Notification was not found.");
        }

        notification.ReadAt ??= DateTime.UtcNow;
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
}
