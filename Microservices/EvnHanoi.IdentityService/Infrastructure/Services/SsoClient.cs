using System.Text.Json;
using EvnHanoi.IdentityService.Core.DTOs;
using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.IdentityService.Core.Options;
using Microsoft.Extensions.Options;

namespace EvnHanoi.IdentityService.Infrastructure.Services;

public sealed class SsoClient : ISsoClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SsoOptions _options;

    public SsoClient(HttpClient httpClient, IOptions<SsoOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<SsoValidationData> ValidateTicketAsync(
        string ticket,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new SsoException("SSO-DISABLED", "Đăng nhập SSO chưa được bật trên hệ thống.", 503);
        }
        if (string.IsNullOrWhiteSpace(_options.AppCode))
        {
            throw new SsoException("SSO-CONFIG", "Thiếu cấu hình Sso:AppCode.", 500);
        }
        //if (_options.AllowMockTicket && ticket == "mock-sso-ticket-123456")
        //{
        //    return CreateMockValidationData(ticket);
        //}

        var url = AppendQuery(_options.ServiceValidateUrl,
            ("ticket", ticket.Trim()),
            ("appCode", _options.AppCode));
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SsoException(
                "SSO-CONNECTION",
                "Không thể kết nối đến máy chủ SSO EVNHANOI để xác thực ticket.",
                502);
        }

        SsoValidationResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<SsoValidationResponse>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new SsoException("SSO-RESPONSE", "Phản hồi xác thực SSO không đúng định dạng.", 502, ex);
        }

        var code = result?.Code?.Trim() ?? "AUT-002";
        if (!string.Equals(result?.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(code, "API-000", StringComparison.OrdinalIgnoreCase)
            || result?.Data?.Identity == null)
        {
            throw SsoErrorMapper.Map(code, result?.Message);
        }
        return result.Data;
    }

    private static string AppendQuery(string url, params (string Key, string Value)[] values)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return url + separator + string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static SsoValidationData CreateMockValidationData(string ticket) => new()
    {
        ServiceTicket = ticket,
        Identity = new SsoIdentity
        {
            Username = "admin",
            UsernameLocal = "admin",
            FullName = "Quản trị viên Hệ thống",
            Email = "admin@evnhanoi.vn",
            DeptId = "281",
            UserId = "mock-admin"
        }
    };
}
