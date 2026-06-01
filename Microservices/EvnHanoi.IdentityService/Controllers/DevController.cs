// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Controllers\DevController.cs
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
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/dev")]
public class DevController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public DevController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("ConnectionString DefaultConnection không được cấu hình.");
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

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

        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // =============================================
            // 1. Tạo Role ADMIN nếu chưa có
            // =============================================
            var adminRoleId = await connection.QuerySingleOrDefaultAsync<long?>(
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
                await connection.ExecuteAsync(insertRoleSql, roleParams, transaction);
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
            var userRoleId = await connection.QuerySingleOrDefaultAsync<long?>(
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
                await connection.ExecuteAsync(insertUserRoleSql, userRoleParams, transaction);
                results.Add($"✅ Tạo Role USER (Id={userRoleParams.Get<long>("Id")}) thành công.");
            }
            else
            {
                results.Add($"ℹ️ Role USER (Id={userRoleId}) đã tồn tại.");
            }

            // =============================================
            // 3. Gán tất cả Permissions cho Role ADMIN
            // =============================================
            await connection.ExecuteAsync(
                "DELETE FROM ROLE_PERMISSION WHERE RoleId = :RoleId",
                new { RoleId = adminRoleId },
                transaction);

            var allPermissions = new[]
            {
                "VIEW_DASHBOARD", "USER_MANAGE", "ROLE_MANAGE", "PERMISSION_MANAGE",
                "SYSTEM_PARAM_MANAGE", "ORGANIZATION_MANAGE", "CATALOG_MANAGE",
                "MENU_MANAGE", "USER_GROUP_MANAGE", "UPLOAD_CONFIG_MANAGE",
                "AUDIT_LOG_VIEW", "AUDIT_LOG_DELETE",
                "EQUIPMENT_VIEW", "EQUIPMENT_MANAGE",
                "DIGITIZATION_VIEW", "DIGITIZATION_MANAGE",
                "REPORT_VIEW", "REPORT_MANAGE", "REPORT_EXPORT"
            };

            var insertPermSql = "INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionCode) VALUES (:Id, :RoleId, :PermissionCode)";
            foreach (var perm in allPermissions)
            {
                await connection.ExecuteAsync(insertPermSql, new
                {
                    Id = Guid.NewGuid().ToString(),
                    RoleId = adminRoleId,
                    PermissionCode = perm
                }, transaction);
            }
            results.Add($"✅ Gán {allPermissions.Length} quyền cho Role ADMIN thành công.");

            // =============================================
            // 4. Lấy userId của admin
            // =============================================
            var adminUserId = await connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM APP_USER WHERE UserName = 'admin'",
                transaction: transaction);

            if (adminUserId == null)
            {
                results.Add("⚠️ Chưa tìm thấy user 'admin'. Hãy gọi POST /api/v1/auth/dev/init-admin trước.");
            }
            else
            {
                // =============================================
                // 5. Gán Role ADMIN cho user admin
                // =============================================
                var existingUserRole = await connection.QuerySingleOrDefaultAsync<long?>(
                    "SELECT UserId FROM USER_ROLE WHERE UserId = :UserId AND RoleId = :RoleId",
                    new { UserId = adminUserId, RoleId = adminRoleId },
                    transaction);

                if (existingUserRole == null)
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO USER_ROLE (UserId, RoleId) VALUES (:UserId, :RoleId)",
                        new { UserId = adminUserId, RoleId = adminRoleId },
                        transaction);
                    results.Add($"✅ Gán Role ADMIN cho user 'admin' (UserId={adminUserId}) thành công.");
                }
                else
                {
                    results.Add($"ℹ️ User 'admin' đã có Role ADMIN.");
                }
            }

            // =============================================
            // 6. Seed đầy đủ hệ thống Menu động (APP_MENU)
            // =============================================
            await connection.ExecuteAsync("DELETE FROM APP_MENU", transaction: transaction);
            results.Add("🗑️ Đã dọn dẹp bảng APP_MENU cũ để seed lại.");

            var insertMenuSql = @"
                INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
                VALUES (:Id, :Name, :Url, :Icon, :ParentId, :SortOrder, :IsActive, :PermissionCode)";

            var menuSeeds = new[]
            {
                new { Id = 1, Name = "Bảng điều khiển", Url = "/dashboard", Icon = "pi pi-home", ParentId = (int?)null, SortOrder = 1, IsActive = 1, PermissionCode = "VIEW_DASHBOARD" },
                
                new { Id = 2, Name = "Quản trị hệ thống", Url = (string)null, Icon = "pi pi-cog", ParentId = (int?)null, SortOrder = 2, IsActive = 1, PermissionCode = "USER_MANAGE" },
                new { Id = 3, Name = "Người dùng", Url = "/administration/user-management", Icon = "pi pi-users", ParentId = (int?)2, SortOrder = 1, IsActive = 1, PermissionCode = "USER_MANAGE" },
                new { Id = 4, Name = "Vai trò & Quyền", Url = "/administration/role-management", Icon = "pi pi-key", ParentId = (int?)2, SortOrder = 2, IsActive = 1, PermissionCode = "ROLE_MANAGE" },
                new { Id = 5, Name = "Cấu hình Menu", Url = "/administration/menu-management", Icon = "pi pi-list", ParentId = (int?)2, SortOrder = 3, IsActive = 1, PermissionCode = "MENU_MANAGE" },
                new { Id = 6, Name = "Nhóm người dùng", Url = "/administration/user-groups", Icon = "pi pi-user-plus", ParentId = (int?)2, SortOrder = 4, IsActive = 1, PermissionCode = "USER_GROUP_MANAGE" },
                new { Id = 7, Name = "Cấu hình Upload", Url = "/administration/upload-configuration", Icon = "pi pi-upload", ParentId = (int?)2, SortOrder = 5, IsActive = 1, PermissionCode = "UPLOAD_CONFIG_MANAGE" },
                new { Id = 8, Name = "Cơ cấu tổ chức", Url = "/administration/organization-settings", Icon = "pi pi-sitemap", ParentId = (int?)2, SortOrder = 6, IsActive = 1, PermissionCode = "ORGANIZATION_MANAGE" },
                new { Id = 9, Name = "Nhật ký hệ thống", Url = "/administration/audit-log", Icon = "pi pi-history", ParentId = (int?)2, SortOrder = 7, IsActive = 1, PermissionCode = "AUDIT_LOG_VIEW" },

                new { Id = 10, Name = "Danh mục hệ thống", Url = (string)null, Icon = "pi pi-folder-open", ParentId = (int?)null, SortOrder = 3, IsActive = 1, PermissionCode = "CATALOG_MANAGE" },
                new { Id = 11, Name = "Đơn vị tính", Url = "/catalog/unit-of-measurement", Icon = "pi pi-tag", ParentId = (int?)10, SortOrder = 1, IsActive = 1, PermissionCode = "CATALOG_MANAGE" },

                new { Id = 12, Name = "Hồ sơ & Thiết bị", Url = (string)null, Icon = "pi pi-file", ParentId = (int?)null, SortOrder = 4, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" },
                new { Id = 13, Name = "Quản lý thông số EAV", Url = "/equipment/form-management", Icon = "pi pi-sliders-h", ParentId = (int?)12, SortOrder = 1, IsActive = 1, PermissionCode = "EQUIPMENT_MANAGE" },

                new { Id = 14, Name = "Số hóa hồ sơ", Url = (string)null, Icon = "pi pi-cloud-upload", ParentId = (int?)null, SortOrder = 5, IsActive = 1, PermissionCode = "DIGITIZATION_VIEW" },
                new { Id = 15, Name = "Tải lên & Nhận dạng OCR", Url = "/digitization/ocr-upload", Icon = "pi pi-upload", ParentId = (int?)14, SortOrder = 1, IsActive = 1, PermissionCode = "DIGITIZATION_MANAGE" },
                new { Id = 16, Name = "Thư mục ảo (Explorer)", Url = "/digitization/virtual-folders", Icon = "pi pi-folder", ParentId = (int?)14, SortOrder = 2, IsActive = 1, PermissionCode = "DIGITIZATION_VIEW" },
                new { Id = 17, Name = "Phân bổ hồ sơ số hóa", Url = "/digitization/ocr-allocation", Icon = "pi pi-share-alt", ParentId = (int?)14, SortOrder = 3, IsActive = 1, PermissionCode = "DIGITIZATION_MANAGE" },
                new { Id = 18, Name = "Dữ liệu huấn luyện AI", Url = "/digitization/ocr-training", Icon = "pi pi-cog", ParentId = (int?)14, SortOrder = 4, IsActive = 1, PermissionCode = "DIGITIZATION_MANAGE" },

                new { Id = 19, Name = "Hiệu đính OCR", Url = "/ocr-correction", Icon = "pi pi-check-square", ParentId = (int?)null, SortOrder = 6, IsActive = 1, PermissionCode = "DIGITIZATION_MANAGE" },
                
                new { Id = 20, Name = "Tra cứu hồ sơ thiết bị", Url = "/search", Icon = "pi pi-search", ParentId = (int?)null, SortOrder = 7, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" },

                new { Id = 21, Name = "Mượn trả hồ sơ", Url = (string)null, Icon = "pi pi-envelope", ParentId = (int?)null, SortOrder = 8, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" },
                new { Id = 22, Name = "Yêu cầu mượn trả", Url = "/workflow/borrow-return", Icon = "pi pi-send", ParentId = (int?)21, SortOrder = 1, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" },
                new { Id = 23, Name = "Thiết lập quy trình duyệt", Url = "/workflow/builder", Icon = "pi pi-chart-line", ParentId = (int?)21, SortOrder = 2, IsActive = 1, PermissionCode = "ROLE_MANAGE" },

                new { Id = 24, Name = "Báo cáo & Thống kê", Url = "/reports", Icon = "pi pi-chart-bar", ParentId = (int?)null, SortOrder = 9, IsActive = 1, PermissionCode = "REPORT_VIEW" },
                
                new { Id = 25, Name = "Kho lưu trữ vật lý", Url = "/physical-storage", Icon = "pi pi-box", ParentId = (int?)null, SortOrder = 10, IsActive = 1, PermissionCode = "EQUIPMENT_VIEW" }
            };

            foreach (var menu in menuSeeds)
            {
                await connection.ExecuteAsync(insertMenuSql, menu, transaction);
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
}
