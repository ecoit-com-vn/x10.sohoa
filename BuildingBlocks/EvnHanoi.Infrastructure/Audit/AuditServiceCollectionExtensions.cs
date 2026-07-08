using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace EvnHanoi.Infrastructure.Audit;

public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký publisher audit qua RabbitMQ. Gọi sau khi đã có hoặc sẽ có IConnection.
    /// Thêm <see cref="AuditActionFilter"/> vào AddControllers(options).
    /// </summary>
    public static IServiceCollection AddAuditInfrastructure(
        this IServiceCollection services,
        string serviceName)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton(new AuditServiceMetadata(serviceName));
        services.AddSingleton<IAuditPublisher, RabbitMqAuditPublisher>();
        return services;
    }

    /// <summary>
    /// Đăng ký kết nối RabbitMQ nếu service chưa có (dùng cho audit publisher).
    /// </summary>
    public static async Task<IServiceCollection> AddRabbitMqConnectionIfMissingAsync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services.Any(d => d.ServiceType == typeof(IConnection)))
            return services;

        var rabbitFactory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/",
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            Port = int.TryParse(configuration["RabbitMQ:Port"], out var port) ? port : 5672
        };

        var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
        services.AddSingleton<IConnection>(rabbitConnection);
        return services;
    }
}
