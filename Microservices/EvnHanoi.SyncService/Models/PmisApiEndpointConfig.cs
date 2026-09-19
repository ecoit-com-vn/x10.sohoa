using System.Linq;

namespace EvnHanoi.SyncService.Models;

/// <summary>
/// 1 trong 9 API PMIS theo tài liệu "Phương án đồng bộ PMIS" — Url/Headers do admin cấu hình qua UI,
/// không hard-code trong appsettings.
/// </summary>
public class PmisApiEndpointConfig
{
    public string Id { get; set; } = string.Empty;
    public string ApiCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string HttpMethod { get; set; } = "GET";
    public int? TimeoutSeconds { get; set; }
    /// <summary>Số bản ghi lấy mỗi trang (query "take") khi gọi API PMIS này — null = dùng mặc định
    /// <see cref="PmisPaging.DefaultPageSize"/>. Trước đây hard-code rải rác trong code (1000 ở
    /// PmisScheduledSyncJob/PmisSyncExecutionService, 100 trong các DTO tra cứu tương tác) — giờ admin tự
    /// chỉnh theo từng API qua "Cấu hình kết nối API" mà không cần build/deploy lại.</summary>
    public int? PageSize { get; set; }
    public bool IsActive { get; set; }
    public int RowVersion { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class PmisApiEndpointConfigListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ApiCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string HttpMethod { get; set; } = "GET";
    public int? TimeoutSeconds { get; set; }
    public int? PageSize { get; set; }
    public bool IsActive { get; set; }
    public int RowVersion { get; set; }
    public int HeaderCount { get; set; }
}

public class UpdatePmisApiEndpointConfigRequest
{
    public string? Url { get; set; }
    public string HttpMethod { get; set; } = "GET";
    public int? TimeoutSeconds { get; set; }
    public int? PageSize { get; set; }
    public bool IsActive { get; set; }
    public int RowVersion { get; set; }
}

/// <summary>Giá trị mặc định khi admin chưa cấu hình PageSize cho 1 API PMIS cụ thể.</summary>
public static class PmisPaging
{
    public const int DefaultPageSize = 100;
}

/// <summary>Danh sách phương thức HTTP hợp lệ cho 1 API PMIS — validate ở controller trước khi lưu, tránh
/// admin gõ nhầm 1 chuỗi tuỳ ý xuống cột HTTP_METHOD (VARCHAR2(10), không có CHECK constraint ở DB).</summary>
public static class PmisHttpMethods
{
    public static readonly string[] Allowed = ["GET", "POST", "PUT", "DELETE"];
    public static bool IsValid(string? method) => method != null && Allowed.Contains(method, StringComparer.OrdinalIgnoreCase);
}
