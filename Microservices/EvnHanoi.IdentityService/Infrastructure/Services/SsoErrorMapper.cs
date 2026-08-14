using EvnHanoi.IdentityService.Core.DTOs;

namespace EvnHanoi.IdentityService.Infrastructure.Services;

public static class SsoErrorMapper
{
    public static SsoException Map(string? code, string? message = null)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? "AUT-002" : code.Trim().ToUpperInvariant();
        var (status, defaultMessage) = normalizedCode switch
        {
            "AUT-005" => (401, "Ticket SSO đã hết hạn. Vui lòng đăng nhập lại."),
            "AUT-006" => (403, "Tài khoản chưa được cấp quyền truy cập ứng dụng."),
            "AUT-002" => (401, "Ticket SSO không hợp lệ."),
            _ => (401, "Xác thực SSO thất bại.")
        };
        return new SsoException(
            normalizedCode,
            string.IsNullOrWhiteSpace(message) ? defaultMessage : message,
            status);
    }
}
