namespace EvnHanoi.NotificationService.Services;

public interface IDossierIndexer
{
    Task<bool> IndexByIdAsync(string dossierId, CancellationToken cancellationToken = default);

    /// <summary>Xóa document khỏi dossier_index (idempotent — không lỗi nếu doc không tồn tại).</summary>
    Task<bool> DeleteByIdAsync(string dossierId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa một lần các document legacy có _id không chuẩn (hoa/thường, N-format) — không search ES.
    /// </summary>
    Task<int> PurgeLegacyDocumentIdsAsync(CancellationToken cancellationToken = default);
}
