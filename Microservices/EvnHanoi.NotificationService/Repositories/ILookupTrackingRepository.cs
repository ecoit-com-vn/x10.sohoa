namespace EvnHanoi.NotificationService.Repositories;

public interface ILookupTrackingRepository
{
    /// <summary>Cộng dồn 1 lượt xem cho (DossierId, EntityType, hôm nay) — MERGE upsert, không insert dòng mới mỗi lượt.</summary>
    Task RecordViewAsync(string entityType, string dossierId);
}
