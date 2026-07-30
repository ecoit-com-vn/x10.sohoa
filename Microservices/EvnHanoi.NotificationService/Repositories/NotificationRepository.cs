using System.Data;
using Dapper;
using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IDbConnection _connection;

    public NotificationRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<string?> CreateWithRecipientsAsync(
        string notificationType,
        string title,
        string? body,
        string? relatedEntityType,
        string? relatedEntityId,
        string? createdByUserId,
        IReadOnlyCollection<string> recipientUserIds)
    {
        if (recipientUserIds.Count == 0) return null;

        if (_connection.State != ConnectionState.Open) _connection.Open();

        using var transaction = _connection.BeginTransaction();
        try
        {
            var notificationId = Guid.NewGuid().ToString();

            const string insertNotificationSql = """
                INSERT INTO NOTIFICATIONS
                    (ID, NOTIFICATION_TYPE, TITLE, BODY, RELATED_ENTITY_TYPE, RELATED_ENTITY_ID, CREATED_BY_USER_ID)
                VALUES
                    (:Id, :NotificationType, :Title, :Body, :RelatedEntityType, :RelatedEntityId, :CreatedByUserId)
                """;

            await _connection.ExecuteAsync(insertNotificationSql, new
            {
                Id = notificationId,
                NotificationType = notificationType,
                Title = title,
                Body = body,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                CreatedByUserId = createdByUserId
            }, transaction);

            const string insertRecipientSql = """
                INSERT INTO NOTIFICATION_RECIPIENTS (ID, NOTIFICATION_ID, USER_ID)
                VALUES (:Id, :NotificationId, :UserId)
                """;

            var recipientRows = recipientUserIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(userId => new
                {
                    Id = Guid.NewGuid().ToString(),
                    NotificationId = notificationId,
                    UserId = userId
                });

            await _connection.ExecuteAsync(insertRecipientSql, recipientRows, transaction);

            transaction.Commit();
            return notificationId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<(IReadOnlyList<NotificationListItemDto> Items, int TotalCount, int UnreadCount)> GetForUserAsync(
        string userId, int page, int pageSize, bool onlyUnread)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        var readFilter = onlyUnread ? "AND r.IS_READ = 0" : "";

        var countSql = $"""
            SELECT COUNT(*) FROM NOTIFICATION_RECIPIENTS r
            WHERE r.USER_ID = :UserId AND r.IS_DELETED = 0 {readFilter}
            """;

        var unreadCountSql = """
            SELECT COUNT(*) FROM NOTIFICATION_RECIPIENTS r
            WHERE r.USER_ID = :UserId AND r.IS_DELETED = 0 AND r.IS_READ = 0
            """;

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("OffsetPlusSize", offset + pageSize);

        var sql = $"""
            SELECT * FROM (
                SELECT
                    n.ID AS {nameof(NotificationListItemDto.Id)},
                    n.NOTIFICATION_TYPE AS {nameof(NotificationListItemDto.NotificationType)},
                    n.TITLE AS {nameof(NotificationListItemDto.Title)},
                    n.BODY AS {nameof(NotificationListItemDto.Body)},
                    n.RELATED_ENTITY_TYPE AS {nameof(NotificationListItemDto.RelatedEntityType)},
                    n.RELATED_ENTITY_ID AS {nameof(NotificationListItemDto.RelatedEntityId)},
                    n.CREATED_AT AS {nameof(NotificationListItemDto.CreatedAt)},
                    r.IS_READ AS {nameof(NotificationListItemDto.IsRead)},
                    r.READ_AT AS {nameof(NotificationListItemDto.ReadAt)},
                    ROW_NUMBER() OVER (ORDER BY n.CREATED_AT DESC) AS RN
                FROM NOTIFICATION_RECIPIENTS r
                JOIN NOTIFICATIONS n ON n.ID = r.NOTIFICATION_ID
                WHERE r.USER_ID = :UserId AND r.IS_DELETED = 0 {readFilter}
            ) WHERE RN > :Offset AND RN <= :OffsetPlusSize
            """;

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
        var unreadCount = await _connection.ExecuteScalarAsync<int>(unreadCountSql, parameters);
        var items = await _connection.QueryAsync<NotificationListItemDto>(sql, parameters);

        return (items.ToList(), totalCount, unreadCount);
    }

    public async Task<bool> MarkAsReadAsync(string userId, string notificationId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = """
            UPDATE NOTIFICATION_RECIPIENTS
            SET IS_READ = 1, READ_AT = SYSTIMESTAMP
            WHERE NOTIFICATION_ID = :NotificationId AND USER_ID = :UserId AND IS_DELETED = 0
            """;

        var affected = await _connection.ExecuteAsync(sql, new { NotificationId = notificationId, UserId = userId });
        return affected > 0;
    }

    public async Task<int> MarkAllAsReadAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = """
            UPDATE NOTIFICATION_RECIPIENTS
            SET IS_READ = 1, READ_AT = SYSTIMESTAMP
            WHERE USER_ID = :UserId AND IS_DELETED = 0 AND IS_READ = 0
            """;

        return await _connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task<bool> DeleteAsync(string userId, string notificationId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = """
            UPDATE NOTIFICATION_RECIPIENTS
            SET IS_DELETED = 1, DELETED_AT = SYSTIMESTAMP
            WHERE NOTIFICATION_ID = :NotificationId AND USER_ID = :UserId AND IS_DELETED = 0
            """;

        var affected = await _connection.ExecuteAsync(sql, new { NotificationId = notificationId, UserId = userId });
        return affected > 0;
    }

    public async Task<int> DeleteAllAsync(string userId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = """
            UPDATE NOTIFICATION_RECIPIENTS
            SET IS_DELETED = 1, DELETED_AT = SYSTIMESTAMP
            WHERE USER_ID = :UserId AND IS_DELETED = 0
            """;

        return await _connection.ExecuteAsync(sql, new { UserId = userId });
    }
}
