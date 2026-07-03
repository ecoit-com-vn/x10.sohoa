using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace EvnHanoi.IdentityService.Infrastructure.Security;

public class DynamicSeederService
{
    private readonly IApiDescriptionGroupCollectionProvider _apiExplorer;
    private readonly IDbConnection _connection;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMemoryCache _cache;

    public DynamicSeederService(
        IApiDescriptionGroupCollectionProvider apiExplorer,
        IDbConnection connection,
        IPermissionRepository permissionRepository,
        IMemoryCache cache)
    {
        _apiExplorer = apiExplorer;
        _connection = connection;
        _permissionRepository = permissionRepository;
        _cache = cache;
    }

    public async Task<List<string>> ScanAndSeedPermissionsAsync()
    {
        var logs = new List<string>();
        if (_connection.State != ConnectionState.Open) _connection.Open();

         // 1. Quét tất cả API Endpoints trong hệ thống
        var apiEndpoints = _apiExplorer.ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(api => api.ActionDescriptor.RouteValues.ContainsKey("controller"))
            .ToList();

        logs.Add($"🔍 Tìm thấy tổng số {apiEndpoints.Count} API Endpoints trong hệ thống.");

        // Từ điển dịch tên tài nguyên sang tiếng Việt thân thiện
        var friendlyResourceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Users", "Người dùng" },
            { "Roles", "Vai trò hệ thống" },
            { "Menus", "Thực đơn điều hướng (Menu)" },
            { "OrganizationUnits", "Đơn vị thành viên" },
            { "UserGroups", "Nhóm người dùng" },
            { "AuditLog", "Nhật ký hoạt động" },
            { "SystemParams", "Tham số hệ thống" },
            { "UploadConfigs", "Cấu hình tải lên" },
            { "Permissions", "Quản lý quyền hạt mịn" },
            { "Signatures", "Chữ ký số" },
            { "Box", "Hộp hồ sơ" },
            { "BorrowRecord", "Yêu cầu mượn trả hồ sơ" },
            { "Catalog", "Danh mục chung" },
            { "Dossier", "Hồ sơ thiết bị" },
            { "DossierCatalog", "Danh mục hồ sơ" },
            { "DossierSet", "Bộ hồ sơ" },
            { "DossierType", "Loại hồ sơ" },
            { "Document", "Văn bản tài liệu" },
            { "DocumentType", "Loại văn bản" },
            { "Digitization", "Hiệu đính số hóa OCR" },
            { "DigitizationTask", "Nhiệm vụ số hóa OCR" },
            { "Domain", "Lĩnh vực" },
            { "EavFormTemplate", "Form" },
            { "Equipment", "Thiết bị lưới điện" },
            { "EquipmentType", "Loại thiết bị" },
            { "Floor", "Tầng lưu trữ" },
            { "FormTemplate", "Biểu mẫu" },
            { "OcrTrainingData", "Dữ liệu huấn luyện AI OCR" },
            { "PhysicalStatus", "Tình trạng vật lý" },
            { "PhysicalStorage", "Kho lưu trữ vật lý" },
            { "Position", "Chức vụ" },
            { "PrivateCatalog", "Danh mục dùng riêng" },
            { "SharedCatalog", "Danh mục dùng chung" },
            { "Shelf", "Kệ lưu trữ" },
            { "Substation", "Trạm biến áp" },
            { "TransmissionLine", "Đường dây truyền tải" },
            { "VirtualFolder", "Thư mục ảo (Explorer)" },
            { "Workflow", "Quy trình hồ sơ" },
            { "WorkflowDefinitions", "Thiết lập quy trình" }, 
            { "DossierWorkflow", "Phê duyệt hồ sơ"},
            { "DossierPublish", "Xuất bản hồ sơ"}
        };

