namespace TechYouthBpm.Application.Auth;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? EntityType,
    string? EntityId,
    DateTime CreatedAt,
    DateTime? ReadAt);

public record CreateNotificationRequest(
    Guid UserId,
    string Type,
    string Title,
    string Message,
    string? EntityType = null,
    string? EntityId = null);
