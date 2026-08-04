using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;
using Serilog;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using RabbitMQ.Client;
using Nest;
using Minio;
using EvnHanoi.EquipmentService.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 1. Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    SerilogSetupHelper.ConfigureSerilog(context, configuration);
});

// 2. Add services to the container
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<EvnHanoi.Infrastructure.Security.DynamicPermissionFilter>();
    options.Filters.Add<AuditActionFilter>();
});
builder.Services.AddStructuredValidationErrors();
builder.Services.AddOpenApi();

var rabbitFactory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
    VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
    Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var port) ? port : 5672
};
var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
builder.Services.AddSingleton<IConnection>(rabbitConnection);
builder.Services.AddAuditInfrastructure("EquipmentService");
builder.Services.AddHostedService<DigitizationMessagingTopologyInitializer>();

builder.Services.AddDapperInfrastructure(builder.Configuration);

// Map string to DbType.AnsiString globally in Dapper to prevent Oracle Implicit Type Conversion (VARCHAR2 vs NVARCHAR2) causing index suppression
Dapper.SqlMapper.AddTypeMap(typeof(string), System.Data.DbType.AnsiString);

var elasticsearchUrl = builder.Configuration["Elasticsearch:Url"]
    ?? builder.Configuration["Elasticsearch:Uri"]
    ?? "http://localhost:9200";
builder.Services.AddSingleton<IElasticClient>(_ =>
    new ElasticClient(new ConnectionSettings(new Uri(elasticsearchUrl)).DefaultDisableIdInference()));

builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDossierRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.DossierRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDossierSearchRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.DossierSearchRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDossierSetRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.DossierSetRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDossierService, EvnHanoi.EquipmentService.Core.Services.DossierService>();
builder.Services.AddScoped<DossierKindGuard>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDocumentRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.DocumentRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IFolderAllocationRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.FolderAllocationRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Services.IDocumentManagementService, EvnHanoi.EquipmentService.Core.Services.DocumentManagementService>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Services.IFolderAllocationService, EvnHanoi.EquipmentService.Core.Services.FolderAllocationService>();
builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["MinIO:Endpoint"] ?? "localhost:9000";
    var accessKey = config["MinIO:AccessKey"] ?? "minioadmin";
    var secretKey = config["MinIO:SecretKey"] ?? "minioadmin";
    var useSslConfig = config["MinIO:UseSSL"];
    var useSsl = !string.IsNullOrEmpty(useSslConfig) && bool.Parse(useSslConfig);

    return new MinioClient()
        .WithEndpoint(endpoint)
        .WithCredentials(accessKey, secretKey)
        .WithSSL(useSsl)
        .Build();
});
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IFileDownloadTokenService, FileDownloadTokenService>();
builder.Services.AddScoped<IDossierDocumentService, DossierDocumentService>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDocumentDigitizationRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.DocumentDigitizationRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDocumentDigitizationService, DocumentDigitizationService>();
builder.Services.AddHostedService<EvnHanoi.EquipmentService.Infrastructure.Messaging.DocumentDigitizationConsumer>();
builder.Services.AddHostedService<EvnHanoi.EquipmentService.Infrastructure.Messaging.OcrJobWatchdogService>();
builder.Services.AddScoped<IClamAvService, ClamAvService>();
builder.Services.AddScoped<IMimeTypeValidationService, MimeTypeValidationService>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IEquipmentRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.EquipmentRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IExternalApiKeyValidator, EvnHanoi.EquipmentService.Infrastructure.Repositories.ExternalApiKeyValidator>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IEquipmentTypeRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.EquipmentTypeRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IPhysicalStorageRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.PhysicalStorageRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.ICatalogRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.CatalogRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IEavFormTemplateRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.EavFormTemplateRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDossierTypeRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.DossierTypeRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDocumentTypeRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.DocumentTypeRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IInfrastructureRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.InfrastructureRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IEavFormTemplateService, EvnHanoi.EquipmentService.Core.Services.EavFormTemplateService>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDossierTypeService, EvnHanoi.EquipmentService.Core.Services.DossierTypeService>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IElasticsearchService, EvnHanoi.EquipmentService.Infrastructure.Services.ElasticsearchService>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDocumentTextIndexNotifier, EvnHanoi.EquipmentService.Infrastructure.Messaging.DocumentTextIndexNotifier>();
builder.Services.AddSingleton<EvnHanoi.EquipmentService.Core.Interfaces.IMessageProducer, EvnHanoi.EquipmentService.Infrastructure.Messaging.RabbitMQProducer>();
builder.Services.AddPermissionDiscovery("EquipmentService");
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<EvnHanoi.Infrastructure.Security.TokenRelayHandler>();

builder.Services.AddHttpClient("IdentityService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:IdentityService"] ?? "http://identityservice");
});

builder.Services.AddHttpClient("WorkflowService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:WorkflowService"] ?? "http://workflowservice");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<EvnHanoi.Infrastructure.Security.TokenRelayHandler>();

builder.Services.AddHttpClient("NotificationService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationService"] ?? "http://notificationservice");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<EvnHanoi.Infrastructure.Security.TokenRelayHandler>();

builder.Services.AddScoped<IDocumentFulltextSearchNotificationClient, DocumentFulltextSearchNotificationClient>();

builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDigitizationProgressNotifier,
    EvnHanoi.EquipmentService.Infrastructure.Notifications.HttpDigitizationProgressNotifier>();

// 3. Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_12345678901234567890";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// 4. Run DbUp Migrations
try
{
    DatabaseMigrationHelper.RunMigrations(app.Configuration, "EquipmentService", runSeeds: app.Environment.IsDevelopment());
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to run database migrations.");
}

try
{
    using var scope = app.Services.CreateScope();
    var elasticService = scope.ServiceProvider.GetRequiredService<EvnHanoi.EquipmentService.Core.Interfaces.IElasticsearchService>();
    await elasticService.CreateIndexAsync();
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to initialize Elasticsearch indices.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

// Chỉ bật chuyển hướng HTTPS khi KHÔNG chạy trong môi trường Aspire 
// Hoặc chỉ bật khi đã lên Production thực tế.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

