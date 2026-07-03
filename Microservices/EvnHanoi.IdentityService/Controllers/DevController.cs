// [CHỈ DÙNG TRONG DEVELOPMENT] Controller seed dữ liệu khởi tạo hệ thống.
// Xóa hoặc disable controller này trước khi deploy lên production.

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/dev")]
[AllowAnonymous]
public class DevController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IDbConnection _connection;
    private readonly EvnHanoi.IdentityService.Infrastructure.Security.DynamicSeederService _seederService;

    public DevController(
        IConfiguration configuration, 
        IDbConnection connection, 
        EvnHanoi.IdentityService.Infrastructure.Security.DynamicSeederService seederService)
    {
        _configuration = configuration;
        _connection = connection;
        _seederService = seederService;
    }

    /// <summary>
    /// Seed toàn bộ dữ liệu ban đầu: Role ADMIN + Permissions + Gán cho user admin.
    /// Chỉ chạy trong môi trường Development.
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> SeedInitialData()
    {
        // Chỉ cho phép trong môi trường Development
        if (!HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment())
        {
            return NotFound();
        }

        var results = new List<string>();

        try
        {
            // 0. Quét hệ thống tự động chèn Permissions & Details trước
            var scanLogs = await _seederService.ScanAndSeedPermissionsAsync();
            results.AddRange(scanLogs);
        }
        catch (Exception ex)
        {
            results.Add($"⚠️ Lỗi quét phân quyền tự động: {ex.Message}");
        }

        if (_connection.State != ConnectionState.Open) _connection.Open();
        using var transaction = _connection.BeginTransaction();

        try
        {
            // =============================================
            // 1. Tạo Role ADMIN nếu chưa có
            // =============================================
            var adminRoleId = await _connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM ROLE WHERE Code = 'ADMIN'",
                transaction: transaction);

            if (adminRoleId == null)
            {
                var insertRoleSql = @"
                    INSERT INTO ROLE (Code, Name, Description, CreatedBy)
                    VALUES ('ADMIN', 'Quản trị viên hệ thống', 'Tài khoản có toàn quyền trên hệ thống', 'SYSTEM')
                    RETURNING Id INTO :Id";

                var roleParams = new DynamicParameters();
                roleParams.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
                await _connection.ExecuteAsync(insertRoleSql, roleParams, transaction);
                adminRoleId = roleParams.Get<long>("Id");
                results.Add($"✅ Tạo Role ADMIN (Id={adminRoleId}) thành công.");
            }
            else
            {
                results.Add($"ℹ️ Role ADMIN (Id={adminRoleId}) đã tồn tại.");
            }

            // =============================================
            // 2. Tạo Role USER nếu chưa có
            // =============================================
            var userRoleId = await _connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM ROLE WHERE Code = 'USER'",
                transaction: transaction);

            if (userRoleId == null)
            {
                var insertUserRoleSql = @"
                    INSERT INTO ROLE (Code, Name, Description, CreatedBy)
                    VALUES ('USER', 'Người dùng', 'Tài khoản người dùng thông thường', 'SYSTEM')
                    RETURNING Id INTO :Id";

                var userRoleParams = new DynamicParameters();
                userRoleParams.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
                await _connection.ExecuteAsync(insertUserRoleSql, userRoleParams, transaction);
                userRoleId = userRoleParams.Get<long>("Id");
                results.Add($"✅ Tạo Role USER (Id={userRoleId}) thành công.");
            }
            else
            {
                results.Add($"ℹ️ Role USER (Id={userRoleId}) đã tồn tại.");
            }

            // =============================================
            // 2b. Tạo Role OPERATOR nếu chưa có
            // =============================================
            var operatorRoleId = await _connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM ROLE WHERE Code = 'OPERATOR'",
                transaction: transaction);

            if (operatorRoleId == null)
            {
                var insertOpRoleSql = @"
                    INSERT INTO ROLE (Code, Name, Description, CreatedBy)
                    VALUES ('OPERATOR', 'Nhân viên vận hành', 'Tài khoản nhân viên vận hành hệ thống', 'SYSTEM')
                    RETURNING Id INTO :Id";

                var opRoleParams = new DynamicParameters();
                opRoleParams.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
                await _connection.ExecuteAsync(insertOpRoleSql, opRoleParams, transaction);
                operatorRoleId = opRoleParams.Get<long>("Id");
                results.Add($"✅ Tạo Role OPERATOR (Id={operatorRoleId}) thành công.");
            }
            else
            {
                results.Add($"ℹ️ Role OPERATOR (Id={operatorRoleId}) đã tồn tại.");
            }

            // =============================================
            // 3. Gán tất cả Permissions cho Role ADMIN
            // =============================================
            await _connection.ExecuteAsync(
                "DELETE FROM ROLE_PERMISSION WHERE RoleId = :RoleId",
                new { RoleId = adminRoleId },
                transaction);

            var allPermissions = new[]
            {
                "VIEW_DASHBOARD", 
                "USER_VIEW", "USER_MANAGE", 
                "ROLE_VIEW", "ROLE_MANAGE", "PERMISSION_MANAGE",
                "SYSTEM_PARAM_VIEW", "SYSTEM_PARAM_MANAGE", 
                "ORGANIZATION_VIEW", "ORGANIZATION_MANAGE", 
                "CATALOG_VIEW", "CATALOG_MANAGE",
                "MENU_VIEW", "MENU_MANAGE", 
                "USER_GROUP_VIEW", "USER_GROUP_MANAGE", 
                "UPLOAD_CONFIG_VIEW", "UPLOAD_CONFIG_MANAGE",
                "AUDIT_LOG_VIEW", "AUDIT_LOG_DELETE",
                "EQUIPMENT_VIEW", "EQUIPMENT_MANAGE",
                "DIGITIZATION_VIEW", "DIGITIZATION_MANAGE",
                "REPORT_VIEW", "REPORT_MANAGE", "REPORT_EXPORT"
            };

            // Truy vấn danh sách Permission active để lấy ID tương ứng với Code
            var permissions = await _connection.QueryAsync<(string Id, string Code)>(
                "SELECT Id, Code FROM PERMISSION WHERE IsActive = 1", 
                transaction: transaction);
            
            var codeToIdMap = permissions.ToDictionary(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase);

            var insertPermSql = "INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionId) VALUES (:Id, :RoleId, :PermissionId)";
            int insertCount = 0;
            foreach (var code in allPermissions)
            {
                if (codeToIdMap.TryGetValue(code, out var permissionId))
                {
                    var id = Guid.NewGuid().ToString();
                    await _connection.ExecuteAsync(insertPermSql, new
                    {
                        Id = id,
                        RoleId = adminRoleId,
                        PermissionId = permissionId
                    }, transaction);
                    insertCount++;
                }
            }
            results.Add($"✅ Gán {insertCount} trên tổng số {allPermissions.Length} quyền cho Role ADMIN thành công.");

            // =============================================
            // 4. Đảm bảo APP_USER 'admin' và 'operator' tồn tại
            // =============================================
            var adminUserId = await _connection.QuerySingleOrDefaultAsync<string>(
                "SELECT Id FROM APP_USER WHERE UserName = 'admin'",
                transaction: transaction);

            if (string.IsNullOrEmpty(adminUserId))
            {
                adminUserId = "018fc1e0-0000-0000-0000-000000000000";
                await _connection.ExecuteAsync(@"
                    INSERT INTO APP_USER (Id, UserName, Email, FullName, PasswordHash, IsActive, LockoutEnabled, AccessFailedCount, CreatedBy)
                    VALUES (:Id, 'admin', 'admin@evnhanoi.vn', N'Quản trị viên Hệ thống', '$2a$11$uLpFt/IGMdFQH.zRBNT1uORBf5u3b3qnFfQygBH2Y4fFKKsAFl/3K', 1, 0, 0, 'SYSTEM')",
                    new { Id = adminUserId },
                    transaction);
                results.Add($"✅ Tạo mới user 'admin' (Id={adminUserId}) thành công.");
            }

            var operatorUserId = await _connection.QuerySingleOrDefaultAsync<string>(
                "SELECT Id FROM APP_USER WHERE UserName = 'operator'",
                transaction: transaction);

            if (string.IsNullOrEmpty(operatorUserId))
            {
                operatorUserId = "018fc1e0-0000-0000-0000-000000000001";
                await _connection.ExecuteAsync(@"
                    INSERT INTO APP_USER (Id, UserName, Email, FullName, PasswordHash, IsActive, LockoutEnabled, AccessFailedCount, CreatedBy)
                    VALUES (:Id, 'operator', 'operator@evnhanoi.vn', N'Nhân viên Vận hành', '$2a$11$uLpFt/IGMdFQH.zRBNT1uORBf5u3b3qnFfQygBH2Y4fFKKsAFl/3K', 1, 1, 0, 'SYSTEM')",
                    new { Id = operatorUserId },
                    transaction);
                results.Add($"✅ Tạo mới user 'operator' (Id={operatorUserId}) thành công.");
            }

            // =============================================
            // 5. Gán Roles cho các User
            // =============================================
            var existingUserRole = await _connection.QuerySingleOrDefaultAsync<string>(
                "SELECT UserId FROM USER_ROLE WHERE UserId = :UserId AND RoleId = :RoleId",
                new { UserId = adminUserId, RoleId = adminRoleId },
                transaction);

            if (string.IsNullOrEmpty(existingUserRole))
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO USER_ROLE (UserId, RoleId) VALUES (:UserId, :RoleId)",
                    new { UserId = adminUserId, RoleId = adminRoleId },
                    transaction);
                results.Add($"✅ Gán Role ADMIN cho user 'admin' (UserId={adminUserId}) thành công.");
            }
            else
            {
                results.Add($"ℹ️ User 'admin' đã có Role ADMIN.");
            }

            var existingOpUserRole = await _connection.QuerySingleOrDefaultAsync<string>(
                "SELECT UserId FROM USER_ROLE WHERE UserId = :UserId AND RoleId = :RoleId",
                new { UserId = operatorUserId, RoleId = operatorRoleId },
                transaction);

            if (string.IsNullOrEmpty(existingOpUserRole))
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO USER_ROLE (UserId, RoleId) VALUES (:UserId, :RoleId)",
                    new { UserId = operatorUserId, RoleId = operatorRoleId },
                    transaction);
                results.Add($"✅ Gán Role OPERATOR cho user 'operator' (UserId={operatorUserId}) thành công.");
            }
            else
            {
                results.Add($"ℹ️ User 'operator' đã có Role OPERATOR.");
            }

            // =============================================
            // 6. Seed đầy đủ hệ thống Menu động (APP_MENU)
            // =============================================
            await _connection.ExecuteAsync("DELETE FROM APP_MENU", transaction: transaction);
            results.Add("🗑️ Đã dọn dẹp bảng APP_MENU cũ để seed lại.");

            var insertMenuSql = @"
                INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
                VALUES (:Id, :Name, :Url, :Icon, :ParentId, :SortOrder, :IsActive, :PermissionCode)";

            var menuSeeds = new[]
            {
                new { Id = 1, Name = "Bảng điều khiển", Url = (string?)"/dashboard", Icon = "pi pi-home", ParentId = (int?)null, SortOrder = 1, IsActive = 1, PermissionCode = "VIEW_DASHBOARD" },
                
                new { Id = 2, Name = "Quản trị hệ thống", Url = (string?)null, Icon = "pi pi-cog", ParentId = (int?)null, SortOrder = 2, IsActive = 1, PermissionCode = "USER_VIEW" },
                new { Id = 3, Name = "Người dùng", Url = (string?)"/administration/user-management", Icon = "pi pi-users", ParentId = (int?)2, SortOrder = 1, IsActive = 1, PermissionCode = "USER_VIEW" },
                new { Id = 4, Name = "Vai trò & Quyền", Url = (string?)"/administration/role-management", Icon = "pi pi-key", ParentId = (int?)2, SortOrder = 2, IsActive = 1, PermissionCode = "ROLE_VIEW" },
                new { Id = 5, Name = "Cấu hình Menu", Url = (string?)"/administration/menu-management", Icon = "pi pi-list", ParentId = (int?)2, SortOrder = 3, IsActive = 1, PermissionCode = "MENU_VIEW" },
                new { Id = 6, Name = "Nhóm người dùng", Url = (string?)"/administration/user-groups", Icon = "pi pi-user-plus", ParentId = (int?)2, SortOrder = 4, IsActive = 1, PermissionCode = "USER_GROUP_VIEW" },
                new { Id = 7, Name = "Cấu hình Upload", Url = (string?)"/administration/upload-configuration", Icon = "pi pi-upload", ParentId = (int?)2, SortOrder = 5, IsActive = 1, PermissionCode = "UPLOAD_CONFIG_VIEW" },
                new { Id = 8, Name = "Cơ cấu tổ chức", Url = (string?)"/administration/organization-settings", Icon = "pi pi-sitemap", ParentId = (int?)2, SortOrder = 6, IsActive = 1, PermissionCode = "ORGANIZATION_VIEW" },
                new { Id = 9, Name = "Nhật ký hệ thống", Url = (string?)"/administration/audit-log", Icon = "pi pi-history", ParentId = (int?)2, SortOrder = 7, IsActive = 1, PermissionCode = "AUDIT_LOG_VIEW" },

                new { Id = 10, Name = "Danh mục hệ thống", Url = (string?)null, Icon = "pi pi-folder-open", ParentId = (int?)null, SortOrder = 3, IsActive = 1, PermissionCode = "CATALOG_VIEW" },
                new { Id = 11, Name = "Đơn vị tính", Url = (string?)"/catalog/unit-of-measurement", Icon = "pi pi-tag", ParentId = (int?)10, SortOrder = 1, IsActive = 1, PermissionCode = "CATALOG_VIEW" },

                new { Id = 12, Name = "Hồ sơ & Thiết bị", Url = (string?)null, Icon = "pi pi-file", ParentId = (int?)null, SortOrder = 4, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" },
                new { Id = 13, Name = "Quản lý thông số EAV", Url = (string?)"/equipment/form-management", Icon = "pi pi-sliders-h", ParentId = (int?)12, SortOrder = 1, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" },

                new { Id = 14, Name = "Số hóa hồ sơ", Url = (string?)null, Icon = "pi pi-cloud-upload", ParentId = (int?)null, SortOrder = 5, IsActive = 1, PermissionCode = "DIGITIZATION_VIEW" },
                new { Id = 15, Name = "Tải lên & Nhận dạng OCR", Url = (string?)"/digitization/ocr-upload", Icon = "pi pi-upload", ParentId = (int?)14, SortOrder = 1, IsActive = 1, PermissionCode = "DIGITIZATION_VIEW" },
                new { Id = 16, Name = "Thư mục ảo (Explorer)", Url = (string?)"/digitization/virtual-folders", Icon = "pi pi-folder", ParentId = (int?)14, SortOrder = 2, IsActive = 1, PermissionCode = "DIGITIZATION_VIEW" },
                new { Id = 17, Name = "Phân bổ hồ sơ số hóa", Url = (string?)"/digitization/ocr-allocation", Icon = "pi pi-share-alt", ParentId = (int?)14, SortOrder = 3, IsActive = 1, PermissionCode = "DIGITIZATION_VIEW" },
                new { Id = 18, Name = "Dữ liệu huấn luyện AI", Url = (string?)"/digitization/ocr-training", Icon = "pi pi-cog", ParentId = (int?)14, SortOrder = 4, IsActive = 1, PermissionCode = "DIGITIZATION_VIEW" },

                new { Id = 19, Name = "Hiệu đính OCR", Url = (string?)"/ocr-correction", Icon = "pi pi-check-square", ParentId = (int?)null, SortOrder = 6, IsActive = 1, PermissionCode = "DIGITIZATION_VIEW" },
                
                new { Id = 20, Name = "Tra cứu hồ sơ thiết bị", Url = (string?)"/search/dossier-by-equipment", Icon = "pi pi-search", ParentId = (int?)null, SortOrder = 7, IsActive = 1, PermissionCode = "SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW" },

                new { Id = 21, Name = "Mượn trả hồ sơ", Url = (string?)null, Icon = "pi pi-envelope", ParentId = (int?)null, SortOrder = 8, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" },
                new { Id = 22, Name = "Yêu cầu mượn trả", Url = (string?)"/workflow/borrow-return", Icon = "pi pi-send", ParentId = (int?)21, SortOrder = 1, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" },
                new { Id = 23, Name = "Cài đặt quy trình", Url = (string?)"/administration/workflow-builder", Icon = "pi pi-sitemap", ParentId = (int?)2, SortOrder = 8, IsActive = 1, PermissionCode = "ROLE_VIEW" },

                new { Id = 24, Name = "Báo cáo & Thống kê", Url = (string?)"/reports", Icon = "pi pi-chart-bar", ParentId = (int?)null, SortOrder = 9, IsActive = 1, PermissionCode = "REPORT_VIEW" },
                
                new { Id = 25, Name = "Kho lưu trữ vật lý", Url = (string?)"/physical-storage", Icon = "pi pi-box", ParentId = (int?)null, SortOrder = 10, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" }
            };

            foreach (var menu in menuSeeds)
            {
                await _connection.ExecuteAsync(insertMenuSql, menu, transaction);
            }
            results.Add($"✅ Khởi tạo thành công {menuSeeds.Length} Menu chức năng.");

            transaction.Commit();

            return Ok(new
            {
                message = "Seed dữ liệu ban đầu hoàn tất.",
                results,
                loginInfo = new
                {
                    username = "admin",
                    password = "Admin@123!",
                    note = "Hãy đổi mật khẩu ngay sau khi đăng nhập lần đầu!"
                }
            });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(500, new { message = $"Lỗi seed dữ liệu: {ex.Message}" });
        }
    }

    /// <summary>
    /// Quét toàn bộ controllers/actions hệ thống để tự động sinh danh mục phân quyền CRUD+
    /// </summary>
    [HttpPost("scan-and-seed-permissions")]
    public async Task<IActionResult> ScanAndSeedPermissions()
    {
        // Chỉ cho phép trong môi trường Development
        if (!HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var logs = await _seederService.ScanAndSeedPermissionsAsync();

            // Gán tất cả Permissions active cho Role ADMIN tự động
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var adminRoleId = await _connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM ROLE WHERE Code = 'ADMIN'");
            if (adminRoleId.HasValue)
            {
                await _connection.ExecuteAsync(
                    "DELETE FROM ROLE_PERMISSION WHERE RoleId = :RoleId",
                    new { RoleId = adminRoleId.Value });

                var permissions = await _connection.QueryAsync<string>(
                    "SELECT Id FROM PERMISSION WHERE IsActive = 1");

                var insertPermSql = "INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionId) VALUES (:Id, :RoleId, :PermissionId)";
                int insertCount = 0;
                foreach (var permId in permissions)
                {
                    var id = Guid.NewGuid().ToString();
                    await _connection.ExecuteAsync(insertPermSql, new
                    {
                        Id = id,
                        RoleId = adminRoleId.Value,
                        PermissionId = permId
                    });
                    insertCount++;
                }
                logs.Add($"✅ Tự động liên kết gán {insertCount} quyền mới quét được cho vai trò ADMIN thành công.");
            }

            return Ok(new
            {
                message = "Quét tự động và seed phân quyền hoàn tất thành công!",
                logs
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Lỗi quét và seed phân quyền: {ex.Message}" });
        }
    }
}
