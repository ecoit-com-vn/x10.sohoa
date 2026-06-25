using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EvnHanoi.NotificationService.Services;

/// <summary>
/// Đọc userId/roles từ JWT — tương thích MapInboundClaims bật/tắt (.NET 8+).
/// </summary>
public static class JwtUserClaimResolver
{
    public static string? ResolveUserId(ClaimsPrincipal user)
    {
        foreach (var claim in EnumerateUserIdClaims(user))
        {
            if (Guid.TryParse(claim, out _))
                return claim.Trim();
        }

        // JWT .NET 8+ có thể giữ claim type ngắn (không map về ClaimTypes) — quét mọi claim GUID.
        foreach (var claim in user.Claims)
        {
            if (IsNonUserIdClaimType(claim.Type))
                continue;

            if (Guid.TryParse(claim.Value, out _))
                return claim.Value.Trim();
        }

        return null;
    }

    private static bool IsNonUserIdClaimType(string claimType) =>
        claimType.Equals(ClaimTypes.Name, StringComparison.OrdinalIgnoreCase)
        || claimType.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("role", StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("preferred_username", StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("unique_name", StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("unit_id", StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("TokenType", StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("exp", StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("iat", StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("nbf", StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("aud", StringComparison.OrdinalIgnoreCase)
        || claimType.Equals("iss", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ResolveRoles(ClaimsPrincipal user)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in user.FindAll(ClaimTypes.Role))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
                roles.Add(claim.Value.Trim());
        }

        foreach (var claim in user.FindAll("role"))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
                roles.Add(claim.Value.Trim());
        }

        foreach (var claim in user.FindAll("http://schemas.microsoft.com/ws/2008/06/identity/claims/role"))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
                roles.Add(claim.Value.Trim());
        }

        return roles.ToList();
    }

    private static IEnumerable<string> EnumerateUserIdClaims(ClaimsPrincipal user)
    {
        string?[] candidates =
        [
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            user.FindFirst("sub")?.Value,
            user.FindFirst("nameid")?.Value,
            user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
        ];

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                yield return candidate.Trim();
        }
    }
}
