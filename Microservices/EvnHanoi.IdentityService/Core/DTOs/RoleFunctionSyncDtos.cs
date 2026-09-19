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

/// <summary>1 dòng user cho API "Danh sách đồng bộ user" — RoleIds là chuỗi RoleId nối dấu phẩy
/// (vd. "1,2,8"), hợp nhất từ USER_ROLE + USER_UNIT_ROLE + USER_GROUP_MEMBER/USER_GROUP_ROLE.</summary>
public class UserSyncItemDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? SsoNsId { get; set; }
    public string? RoleIds { get; set; }
    public long? OrganizationUnitId { get; set; }
    public string? SsoDeptId { get; set; }
    public long? PositionId { get; set; }
}
