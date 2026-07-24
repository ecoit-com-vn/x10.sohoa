using System.Collections.Generic;
using System.Globalization;

namespace EvnHanoi.Infrastructure.Audit;

/// <summary>
/// Ánh xạ mã hành động/loại đối tượng kỹ thuật sang tên tiếng Việt hiển thị ở màn Nhật ký hệ thống.
/// </summary>
public static class AuditVietnameseLabels
{
    public static readonly IReadOnlyDictionary<string, string> ActionLabels = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        [AuditActions.Create] = "Thêm mới",
        [AuditActions.Update] = "Chỉnh sửa",
        [AuditActions.Delete] = "Xóa",
        [AuditActions.Manage] = "Khóa/Mở khóa",
        [AuditActions.Import] = "Tải lên",
        [AuditActions.Export] = "Tải xuống",
        [AuditActions.Release] = "Phát hành",
        [AuditActions.Login] = "Đăng nhập",
        [AuditActions.Logout] = "Đăng xuất",
        ["APPROVE"] = "Phê duyệt",
        ["SUBMIT"] = "Gửi phê duyệt",
        ["REJECT"] = "Từ chối",
        ["VIEW"] = "Xem",
    };

    /// <summary>
    /// Mã khớp CHÍNH XÁC với <c>PermissionCodeResolver.GetResourceBase</c> (special-case switch hoặc
    /// <c>ToSnakeCase(controllerName)</c> mặc định) — không được đoán/tự đặt tên khác thực tế controller sinh ra,
    /// nếu không dropdown filter "Loại đối tượng" sẽ không khớp cột "Đối tượng" ở lưới (đã từng xảy ra với
    /// STORAGE_BOX/STORAGE_SHELF/FOLDER — các mã không tồn tại thật, đã sửa lại đúng theo controller thực tế).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ResourceTypeLabels = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        // Hồ sơ / nghiệp vụ (LogGroup = NGHIEP_VU, resourceType bắt đầu "DOSSIER")
        ["DOSSIER"] = "Hồ sơ",
        ["DOSSIER_DIGITIZATION"] = "Hồ sơ số hóa",
        ["DOSSIER_SET"] = "Bộ hồ sơ",
        ["DOSSIER_TYPE"] = "Loại hồ sơ",
        ["DOSSIER_PUBLISH"] = "Xuất bản hồ sơ",

        // Tài liệu / thư mục / lưu trữ vật lý (LogGroup = THAO_TAC)
        ["DOCUMENT"] = "Tài liệu",
        ["DOCUMENT_TYPE"] = "Loại tài liệu",
        ["DOCUMENT_SEARCH"] = "Tìm kiếm tài liệu",
        ["DOCUMENT_FULLTEXT_SEARCH"] = "Tra cứu toàn văn tài liệu",
        ["VIRTUAL_FOLDER"] = "Thư mục",
        ["FOLDER_ALLOCATION"] = "Phân bổ nhập liệu",
        ["PHYSICAL_STORAGE"] = "Lưu trữ vật lý (kệ/tầng/hộp)",

        // Thiết bị / trạm / đường dây
        ["EQUIPMENT"] = "Thiết bị",
        ["EQUIPMENT_TYPE"] = "Loại thiết bị",
        ["SUBSTATION"] = "Trạm",
        ["TRANSMISSION_LINE"] = "Đường dây",
        ["SEARCH_SUBSTATION"] = "Tra cứu trạm biến áp",
        ["SEARCH_DOSSIERS_BY_EQUIPMENT"] = "Tra cứu hồ sơ theo thiết bị",
        ["SEARCH_DOSSIERS_IN_WAREHOUSE"] = "Tra cứu hồ sơ trong kho",

        // Danh mục hệ thống
        ["CATALOG"] = "Danh mục",
        ["SHARED_CATALOG"] = "Danh mục dùng chung",
        ["PRIVATE_CATALOG"] = "Danh mục riêng",
        ["DOMAIN"] = "Lĩnh vực",
        ["PROCESSING_CATEGORY"] = "Quy trình xử lý",
        ["PHYSICAL_STATUS"] = "Tình trạng vật lý",
        ["POSITION"] = "Chức vụ",

        // Số hóa / OCR / biểu mẫu động (EAV) — tên lấy đúng theo menu tương ứng (APP_MENU / permission Name)
        ["EAV_FORM_TEMPLATE"] = "Quản lý thông số EAV",
        ["EAV_COMPLETED_FORM"] = "Danh sách form hoàn thành",
        ["EAV_FORM_APPROVAL"] = "Phê duyệt biểu mẫu",

        // Quản trị / phân quyền
        ["USER"] = "Người dùng",
        ["USER_GROUP"] = "Nhóm người dùng",
        ["USER_UNIT_ROLES"] = "Vai trò đơn vị của người dùng",
        ["ORGANIZATION"] = "Đơn vị",
        ["ROLE"] = "Vai trò",
        ["PERMISSION"] = "Quyền",
        ["SYSTEM_PERMISSION_GROUP"] = "Nhóm quyền hệ thống",
        ["UNIT_PERMISSION_GROUP"] = "Nhóm quyền đơn vị",
        ["MENU"] = "Menu",
        ["SYSTEM_PARAM"] = "Tham số hệ thống",
        ["UPLOAD_CONFIG"] = "Cấu hình tải lên",
        ["SIGNATURE"] = "Chữ ký số",
        ["NOTIFICATIONS"] = "Thông báo",
        ["AUDIT_LOG"] = "Nhật ký hệ thống",
        ["AUTH"] = "Xác thực",

        // Workflow / mượn trả
        ["WORKFLOW"] = "Quy trình",
        ["WORKFLOW_DEFINITION"] = "Định nghĩa quy trình",

        // Báo cáo / thống kê
        ["REPORT"] = "Báo cáo",
        ["REPORT_GROUP"] = "Nhóm báo cáo hệ thống",
        ["REPORT_STATISTICS"] = "Thống kê báo cáo",
        ["REPORT_UNIT_PUBLISH"] = "Nhóm báo cáo theo đơn vị",
        // Khác — LOOKUP_TRACKING không có nhãn vì [SkipAudit] (LookupTrackingController), không bao giờ phát sinh log
        ["SYNC"] = "Đồng bộ dữ liệu (PMIS)",
    };

    public static string GetActionLabel(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return "Thao tác";

        return ActionLabels.TryGetValue(action, out var label) ? label : Humanize(action);
    }

    public static string GetResourceTypeLabel(string? resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            return "Đối tượng";

        return ResourceTypeLabels.TryGetValue(resourceType, out var label) ? label : Humanize(resourceType);
    }

    /// <summary>
    /// Fallback cho các mã chưa có trong bảng ánh xạ: "STORAGE_ROOM" -> "Storage room".
    /// </summary>
    private static string Humanize(string code)
    {
        var words = code.Replace('_', ' ').ToLower(CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(words) ? code : char.ToUpper(words[0], CultureInfo.InvariantCulture) + words[1..];
    }
}
