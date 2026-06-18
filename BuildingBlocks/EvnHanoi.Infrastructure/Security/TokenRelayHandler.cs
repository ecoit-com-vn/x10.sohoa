using Microsoft.AspNetCore.Http;

namespace EvnHanoi.Infrastructure.Security;

/// <summary>
/// DelegatingHandler tự động lấy Bearer token từ HTTP request gốc
/// và đính kèm vào mọi outgoing request của HttpClient đăng ký nó.
/// Đăng ký bằng AddTokenRelayHttpClient() trong DI.
/// </summary>
public class TokenRelayHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TokenRelayHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader["Bearer ".Length..]);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
