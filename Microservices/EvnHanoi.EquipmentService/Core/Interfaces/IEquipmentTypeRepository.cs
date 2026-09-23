using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEquipmentTypeRepository
{
    Task<EquipmentTypeDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<EquipmentType>> GetAllAsync();
    Task<(IEnumerable<EquipmentTypeDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? code, string? name, int? gridTypeId, bool? isActive);
    Task<IEnumerable<GridType>> GetGridTypesAsync();
    Task<bool> CreateAsync(EquipmentType type);
    Task<bool> UpdateAsync(EquipmentType type);
    Task<bool> DeleteAsync(Guid id);
    
    Task<IEnumerable<AttributeDefinition>> GetAttributeDefinitionsAsync(Guid equipmentTypeId);
    Task<bool> AddAttributeDefinitionAsync(AttributeDefinition attributeDefinition);

    /// <summary>Nhãn tiếng Việt (JSON, field PMIS "tenThongSoKyThuat") đã lưu cho loại thiết bị này — null
    /// nếu chưa từng ghi (xem <see cref="SetPmisFieldLabelsIfEmptyAsync"/>).</summary>
    Task<string?> GetPmisFieldLabelsAsync(Guid equipmentTypeId);

    /// <summary>Ghi PmisFieldLabels CHỈ KHI cột đang rỗng (giống EquipmentRepository.SetFormValuesIfEmptyAsync)
    /// — nhãn PMIS là hằng số theo loại thiết bị nên chỉ cần lưu 1 lần, không cần ghi đè mỗi lần đồng bộ
    /// từng thiết bị. Trả true nếu vừa ghi (trước đó rỗng), false nếu đã có sẵn (bỏ qua).</summary>
    Task<bool> SetPmisFieldLabelsIfEmptyAsync(Guid equipmentTypeId, string fieldLabelsJson);
}
