namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

internal static class EquipmentSqlFilters
{
    /// <summary>Loại thiết bị "hồn ma" (StatusTransition=0, "Đã chuyển TBA" — bị thay thế bởi
    /// CloneForInfrastructureTransferAsync khi thiết bị chuyển Trạm/Đường dây, giữ nguyên INFRASTRUCTURE_ID
    /// cũ) khỏi mọi truy vấn liệt kê/đếm thiết bị đang hoạt động — KHÔNG loại StatusTransition=1
    /// ("Đã chuyển hồ sơ"), vì đó vẫn là thiết bị sống thật (INFRASTRUCTURE_ID/IS_ACTIVE không đổi), chỉ
    /// hồ sơ được copy sang bản ghi khác (xem EquipmentController.CreateDetailFromById). Nhận alias bảng
    /// EQUIPMENTS trong câu SQL đang ghép (rỗng nếu không cần/không có alias) — vd
    /// NotTransferredAway("e") -&gt; "(e.StatusTransition IS NULL OR e.StatusTransition &lt;&gt; 0)".</summary>
    public static string NotTransferredAway(string alias = "")
    {
        var prefix = string.IsNullOrEmpty(alias) ? string.Empty : $"{alias}.";
        return $"({prefix}StatusTransition IS NULL OR {prefix}StatusTransition <> 0)";
    }
}
