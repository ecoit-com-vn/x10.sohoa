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
        DateTime? toOperationDate = null,
        bool rootOnly = false);
    Task<Guid> CreateAsync(Infrastructure infrastructure);
    Task<bool> UpdateAsync(Infrastructure infrastructure);
    Task<bool> DeleteAsync(Guid id);

    /// <summary>Chỉ Đường dây (InfraTypeId=2): các đường dây NHÁNH CON trực tiếp (PARENT_ID = parentId) —
    /// tải "lười" khi người dùng bấm mở rộng 1 dòng trên màn hình Danh mục đường dây, vì trang dữ liệu
    /// phân trang (GetPagedAsync) không đảm bảo cha/con luôn rơi vào cùng 1 trang (14000+ đường dây,
    /// PARENT_ID rải rác khắp các trang) — xem TransmissionLineController.GetChildren.</summary>
    Task<IEnumerable<Infrastructure>> GetChildLinesAsync(Guid parentId);

    /// <summary>
    /// Đồng bộ PMIS: tìm theo PmisCode, có thì cập nhật, chưa có thì tạo mới. Trả về Id + đã tạo mới hay
    /// chưa (dùng ghi ACTION_TYPE CREATE/UPDATE vào SYNC_HISTORY_DETAIL) + HasChanged — nếu bản ghi đã
    /// tồn tại và dữ liệu PMIS gửi về giống hệt dữ liệu đang lưu thì KHÔNG issue câu UPDATE (trả
    /// HasChanged=false, caller ghi ACTION_TYPE=SKIP) — tránh ghi đè/tăng ModifiedDate vô ích mỗi lần
    /// resync khi PMIS không có gì mới.
    /// </summary>
    Task<(Guid Id, bool WasCreated, bool HasChanged)> UpsertFromPmisAsync(
        int infraTypeId, string pmisCode, string code, string name, string? address, string? unitCode, DateTime? operationDate,
        int? gridTypeId = null, bool isRootLine = false, Guid? parentInfrastructureId = null);

    /// <summary>Danh sách PmisCode đã đồng bộ (dùng cho auto-sync Thiết bị — lặp qua từng Trạm/Đường dây đã có để lấy thiết bị con).</summary>
    Task<IEnumerable<(string PmisCode, int InfraTypeId)>> GetSyncedPmisCodesAsync();

    /// <summary>Toàn bộ Đường dây hiện có (Id + Name + mã đơn vị PMIS nếu tra được) — SyncService tải 1
    /// lần/lượt đồng bộ Đường dây để tự tìm cha theo tên trong bộ nhớ, thay vì mỗi dòng tự query riêng.</summary>
    Task<IEnumerable<EvnHanoi.EquipmentService.Core.DTOs.LineNameIndexEntry>> GetLineNameIndexAsync();

    /// <summary>Các Đường dây ĐÃ tồn tại cần "khớp lại" bởi job Quartz riêng chạy nền định kỳ của
    /// SyncService (LineParentBackfillJob, KHÔNG chèn vào lượt đồng bộ Đường dây nào) — gồm 2 trường hợp:
    /// (1) tên có "/" (chắc chắn là nhánh) nhưng PARENT_ID còn NULL; (2) đã có PARENT_ID nhưng GRIDTYPEID
    /// vẫn NULL — trả kèm ParentId/ParentGridTypeId (JOIN sẵn) để trường hợp (2) không cần resolve lại
    /// theo tên.</summary>
    Task<IEnumerable<EvnHanoi.EquipmentService.Core.DTOs.LineNameIndexEntry>> GetLinesNeedingBackfillAsync();

    /// <summary>Cập nhật RIÊNG cột PARENT_ID cho NHIỀU Đường dây đã tồn tại cùng lúc (backfill) — không
    /// đụng các field khác, khác UpdateAsync/UpsertFromPmisAsync vốn cần đủ dữ liệu PMIS gốc của dòng đó.
    /// Trả về số dòng cập nhật thành công.</summary>
    Task<int> UpdateParentIdsAsync(IReadOnlyList<(Guid Id, Guid ParentId, int? GridTypeId)> items);
}
