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
            "SystemPermissionGroups" => "SYSTEM_PERMISSION_GROUP",
            "UnitPermissionGroups" => "UNIT_PERMISSION_GROUP",
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
            "SubstationSearch" => "SEARCH_SUBSTATION",
            "DossierSearch" => "SEARCH_DOSSIERS_IN_WAREHOUSE",
            "DossierCatalog" => "SEARCH_DOSSIERS_IN_WAREHOUSE",
            "ReportDossierByGridType" => "REPORT_DOSSIER_BY_GRIDTYPE",
            "ReportDossierByEquipment" => "REPORT_DOSSIER_BY_EQUIPMENT",
            "ReportDossierByStation" => "REPORT_DOSSIER_BY_STATION",
            "ReportDossierByLine" => "REPORT_DOSSIER_BY_LINE",
            "ReportGroup" => "REPORT_GROUP",
            "ReportGroups" => "REPORT_GROUP",
            "ReportUnitPublish" => "REPORT_UNIT_PUBLISH",
            "DocumentFullTextSearch" => "DOCUMENT_FULLTEXT_SEARCH",
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
        if (string.Equals(controllerKey, "ProcessingCategory", StringComparison.OrdinalIgnoreCase) &&
            (actionName.Equals("Lock", StringComparison.OrdinalIgnoreCase) ||
             actionName.Equals("Unlock", StringComparison.OrdinalIgnoreCase)))
        {
            return "EDIT";
        }

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

        // Tra cứu trạm biến áp: mọi GET → VIEW
        if (string.Equals(controllerKey, "SubstationSearch", StringComparison.OrdinalIgnoreCase) &&
            httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return "VIEW";
        }

        // Tìm kiếm hồ sơ trong kho: mọi GET → VIEW
        if ((string.Equals(controllerKey, "DossierSearch", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(controllerKey, "DossierCatalog", StringComparison.OrdinalIgnoreCase)) &&
            httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return "VIEW";
        }

        // Báo cáo hồ sơ thiết bị: GET danh sách/chi tiết/lookup → VIEW; export → EXPORT
        if (controllerKey.StartsWith("ReportDossierBy", StringComparison.OrdinalIgnoreCase))
        {
            if (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                var actLower = actionName.ToLowerInvariant();
                if (actLower.Contains("export"))
                    return "EXPORT";
                return "VIEW";
            }
        }

        // Tra cứu toàn văn tài liệu: mọi GET → VIEW (kể cả download-url)
        if (string.Equals(controllerKey, "DocumentFullTextSearch", StringComparison.OrdinalIgnoreCase) &&
            httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return "VIEW";
        }

        // EavFormApprovalController → EAV_FORM_APPROVAL_*
        if (string.Equals(controllerKey, "EavFormApproval", StringComparison.OrdinalIgnoreCase))
        {
            var actLower = actionName.ToLowerInvariant();
            if (actLower.Contains("approve") || actLower.Contains("reject") || actLower.Contains("restore"))
            {
                return "APPROVE";
            }
        }

        // EavFormTemplateController / FormTemplateController — restore phiên bản = EDIT
        if (string.Equals(controllerKey, "EavFormTemplate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(controllerKey, "FormTemplate", StringComparison.OrdinalIgnoreCase))
        {
            var actLower = actionName.ToLowerInvariant();
            if (actLower.Contains("submit"))
            {
                return "SUBMIT";
            }
            if (actLower.Contains("restore"))
            {
                return "EDIT";
            }
        }

        // EavCompletedFormController — restore = MANAGE
        if (string.Equals(controllerKey, "EavCompletedForm", StringComparison.OrdinalIgnoreCase))
        {
            var actLower = actionName.ToLowerInvariant();
            if (actLower.Contains("restore"))
            {
                return "MANAGE";
            }
        }

        // Thiết bị — OCR/bóc tách tài liệu hồ sơ liên quan → EQUIPMENT_EDIT (không DOSSIER_IMPORT)
        if (string.Equals(controllerKey, "Equipment", StringComparison.OrdinalIgnoreCase))
        {
            var actLower = actionName.ToLowerInvariant();
            if (actLower.Contains("document") &&
                (actLower.Contains("digitization") || actLower.Contains("extraction")))
            {
                return "EDIT";
            }
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
            "PROCESSING_CATEGORY_VIEW" => "Xem quy trình xử lý",
            "PROCESSING_CATEGORY_CREATE" => "Thêm quy trình xử lý",
            "PROCESSING_CATEGORY_EDIT" => "Sửa quy trình xử lý",
            "PROCESSING_CATEGORY_DELETE" => "Xóa quy trình xử lý",
            "DOSSIER_PUBLISH_RELEASE" => "Xuất bản hồ sơ",
            "DOSSIER_PUBLISH_VIEW" => "Xem xuất bản hồ sơ",
            "SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW" => "Tra cứu hồ sơ thiết bị",
            "SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW" => "Tìm kiếm hồ sơ trong kho",
            "SEARCH_SUBSTATION_VIEW" => "Tra cứu tìm kiếm Trạm biến áp",
            "REPORT_DOSSIER_BY_GRIDTYPE_VIEW" => "Xem báo cáo hồ sơ theo loại lưới điện",
            "REPORT_DOSSIER_BY_GRIDTYPE_EXPORT" => "Xuất Excel báo cáo theo loại lưới điện",
            "REPORT_GROUP_VIEW" => "Xem cấu hình nhóm báo cáo hệ thống",
            "REPORT_GROUP_CREATE" => "Thêm nhóm báo cáo hệ thống",
            "REPORT_GROUP_EDIT" => "Sửa nhóm báo cáo hệ thống",
            "REPORT_GROUP_DELETE" => "Xóa nhóm báo cáo hệ thống",
            "REPORT_UNIT_PUBLISH_VIEW" => "Xem cấu hình nhóm báo cáo đơn vị",
            "REPORT_UNIT_PUBLISH_EDIT" => "Lưu cấu hình nhóm báo cáo đơn vị",
            "REPORT_UNIT_PUBLISH_RELEASE" => "Công bố cấu hình nhóm báo cáo đơn vị",
            "REPORT_DOSSIER_BY_EQUIPMENT_VIEW" => "Xem báo cáo hồ sơ theo thiết bị",
            "REPORT_DOSSIER_BY_EQUIPMENT_EXPORT" => "Xuất Excel báo cáo theo thiết bị",
            "REPORT_DOSSIER_BY_STATION_VIEW" => "Xem báo cáo hồ sơ theo trạm",
            "REPORT_DOSSIER_BY_STATION_EXPORT" => "Xuất Excel báo cáo theo trạm",
            "REPORT_DOSSIER_BY_LINE_VIEW" => "Xem báo cáo hồ sơ theo đường dây",
            "REPORT_DOSSIER_BY_LINE_EXPORT" => "Xuất Excel báo cáo theo đường dây",
            "DOCUMENT_FULLTEXT_SEARCH_VIEW" => "Tra cứu toàn văn tài liệu",
            "DOCUMENT_IMPORT" => "Nhập tệp kho tài liệu thiết bị",
            "FOLDER_ALLOCATION_VIEW" => "Xem phân bổ nhập liệu",
            "FOLDER_ALLOCATION_EDIT" => "Cấu hình phân bổ nhập liệu",
            "DOSSIER_DIGITIZATION_VIEW" => "Xem hồ sơ số hóa",
            "DOSSIER_DIGITIZATION_CREATE" => "Tạo hồ sơ số hóa",
            "DOSSIER_DIGITIZATION_EDIT" => "Sửa hồ sơ số hóa",
            "DOSSIER_DIGITIZATION_DELETE" => "Xóa hồ sơ số hóa",
            "DOSSIER_DIGITIZATION_MANAGE" => "Quản lý quy trình hồ sơ số hóa",
            "EAV_FORM_TEMPLATE_VIEW" => "Xem cấu hình biểu mẫu",
            "EAV_FORM_TEMPLATE_CREATE" => "Tạo biểu mẫu",
            "EAV_FORM_TEMPLATE_EDIT" => "Chỉnh sửa biểu mẫu",
            "EAV_FORM_TEMPLATE_SUBMIT" => "Gửi duyệt biểu mẫu",
            "EAV_FORM_TEMPLATE_DELETE" => "Xóa biểu mẫu",
            "EAV_FORM_APPROVAL_VIEW" => "Xem hàng chờ phê duyệt biểu mẫu",
            "EAV_FORM_APPROVAL_APPROVE" => "Phê duyệt / từ chối biểu mẫu",
            "EAV_COMPLETED_FORM_VIEW" => "Xem danh sách form hoàn thành",
            "EAV_COMPLETED_FORM_MANAGE" => "Khóa / mở khóa biểu mẫu hoàn thành",
            "EAV_COMPLETED_FORM_DELETE" => "Xóa biểu mẫu hoàn thành",
            "AUDIT_LOG_VIEW" => "Xem nhật ký hệ thống",
            "AUDIT_LOG_DELETE" => "Xóa nhật ký hệ thống",
            "AUDIT_LOG_EXPORT" => "Xuất nhật ký hệ thống",
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
