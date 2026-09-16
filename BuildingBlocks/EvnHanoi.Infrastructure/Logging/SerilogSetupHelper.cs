using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System;

namespace EvnHanoi.Infrastructure.Logging;

public static class SerilogSetupHelper
{
    public static void ConfigureSerilog(HostBuilderContext context, LoggerConfiguration configuration)
    {
        var applicationName = context.HostingEnvironment.ApplicationName ?? "EvnHanoi.Backend";

        configuration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: "Logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                retainedFileCountLimit: 30,
                // Chặn 1 file phình vô hạn trong 1 ngày (vd lỗi lặp liên tục hàng giờ như sự cố PMIS đã gặp)
                // — ghi đè writable layer của container, tính vào ephemeral-storage của node K8s, từng
                // gây tràn đĩa node làm kubelet đuổi hàng loạt pod. 50MB/file x tối đa 30 file giữ lại
                // (retainedFileCountLimit ở trên) = tối đa ~1.5GB/service thay vì không giới hạn.
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .WriteTo.Elasticsearch(new Serilog.Sinks.Elasticsearch.ElasticsearchSinkOptions(new Uri(context.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200"))
            {
                AutoRegisterTemplate = true,
                IndexFormat = "app_logs-{0:yyyy.MM.dd}",
                AutoRegisterTemplateVersion = Serilog.Sinks.Elasticsearch.AutoRegisterTemplateVersion.ESv8
            });

        // Read overrides or additional settings from appsettings.json if needed
        configuration.ReadFrom.Configuration(context.Configuration);
    }
}
