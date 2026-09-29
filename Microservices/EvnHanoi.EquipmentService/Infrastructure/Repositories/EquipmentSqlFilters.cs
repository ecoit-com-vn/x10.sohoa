namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

internal static class EquipmentSqlFilters
{
    /// <summary>Loại thiết bị "hồn ma" (StatusTransition=0, "Đã chuyển TBA" — bị thay thế bởi
    /// CloneForInfrastructureTransferAsync khi thiết bị chuyển Trạm/Đường dây, giữ nguyên INFRASTRUCTURE_ID
    /// cũ) khỏi mọi truy vấn liệt kê/đếm thiết bị đang hoạt động — KHÔNG loại StatusTransition=1
    /// ("Đã chuyển hồ sơ"), vì đó vẫn là thiết bị sống thật (INFRASTRUCTURE_ID/IS_ACTIVE không đổi), chỉ
    /// hồ sơ được copy sang bản ghi khác (xem EquipmentController.CreateDetailFromById). Nhận alias bảng
    /// EQUIPMENTS trong câu SQL đang ghép (rỗng nếu không cần/không có alias) — vd
    /// NotTransferredAway("e") -&gt; "(CASE WHEN (e.StatusTransition IS NULL OR e.StatusTransition &lt;&gt; 0) THEN 1 END) = 1".
    ///
    /// Bọc trong CASE WHEN ... THEN 1 END thay vì viết OR/IS NULL trần — kiểm chứng bằng EXPLAIN PLAN
    /// thật: dù điều kiện này RẤT chọn lọc (chỉ ~4.4% số dòng EQUIPMENTS khớp, tức loại bỏ ~95.6% dòng
    /// "hồn ma"), 1 index B-tree thường trên StatusTransition KHÔNG giúp được vì B-tree không lưu giá trị
    /// NULL — nhánh "IS NULL" vẫn buộc full table scan. Biểu thức CASE này khớp CHÍNH XÁC với index hàm
    /// IX_EQUIPMENTS_NOT_TRANSFERRED (Migration0071) — index hàm chỉ lưu các dòng "sống" (95.6% dòng
    /// "hồn ma" có CASE trả về NULL nên KHÔNG được lưu vào index luôn), giúp Oracle dùng index thay vì
    /// full scan. PHẢI giữ nguyên văn biểu thức này (kể cả khoảng trắng/thứ tự) — Oracle chỉ nhận diện
    /// được function-based index khi WHERE khớp CHÍNH XÁC biểu thức lúc tạo index.</summary>
    public static string NotTransferredAway(string alias = "")
    {
        var prefix = string.IsNullOrEmpty(alias) ? string.Empty : $"{alias}.";
        return $"(CASE WHEN ({prefix}StatusTransition IS NULL OR {prefix}StatusTransition <> 0) THEN 1 END) = 1";
    }
}
