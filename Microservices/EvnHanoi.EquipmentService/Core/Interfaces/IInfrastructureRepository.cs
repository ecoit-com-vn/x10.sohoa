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
    /// <summary>parentPmisCode: mã PMIS của Đường dây CHA (field "maCha", chỉ có ý nghĩa với InfraTypeId=2)
    /// — server tự SELECT INFRASTRUCTURE theo mã này để lấy Id + GridTypeId (mượn tạm cho nhánh không có
    /// capDienAp riêng), giống hệt cách ParentPmisCode được resolve cho Thiết bị (xem
    /// EquipmentRepository.UpsertFromPmisAsync). null/rỗng nghĩa là đường trục gốc.
    /// ParentUnresolved (trả về) = true khi parentPmisCode CÓ giá trị nhưng KHÔNG khớp được (đường trục
    /// chưa đồng bộ tới, mã sai, hoặc parentPmisCode tự trỏ về chính dòng này) — caller (SyncService) dùng
    /// để ghi 1 dòng Warning thấy được trong "Lịch sử đồng bộ" thay vì âm thầm mãi mãi.</summary>
    Task<(Guid Id, bool WasCreated, bool HasChanged, bool ParentUnresolved)> UpsertFromPmisAsync(
        int infraTypeId, string pmisCode, string code, string name, string? address, string? unitCode, DateTime? operationDate,
        int? gridTypeId = null, string? parentPmisCode = null, string? cmisCode = null);

    /// <summary>Danh sách PmisCode đã đồng bộ (dùng cho auto-sync Thiết bị — lặp qua từng Trạm/Đường dây đã có để lấy thiết bị con).</summary>
    Task<IEnumerable<(string PmisCode, int InfraTypeId)>> GetSyncedPmisCodesAsync();
}
