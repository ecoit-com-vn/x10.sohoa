using System.Text.Json.Serialization;

namespace EvnHanoi.EquipmentService.Core.DTOs.DigitalSignature;

// ===== API 1: lay-thong-tin-serial-number =====

public class KySoSerialNumberRequest
{
    [JsonPropertyName("ns_ID")]
    public long NsId { get; set; }
}

public class KySoSerialNumberResponse
{
    [JsonPropertyName("data")]
    public List<KySoSerialNumberData>? Data { get; set; }

    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }
}

public class KySoSerialNumberData
{
    [JsonPropertyName("NS_ID")]
    public long NsId { get; set; }

    [JsonPropertyName("SERIAL")]
    public string? Serial { get; set; }

    [JsonPropertyName("ALIAS")]
    public string? Alias { get; set; }

    [JsonPropertyName("VALIDFR")]
    public DateTime? ValidFr { get; set; }

    [JsonPropertyName("VALIDTO")]
    public DateTime? ValidTo { get; set; }

    [JsonPropertyName("STATUS")]
    public int Status { get; set; }
}

// ===== API 2: lay-anh-chu-ky =====

public class KySoSignatureImageRequest
{
    [JsonPropertyName("ns_id")]
    public long NsId { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; } = 1;
}

public class KySoSignatureImageResponse
{
    [JsonPropertyName("data")]
    public string? Data { get; set; } // base64 PNG

    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }
}

// ===== API 3: sign-pdf-base64-image =====

public class KySoTimestampConfig
{
    [JsonPropertyName("useTimestamp")]
    public bool UseTimestamp { get; set; } = false;
}

public class KySoDisplayImageConfig
{
    [JsonPropertyName("locateSign")]
    public int LocateSign { get; set; } = 5;

    [JsonPropertyName("numberPageSign")]
    public int NumberPageSign { get; set; } = 1;

    [JsonPropertyName("widthRectangle")]
    public int WidthRectangle { get; set; } = 160;

    [JsonPropertyName("heightRectangle")]
    public int HeightRectangle { get; set; } = 100;

    [JsonPropertyName("marginLeftOfRectangle")]
    public int MarginLeftOfRectangle { get; set; } = 362;

    [JsonPropertyName("marginRightOfRectangle")]
    public int MarginRightOfRectangle { get; set; } = 0;

    [JsonPropertyName("marginTopOfRectangle")]
    public int MarginTopOfRectangle { get; set; } = 0;

    [JsonPropertyName("marginBottomOfRectangle")]
    public int MarginBottomOfRectangle { get; set; } = 428;

    [JsonPropertyName("contact")]
    public string Contact { get; set; } = "contact";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "reason";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "location";
}

public class KySoSignPdfRequest
{
    [JsonPropertyName("appCode")]
    public string AppCode { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("caType")]
    public int CaType { get; set; } = 1;

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("digestAlgorithm")]
    public string DigestAlgorithm { get; set; } = "SHA-1";

    [JsonPropertyName("timestampConfig")]
    public KySoTimestampConfig TimestampConfig { get; set; } = new();

    [JsonPropertyName("displayImageConfigBO")]
    public KySoDisplayImageConfig DisplayImageConfigBO { get; set; } = new();

    [JsonPropertyName("fileImageBase64")]
    public string FileImageBase64 { get; set; } = string.Empty;

    [JsonPropertyName("fileBase64")]
    public string FileBase64 { get; set; } = string.Empty;
}

public class KySoSignPdfResponse
{
    [JsonPropertyName("data")]
    public KySoSignPdfResultData? Data { get; set; }

    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }
}

public class KySoSignPdfResultData
{
    /// <summary>Kết quả ký THỰC SỰ — outer status/statusCode chỉ phản ánh gọi HTTP thành công.</summary>
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("objectError")]
    public object? ObjectError { get; set; }

    [JsonPropertyName("signedFileBase64")]
    public string? SignedFileBase64 { get; set; }
}

// ===== Service-level result =====

/// <summary>Kết quả nghiệp vụ của một lần ký số tài liệu — không ném exception cho lỗi nghiệp vụ.</summary>
public class SignDocumentResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? NewVersionId { get; set; }
    public int? NewVersionNumber { get; set; }
    public DateTime? SignedAt { get; set; }

    public static SignDocumentResult Fail(string message) => new() { Success = false, ErrorMessage = message };

    public static SignDocumentResult Ok(Guid newVersionId, int newVersionNumber, DateTime signedAt) => new()
    {
        Success = true,
        NewVersionId = newVersionId,
        NewVersionNumber = newVersionNumber,
        SignedAt = signedAt
    };
}
