using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Thực thể phân bổ nhập liệu thư mục cho người dùng (Folder User Allocation)
/// </summary>
public class FolderUserAllocation
{
    public Guid Id { get; set; }
    public Guid FolderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long UnitId { get; set; }

    /// <summary>
    /// Trạng thái phân bổ: 'Active' | 'Revoked'
    /// </summary>
    public string Status { get; set; } = "Active";

    // Optimistic locking
    public int RowVersion { get; set; } = 1;

    // Audit fields
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;
}
