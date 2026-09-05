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
    Task<IReadOnlyList<Guid>> CloneDossiersAndDocumentsForDetailTransferAsync(Equipment sourceEquipment, Equipment replacementEquipment);
    Task<bool> UpdateAsync(Equipment equipment);
    Task<bool> ConfirmAsync(Guid id, string modifiedBy);
    Task<bool> UpdateAttributesAsync(Guid equipmentId, IEnumerable<AttributeValue> attributes);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<AttributeValue>> GetAttributesAsync(Guid equipmentId);
    
    // Lookups
    Task<IEnumerable<OrganizationDto>> GetOrganizationUnitsHierarchicalAsync(long? startUnitId);
    Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync(IEnumerable<long>? authorizedUnitIds = null);
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
    Task<EquipmentPmisUpsertResult> UpsertFromPmisAsync(
        string pmisCode, string code, string name, string? serialNumber,
        string equipmentTypeCode, string? parentPmisCode, string? unitCode,
        int? manufactureYear, string? qrCodeBase64, int? gridTypeId = null, string? equipmentTypeName = null);
}
