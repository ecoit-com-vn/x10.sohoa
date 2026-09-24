using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(Guid id);
    Task<Equipment?> GetByCodeAsync(string code, Guid? infrastructureId);
    Task<EquipmentDto?> GetDtoByIdAsync(Guid id);
    Task<(IEnumerable<EquipmentExternalDto> Items, int TotalCount)> GetExternalListAsync(PmisEquipmentListRequestDto filter);
    Task<(IEnumerable<EquipmentDetailListDto> Items, int TotalCount)> GetExternalListWithItemsAsync(PmisEquipmentListRequestDto filter);
    Task<IEnumerable<Equipment>> GetAllAsync(IEnumerable<long>? unitIds = null);
    Task<(IEnumerable<EquipmentDto> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        string? keyword,
        string? code, 
        string? name, 
        long? unitId, 
        Guid? infrastructureId, 
        int? gridTypeId, 
        Guid? equipmentTypeId, 
        bool? isActive, 
        IEnumerable<long>? authorizedUnitIds);
    Task<bool> CreateWithAttributesAsync(Equipment equipment, IEnumerable<AttributeValue> attributes);
    Task<bool> CreateAsync(Equipment equipment);
    Task<bool> CloneForInfrastructureTransferAsync(Equipment sourceEquipment, Equipment replacementEquipment);
    Task<Equipment?> GetDetailTransferTargetAsync(Equipment sourceEquipment);
    /// <summary>
    /// Lịch sử di chuyển (đổi Trạm/Đường dây quản lý) của thiết bị, gộp theo <paramref name="equipmentCode"/>
    /// vì mỗi lần "Chuyển thiết bị" tạo bản ghi EQUIPMENTS mới (Id đổi), chỉ Code là bất biến qua các lần
    /// chuyển. Sắp xếp theo TransferredAt giảm dần (mới nhất lên đầu).
    /// </summary>
    Task<(IEnumerable<EquipmentTransferHistoryDto> Items, int TotalCount)> GetTransferHistoryAsync(
        string equipmentCode,
        Guid? infrastructureId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize);
    Task<IReadOnlyList<Guid>> CloneDossiersAndDocumentsForDetailTransferAsync(Equipment sourceEquipment, Equipment replacementEquipment);
    Task<bool> UpdateAsync(Equipment equipment);
    Task<bool> ConfirmAsync(Guid id, string modifiedBy);
    Task<bool> UpdateAttributesAsync(Guid equipmentId, IEnumerable<AttributeValue> attributes);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<AttributeValue>> GetAttributesAsync(Guid equipmentId);
    
    // Lookups
    Task<IEnumerable<OrganizationDto>> GetOrganizationUnitsHierarchicalAsync(long? startUnitId);
    Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync(IEnumerable<long>? authorizedUnitIds = null, string? keyword = null);
    Task<IEnumerable<EquipmentTypeDto>> GetEquipmentTypesLookupAsync();
    Task<(IEnumerable<EquipmentLookupItemDto> Items, int TotalCount)> GetLookupPagedAsync(
        EquipmentLookupFilterDto filter,
        IEnumerable<long>? authorizedUnitIds);
    Task<int> CountByInfrastructureIdAsync(Guid infrastructureId);

    /// <summary>
    /// Đồng bộ PMIS: tìm theo PmisCode, có thì cập nhật, chưa có thì tạo mới — chỉ cập nhật các cột
    /// định danh (Name/Code/SerialNumber/ManufactureYear/QrCode/InfrastructureId), KHÔNG đụng
    /// FormValues (dữ liệu người dùng chỉnh sửa nội bộ). Nếu chưa có ánh xạ loại thiết bị PMIS
    /// (PMIS_EQUIPMENT_TYPE_MAPPING) tương ứng thì TỰ ĐỘNG tạo cả loại thiết bị (EquipmentTypes, nếu
    /// Code theo quy ước chưa tồn tại) lẫn dòng ánh xạ — xem EquipmentRepository.ResolveOrCreateEquipmentTypeIdAsync.
    /// Chỉ còn Fail khi không xác định được cấp điện áp (capDienAp trống/không đọc được).
    /// <paramref name="gridTypeId"/>: cấp điện áp của thiết bị (1 = Cao áp, 2 = Trung áp, 3 = Hạ áp) dùng để tra
    /// đúng dòng ánh xạ loại thiết bị — nếu null (thiết bị đường dây không có capDienAp riêng) sẽ tự
    /// lấy theo GRIDTYPEID của Trạm/Đường dây cha (<paramref name="parentPmisCode"/>).
    /// <paramref name="equipmentTypeName"/>: tên loại thiết bị PMIS (tenLoaiTB) — dùng đặt Name khi phải
    /// tự tạo EquipmentTypes mới.
    /// </summary>
    /// <paramref name="prefetch"/>: kết quả PrefetchUpsertLookupsAsync cho CẢ LÔ chứa bản ghi này — nếu
    /// khác null, 4 lookup lặp lại (cha, loại thiết bị, đơn vị, bản ghi đã tồn tại) tra dictionary trong
    /// bộ nhớ thay vì tự SELECT (giảm N+1 khi gọi lặp cho nhiều bản ghi cùng 1 trang PMIS). null (mặc
    /// định) giữ nguyên hành vi cũ.
    Task<EquipmentPmisUpsertResult> UpsertFromPmisAsync(
        string pmisCode, string code, string name, string? serialNumber,
        string equipmentTypeCode, string? parentPmisCode, string? unitCode,
        int? manufactureYear, string? qrCodeBase64, int? gridTypeId = null, string? equipmentTypeName = null,
        EquipmentUpsertPrefetch? prefetch = null);

    /// <summary>Prefetch theo LÔ 4 lookup lặp lại của UpsertFromPmisAsync (cha, loại thiết bị, đơn vị, bản
    /// ghi đã tồn tại) — xem EquipmentUpsertPrefetch. Gọi 1 lần cho CẢ TRANG trước khi lặp gọi
    /// UpsertFromPmisAsync cho từng bản ghi trong trang.</summary>
    Task<EquipmentUpsertPrefetch> PrefetchUpsertLookupsAsync(
        IReadOnlyList<(string PmisCode, string? ParentPmisCode, string? UnitCode)> items);

    /// <summary>Ghi FormValues mặc định từ PMIS — CHỈ áp dụng khi thiết bị chưa từng có thông số nào
    /// (FORM_VALUES đang NULL), không bao giờ ghi đè dữ liệu người dùng đã tự nhập/sửa.</summary>
    Task<bool> SetFormValuesIfEmptyAsync(Guid equipmentId, string formValuesJson);

    /// <summary>Đếm số thiết bị bị đánh dấu "Đã chuyển TBA" (StatusTransition=0) bởi chính PMIS_SYNC trong
    /// <paramref name="sinceUtc"/> gần đây — dùng bởi PmisReconciliationJob (SyncService) làm compensating
    /// check nhẹ cho khả năng mất EquipmentTbaTransferredEvent nếu publish RabbitMQ lỗi ngay sau khi DB đã
    /// ghi thành công (xem InternalPmisSyncController.UpsertEquipmentFromPmis) — CHỈ log số lượng để đối
    /// chiếu thủ công, không tự động phát lại event (tránh trùng lặp nếu event thực ra đã gửi được).</summary>
    Task<int> CountRecentlyTransferredAsync(DateTime sinceUtc);
}
