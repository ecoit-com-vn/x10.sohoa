using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Repositories;

public interface INotificationRepository
{
    Task<string?> CreateWithRecipientsAsync(
        string notificationType,
        string title,
        string? body,
        string? relatedEntityType,
        string? relatedEntityId,
        string? createdByUserId,
        IReadOnlyCollection<string> recipientUserIds);

    Task<(IReadOnlyList<NotificationListItemDto> Items, int TotalCount, int UnreadCount)> GetForUserAsync(
        string userId, int page, int pageSize, bool onlyUnread);

    Task<bool> MarkAsReadAsync(string userId, string notificationId);

    Task<int> MarkAllAsReadAsync(string userId);

    Task<bool> DeleteAsync(string userId, string notificationId);

    Task<int> DeleteAllAsync(string userId);
}
