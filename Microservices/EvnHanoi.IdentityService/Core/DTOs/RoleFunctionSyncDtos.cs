namespace EvnHanoi.IdentityService.Core.DTOs;

/// <summary>1 dòng ánh xạ Vai trò ↔ Chức năng cho API đồng bộ — suy ra qua chuỗi
/// ROLE_PERMISSION_GROUP → PERMISSION_GROUP_PERMISSION → PERMISSION.Code = APP_MENU.PermissionCode.</summary>
public class RoleFunctionAssignmentDto
{
    public long RoleId { get; set; }
    public long FuncId { get; set; }
}

/// <summary>Tham số API "Thêm mới vai trò" đồng bộ — chỉ nhận Tên vai trò, Code được tự sinh.</summary>
public class SyncCreateRoleRequest
{
    public string Title { get; set; } = string.Empty;
}

/// <summary>Tham số API "Sửa vai trò" đồng bộ.</summary>
public class SyncUpdateRoleRequest
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

/// <summary>Tham số API "Xóa vai trò" đồng bộ.</summary>
public class SyncDeleteRoleRequest
{
    public long Id { get; set; }
}
