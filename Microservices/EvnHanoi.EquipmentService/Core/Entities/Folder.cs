namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Thư mục tài liệu thiết bị — lưu cấu trúc cây thư mục gắn với từng đơn vị
/// </summary>
public class Folder
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public long UnitId { get; set; }  // ORGANIZATION_UNIT.ID is NUMBER (long)

    // Optimistic locking
    public int RowVersion { get; set; } = 1;

    // Audit fields
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;
}

/// <summary>
/// Tài liệu — lưu metadata tài liệu (file thực lưu ở MinIO)
/// </summary>
public class Document
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? FolderId { get; set; }
    public Guid? DossierId { get; set; }
    public Guid? DocumentTypeId { get; set; }

    // Status tracking (Active, Deleted)
    public string Status { get; set; } = "Active";

    // Optimistic locking
    public int RowVersion { get; set; } = 1;

    // Audit fields
    public string? CreatedBy { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;
}

