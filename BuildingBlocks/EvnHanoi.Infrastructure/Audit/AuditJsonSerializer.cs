using System.Text.Encodings.Web;
using System.Text.Json;

namespace EvnHanoi.Infrastructure.Audit;

/// <summary>
/// JSON options dùng chung cho audit queue — giữ nguyên ký tự tiếng Việt (không escape \uXXXX).
/// </summary>
public static class AuditJsonSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
