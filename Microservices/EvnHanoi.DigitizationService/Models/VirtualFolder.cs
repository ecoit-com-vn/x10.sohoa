using System;

namespace EvnHanoi.DigitizationService.Models
{
    public class VirtualFolder
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long? ParentId { get; set; }
        public long? UnitId { get; set; }
        public string? EquipmentId { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
