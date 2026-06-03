using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.Infrastructure.Security;

public static class PermissionDiscoveryExtensions
{
    /// <summary>
    /// Đăng ký Background Service tự động phát hiện và đồng bộ phân quyền qua RabbitMQ.
    /// </summary>
    public static IServiceCollection AddPermissionDiscovery(this IServiceCollection services, string serviceName)
    {
        services.AddHostedService(sp => 
            new PermissionDiscoveryService(
                sp.GetRequiredService<IApiDescriptionGroupCollectionProvider>(),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp,
                sp.GetRequiredService<ILogger<PermissionDiscoveryService>>(),
                serviceName
            ));
        return services;
    }
}
