using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;

namespace TechYouthBpm.Application.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result> MarkReadAsync(Guid notificationId, UserDto currentUser, CancellationToken cancellationToken = default);
    Task<Result> MarkAllReadAsync(UserDto currentUser, CancellationToken cancellationToken = default);
    Task CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default);
}