        var friendlyActionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "VIEW", "Xem" },
            { "CREATE", "Thêm mới" },
            { "EDIT", "Chỉnh sửa" },
            { "DELETE", "Xóa" },
            { "IMPORT", "Nhập tệp (Import)" },
            { "EXPORT", "Xuất tệp (Export)" },
            { "MANAGE", "Quản lý chuyên sâu" },
            { "RELEASE", "Xuất bản" },
        };

        int permCount = 0;
        int detailCount = 0;

        // Group endpoints by controller
        var endpointsByController = apiEndpoints
            .GroupBy(api => api.ActionDescriptor.RouteValues["controller"])
            .Where(g => !string.IsNullOrEmpty(g.Key) && !g.Key.Equals("Dev", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var controllerGroup in endpointsByController)
        {
            var controllerKey = controllerGroup.Key!;
            var controllerName = controllerKey + "Controller"; // e.g. UsersController
            
            // Standardize resource base name: e.g. "Users" -> "USER"
            string resourceBase = GetResourceBase(controllerKey);
            string resourceFriendly = friendlyResourceNames.TryGetValue(controllerKey, out var val) ? val : controllerKey;

            foreach (var api in controllerGroup)
            {
                var actionName = api.ActionDescriptor.RouteValues["action"] ?? "Unknown";
                var httpMethod = api.HttpMethod ?? "GET";

                // Phân loại hành động sang CRUD+
                string category = CategorizeAction(actionName, httpMethod);
                string permissionCode = $"{resourceBase}_{category}"; // e.g. USER_CREATE
                
                string permId = GenerateDeterministicGuid("PERM_" + permissionCode);
                string detailId = GenerateDeterministicGuid("DETAIL_" + controllerName + "_" + actionName + "_" + permissionCode);

                string permName = $"{friendlyActionNames[category]} {resourceFriendly.ToLower()}";
                string permDesc = $"Cho phép thực thi hành động '{actionName}' trên tài nguyên '{resourceFriendly}'";

                // 2. Chèn Quyền vào bảng PERMISSION nếu chưa có
                var existsPerm = await _connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM PERMISSION WHERE Code = :Code", new { Code = permissionCode });

                if (existsPerm == 0)
                {
                    await _connection.ExecuteAsync(@"
                        INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
                        VALUES (:Id, :Code, :Name, :Description, 1, 'SYSTEM')",
                        new { Id = permId, Code = permissionCode, Name = permName, Description = permDesc });
                    permCount++;
                }
                else
                {
                    // Lấy lại ID thực tế của permission hiện tại
                    permId = await _connection.QuerySingleAsync<string>(
                        "SELECT Id FROM PERMISSION WHERE Code = :Code", new { Code = permissionCode });
                }

                // 3. Chèn Chi tiết Quyền vào bảng PERMISSION_DETAIL nếu chưa có
                var existsDetail = await _connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM PERMISSION_DETAIL WHERE ControllerName = :ControllerName AND ActionName = :ActionName AND PermissionId = :PermissionId",
                    new { ControllerName = controllerName, ActionName = actionName, PermissionId = permId });

                if (existsDetail == 0)
                {
                    await _connection.ExecuteAsync(@"
                        INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
                        VALUES (:Id, :PermissionId, :ControllerName, :ActionName)",
                        new { Id = detailId, PermissionId = permId, ControllerName = controllerName, ActionName = actionName });
                    detailCount++;
                }
            }
        }

        logs.Add($"✨ Tự động tạo mới thành công {permCount} quyền PERMISSION.");
        logs.Add($"✨ Tự động tạo mới thành công {detailCount} chi tiết ánh xạ API PERMISSION_DETAIL.");

        return logs;
    }

    private string GetResourceBase(string controllerKey)
    {
        return controllerKey switch
        {
            "Menus" => "MENU",
            "Users" => "USER",
            "Roles" => "ROLE",
            "Permissions" => "PERMISSION",
            "OrganizationUnits" => "ORGANIZATION",
            "UploadConfigs" => "UPLOAD_CONFIG",
            "SystemParams" => "SYSTEM_PARAM",
            "UserGroups" => "USER_GROUP",
            "AuditLog" => "AUDIT_LOG",
            "Signatures" => "SIGNATURE",
            _ => controllerKey.ToUpperInvariant()
        };
    }

    private string CategorizeAction(string actionName, string httpMethod)
    {
        string actLower = actionName.ToLowerInvariant();

        if (actLower.Contains("submit") && (actLower.Contains("digitization")))
        {
            return "CREATE";
        }

        // 0. MANAGE (Explicit management actions like assignment/grant/revoke)
        if (actLower.Contains("assign") || actLower.Contains("grant") || actLower.Contains("revoke") || actLower.Contains("move"))
        {
            return "MANAGE";
        }
        
        // 1. IMPORT
        if (actLower.Contains("import") || actLower.Contains("upload") || actLower.Contains("extract") || actLower.Contains("ocr"))
        {
            return "IMPORT";
        }

        // 2. EXPORT
        if (actLower.Contains("export") || actLower.Contains("download"))
        {
            return "EXPORT";
        }

        // 3. DELETE
        if (httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase) || 
            actLower.StartsWith("delete") || actLower.StartsWith("remove") || actLower.StartsWith("destroy"))
        {
            return "DELETE";
        }

        // 4. EDIT
        if (httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) || 
            httpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase) ||
            actLower.StartsWith("update") || actLower.StartsWith("edit") || actLower.StartsWith("save") || actLower.StartsWith("patch"))
        {
            return "EDIT";
        }

        // 5. CREATE
        if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) || 
            actLower.StartsWith("create") || actLower.StartsWith("add") || actLower.StartsWith("insert"))
        {
            return "CREATE";
        }

        // 6. VIEW (Fallback for all GET requests)
        if (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) || 
            actLower.StartsWith("get") || actLower.StartsWith("find") || actLower.StartsWith("search") || actLower.StartsWith("load"))
        {
            return "VIEW";
        }

        // 7. RELEASE 
        if (actLower.Contains("publish"))
        {
            return "RELEASE";
        }

        // 8. MANAGE (General fallback)
        return "MANAGE";
    }

    private string GenerateDeterministicGuid(string input)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            byte[] guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, 16);
            return new Guid(guidBytes).ToString();
        }
    }
}
