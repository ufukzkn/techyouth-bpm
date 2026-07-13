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

public sealed class NotificationListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Query { get; init; }
    public string? ReadStatus { get; init; }
    public string? Category { get; init; }
}

public record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int AllCount,
    int UnreadCount);

public record MarkNotificationReadStateRequest(bool IsRead);

public record CreateNotificationRequest(
    Guid UserId,
    string Type,
    string Title,
    string Message,
    string? EntityType = null,
    string? EntityId = null);
