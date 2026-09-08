using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

using Infrastructure = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;

public interface IInfrastructureRepository
{
    Task<Infrastructure?> GetByIdAsync(Guid id);
    Task<Infrastructure?> GetByCodeAsync(string code);
    Task<(IEnumerable<Infrastructure> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        int infraTypeId,
        string? keyword,
        int? status,
        IEnumerable<long>? unitIds = null,
        long? unitId = null,
        int? gridTypeId = null,
        DateTime? fromOperationDate = null,
        DateTime? toOperationDate = null);
    Task<Guid> CreateAsync(Infrastructure infrastructure);
    Task<bool> UpdateAsync(Infrastructure infrastructure);
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Đồng bộ PMIS: tìm theo PmisCode, có thì cập nhật, chưa có thì tạo mới. Trả về Id + đã tạo mới hay
    /// chưa (dùng ghi ACTION_TYPE CREATE/UPDATE vào SYNC_HISTORY_DETAIL) + HasChanged — nếu bản ghi đã
    /// tồn tại và dữ liệu PMIS gửi về giống hệt dữ liệu đang lưu thì KHÔNG issue câu UPDATE (trả
    /// HasChanged=false, caller ghi ACTION_TYPE=SKIP) — tránh ghi đè/tăng ModifiedDate vô ích mỗi lần
    /// resync khi PMIS không có gì mới.
    /// </summary>
    Task<(Guid Id, bool WasCreated, bool HasChanged)> UpsertFromPmisAsync(
        int infraTypeId, string pmisCode, string code, string name, string? address, string? unitCode, DateTime? operationDate, int? gridTypeId = null);

    /// <summary>Danh sách PmisCode đã đồng bộ (dùng cho auto-sync Thiết bị — lặp qua từng Trạm/Đường dây đã có để lấy thiết bị con).</summary>
    Task<IEnumerable<(string PmisCode, int InfraTypeId)>> GetSyncedPmisCodesAsync();
}
