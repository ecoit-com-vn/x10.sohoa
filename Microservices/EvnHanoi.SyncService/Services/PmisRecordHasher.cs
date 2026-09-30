using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EvnHanoi.SyncService.Services;

/// <summary>Băm nội dung + đọc khoá của bản ghi PMIS thô (JsonElement do PmisScheduledSyncJob tạo bằng
/// JsonSerializer.SerializeToElement(dto) — thứ tự thuộc tính theo DTO nên chuỗi JSON ổn định giữa các lượt).</summary>
internal static class PmisRecordHasher
{
    public static string Compute(JsonElement element) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(element.GetRawText())));

    /// <summary>Mã PMIS của Trạm (MaTBA) hoặc Đường dây (MaDuongDay), đã Trim; rỗng nếu thiếu.</summary>
    public static string InfrastructureCode(JsonElement raw, int infraTypeId) =>
        ReadString(raw, infraTypeId == 1 ? "MaTBA" : "MaDuongDay");

    /// <summary>Mã thiết bị: MaThietBi (thiết bị TBA) nếu có giá trị, ngược lại MaTB (thiết bị đường dây) — cùng
    /// quy ước với PmisSyncExecutionService.SyncEquipmentAsync; đã Trim; rỗng nếu thiếu.</summary>
    public static string EquipmentCode(JsonElement raw)
    {
        var maThietBi = ReadString(raw, "MaThietBi");
        return maThietBi.Length > 0 ? maThietBi : ReadString(raw, "MaTB");
    }

    private static string ReadString(JsonElement raw, string name)
    {
        if (raw.ValueKind != JsonValueKind.Object) return string.Empty;
        foreach (var prop in raw.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                return prop.Value.GetString()?.Trim() ?? string.Empty;
        }
        return string.Empty;
    }
}
