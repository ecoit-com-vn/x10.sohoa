using EvnHanoi.AppHost;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// AppHost là generic host (không phải WebApplicationBuilder) nên chỉ nhận biết DOTNET_ENVIRONMENT,
// KHÔNG nhận ASPNETCORE_ENVIRONMENT — nếu chỉ set ASPNETCORE_ENVIRONMENT=Development (như launchSettings.json
// trước đây, hoặc khi chạy thẳng file build thay vì `dotnet run`), AppHost vẫn coi mình đang ở Production và
// KHÔNG nạp appsettings.Development.json, khiến mọi config["..."] đọc từ đây trả về null (rồi WithEnvironment
// truyền null xuống service con thành biến môi trường RỖNG, không phải biến bị thiếu — ví dụ new Uri("") crash).
// AppHost chỉ dùng để orchestrate dev cục bộ (production deploy qua K8s ConfigMap trực tiếp), nên nạp thẳng
// appsettings.Development.json ở đây luôn an toàn, không phụ thuộc cách khởi động.
builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
var config = builder.Configuration;

var identityService = builder.AddProject<Projects.EvnHanoi_IdentityService>("identityservice")
    .WithSharedInfrastructure(config)
    .WithMinio(config);

var equipmentService = builder.AddProject<Projects.EvnHanoi_EquipmentService>("equipmentservice")
    .WithSharedInfrastructure(config)
    .WithMinio(config)
    .WithServiceUrls(config, "IdentityService", "WorkflowService", "NotificationService");

var digitizationService = builder.AddProject<Projects.EvnHanoi_DigitizationService>("digitizationservice")
    .WithSharedInfrastructure(config)
    .WithMinio(config)
    .WithEnvironment("AIModelServers__OcrVlServerUrl", config["AIModelServers:OcrVlServerUrl"])
    .WithEnvironment("AIModelServers__LlmServerUrl", config["AIModelServers:LlmServerUrl"]);

var notificationService = builder.AddProject<Projects.EvnHanoi_NotificationService>("notificationservice")
    .WithSharedInfrastructure(config)
    .WithMinio(config)
    .WithServiceUrls(config, "IdentityService");

var syncService = builder.AddProject<Projects.EvnHanoi_SyncService>("syncservice")
    .WithSharedInfrastructure(config)
    .WithEnvironment("Pmis__ApiUrl", config["Pmis:ApiUrl"])
    .WithEnvironment("Pmis__SyncIntervalMinutes", config["Pmis:SyncIntervalMinutes"]);

var workflowService = builder.AddProject<Projects.EvnHanoi_WorkflowService>("workflowservice")
    .WithSharedInfrastructure(config)
    .WithServiceUrls(config, "IdentityService", "EquipmentService");

var reportService = builder.AddProject<Projects.EvnHanoi_ReportService>("reportservice")
    .WithSharedInfrastructure(config)
    .WithMinio(config)
    .WithServiceUrls(config, "IdentityService");

builder.AddProject<Projects.EvnHanoi_ApiGateway>("apigateway")
    .WithHttpEndpoint(port: 5000, name: "gateway-http")
    .WithReference(identityService)
    .WithReference(equipmentService)
    .WithReference(digitizationService)
    .WithReference(notificationService)
    .WithReference(syncService)
    .WithReference(workflowService)
    .WithReference(reportService);

builder.Build().Run();
