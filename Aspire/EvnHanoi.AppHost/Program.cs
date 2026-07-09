using EvnHanoi.AppHost;

var builder = DistributedApplication.CreateBuilder(args);
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
