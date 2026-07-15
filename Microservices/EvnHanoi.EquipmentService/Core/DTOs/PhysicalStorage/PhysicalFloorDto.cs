using System;

namespace EvnHanoi.EquipmentService.Core.DTOs.PhysicalStorage
{
    public class PhysicalFloorDto
    {
        public long Id { get; set; }
        public long ShelfId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Status { get; set; } = 1; // 1=Active, 0=Locked
        public bool IsDeleted { get; set; } = false;
        public int Priority { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
