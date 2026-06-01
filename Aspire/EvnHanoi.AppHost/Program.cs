var builder = DistributedApplication.CreateBuilder(args);

// Add Microservices
var identityService = builder.AddProject<Projects.EvnHanoi_IdentityService>("identityservice");

var equipmentService = builder.AddProject<Projects.EvnHanoi_EquipmentService>("equipmentservice");

var digitizationService = builder.AddProject<Projects.EvnHanoi_DigitizationService>("digitizationservice");

var notificationService = builder.AddProject<Projects.EvnHanoi_NotificationService>("notificationservice");

var syncService = builder.AddProject<Projects.EvnHanoi_SyncService>("syncservice");

var workflowService = builder.AddProject<Projects.EvnHanoi_WorkflowService>("workflowservice");

var reportService = builder.AddProject<Projects.EvnHanoi_ReportService>("reportservice");

// Add ApiGateway (which proxies to the other services)
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
