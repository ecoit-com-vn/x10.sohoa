using System.Text.Json.Serialization;
using System.Text.Json;
using System.Globalization;

namespace EvnHanoi.IdentityService.Core.DTOs;

public sealed class SsoValidationResponse
{
    public string? Code { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
    public SsoValidationData? Data { get; set; }
}

public sealed class SsoValidationData
{
    public string? ServiceTicket { get; set; }
    public DateTimeOffset? ExpiresIn { get; set; }
    public SsoIdentity? Identity { get; set; }
}

public sealed class SsoIdentity
{
    public string? Username { get; set; }
    public string? UsernameLocal { get; set; }
    public string? FullName { get; set; }
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? UserId { get; set; }
    public string? AppCode { get; set; }
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? AppId { get; set; }
    public string? Email { get; set; }
    [JsonPropertyName("ns_id")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? NsId { get; set; }
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? DeptId { get; set; }
    public string? StaffCode { get; set; }
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? PositionId { get; set; }
    public string? PositionName { get; set; }
    public string? Phone { get; set; }
    public bool? Authentication2Factor { get; set; }
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? OrgId { get; set; }
}

public sealed class SsoException : Exception
{
    public SsoException(string code, string message, int statusCode = 401, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}

public sealed class FlexibleStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to string.")
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
