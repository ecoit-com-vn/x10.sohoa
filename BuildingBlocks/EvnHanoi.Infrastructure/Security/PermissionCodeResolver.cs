using System;
using System.Text;

namespace EvnHanoi.Infrastructure.Security;

/// <summary>
/// Ánh xạ controller + hành động → mã quyền hạt mịn (dùng chung Discovery + Filter).
/// </summary>
public static class PermissionCodeResolver
{
    public static string GetResourceBase(string controllerKey)
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
            "WorkflowDefinitions" => "WORKFLOW_DEFINITION",
            "DossierWorkflow" => "DOSSIER",
            "DossierDigitization" => "DOSSIER_DIGITIZATION",
            "DossierDigitizationWorkflow" => "DOSSIER_DIGITIZATION",
            "DossierByEquipment" => "SEARCH_DOSSIERS_BY_EQUIPMENT",
            "SearchDossiersByEquipment" => "SEARCH_DOSSIERS_BY_EQUIPMENT",
            _ => ToSnakeCase(controllerKey)
        };
    }

    public static string BuildPermissionCode(string controllerKey, string category, string? resourceBase = null)
    {
        resourceBase ??= GetResourceBase(controllerKey);
        return $"{resourceBase}_{category}";
    }

    /// <summary>
    /// Phân loại action → category quyền. RELEASE (publish/*) phải kiểm tra trước EDIT/PUT và trước revoke (unpublish chứa "revoke").
    /// </summary>
    public static string CategorizeAction(string controllerKey, string actionName, string httpMethod)
    {
        // Phân bổ nhập liệu: GET -> VIEW, các method khác (POST, PUT, DELETE, revoke...) -> EDIT
        if (string.Equals(controllerKey, "FolderAllocation", StringComparison.OrdinalIgnoreCase))
        {
            if (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return "VIEW";
            }
            return "EDIT";
        }

        // DossierPublish: mọi GET (GetPaged, GetTabCounts, …) → VIEW
        if (string.Equals(controllerKey, "DossierPublish", StringComparison.OrdinalIgnoreCase) &&
            httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return "VIEW";
        }

        // Tra cứu hồ sơ thiết bị: mọi GET → VIEW
        if ((string.Equals(controllerKey, "DossierByEquipment", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(controllerKey, "SearchDossiersByEquipment", StringComparison.OrdinalIgnoreCase)) &&
            httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return "VIEW";
        }

        return CategorizeAction(actionName, httpMethod);
    }

    public static string CategorizeAction(string actionName, string httpMethod)
    {
        string actLower = actionName.ToLowerInvariant();

        // 1. RELEASE — publish / unpublish / republish (PUT, phải trước EDIT và revoke)
        if (actLower.Contains("publish"))
        {
            return "RELEASE";
        }

        if (actLower.Contains("submit") && actLower.Contains("digitization"))
        {
            return "CREATE";
        }

        if (actLower.Contains("assign") || actLower.Contains("grant") ||
            actLower.Contains("revoke") || actLower.Contains("move") || actLower.Contains("lock") || actLower.Contains("reactivate"))
        {
            return "MANAGE";
        }

        if (actLower.Contains("import") || actLower.Contains("upload") || actLower.Contains("extract") || actLower.Contains("ocr"))
        {
            return "IMPORT";
        }

        if (actLower.Contains("export") || actLower.Contains("download"))
        {
            return "EXPORT";
        }

        if (httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase) ||
            actLower.StartsWith("delete") || actLower.StartsWith("remove") || actLower.StartsWith("destroy"))
        {
            return "DELETE";
        }

        if (httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            httpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase) ||
            actLower.StartsWith("update") || actLower.StartsWith("edit") || actLower.StartsWith("save") || actLower.StartsWith("patch"))
        {
            return "EDIT";
        }

        if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            actLower.StartsWith("create") || actLower.StartsWith("add") || actLower.StartsWith("insert"))
        {
            return "CREATE";
        }

        if (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
            actLower.StartsWith("get") || actLower.StartsWith("find") || actLower.StartsWith("search") || actLower.StartsWith("load"))
        {
            return "VIEW";
        }

        return "MANAGE";
    }

    public static string? GetFriendlyPermissionName(string permissionCode)
    {
        return permissionCode switch
        {
            "DOSSIER_PUBLISH_RELEASE" => "Xuất bản hồ sơ",
            "DOSSIER_PUBLISH_VIEW" => "Xem xuất bản hồ sơ",
            "SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW" => "Tra cứu hồ sơ thiết bị",
            "FOLDER_ALLOCATION_VIEW" => "Xem phân bổ nhập liệu",
            "FOLDER_ALLOCATION_EDIT" => "Cấu hình phân bổ nhập liệu",
            "DOSSIER_DIGITIZATION_VIEW" => "Xem hồ sơ số hóa",
            "DOSSIER_DIGITIZATION_CREATE" => "Tạo hồ sơ số hóa",
            "DOSSIER_DIGITIZATION_EDIT" => "Sửa hồ sơ số hóa",
            "DOSSIER_DIGITIZATION_DELETE" => "Xóa hồ sơ số hóa",
            "DOSSIER_DIGITIZATION_MANAGE" => "Quản lý quy trình hồ sơ số hóa",
            _ => null
        };
    }

    public static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (i > 0 && char.IsUpper(c))
            {
                if (input[i - 1] != '_' && (!char.IsUpper(input[i - 1]) || (i + 1 < input.Length && char.IsLower(input[i + 1]))))
                {
                    sb.Append('_');
                }
            }
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }
}
