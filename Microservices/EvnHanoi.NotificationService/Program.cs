using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.NotificationService.Hubs;
using EvnHanoi.NotificationService.Schedulers;
using EvnHanoi.NotificationService.Services;
using EvnHanoi.NotificationService.Workers;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Serilog;
using RabbitMQ.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net.Http;
using Scalar.AspNetCore;
using Quartz;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var internalToken = builder.Configuration["Internal:Token"];
if (string.IsNullOrWhiteSpace(internalToken))
{
    Log.Warning("Internal:Token chưa cấu hình — POST internal/v1/dossiers/{{id}}/reindex sẽ trả 503.");
}

// Setup Serilog
builder.Host.UseSerilog(SerilogSetupHelper.ConfigureSerilog);

// Setup DbUp
EvnHanoi.Infrastructure.Database.DatabaseMigrationHelper.RunMigrations(builder.Configuration, "NotificationService");

var rabbitFactory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
    Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var port) ? port : 5672
};

var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
builder.Services.AddSingleton<IConnection>(rabbitConnection);
builder.Services.AddAuditInfrastructure("NotificationService");

builder.Services.AddControllers(options =>
{
    options.Filters.Add<DynamicPermissionFilter>();
    options.Filters.Add<AuditActionFilter>();
});
builder.Services.AddStructuredValidationErrors();
builder.Services.AddDapperInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

// SignalR with Redis backplane (bắt buộc khi chạy nhiều replica NotificationService)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnectionString) && !redisConnectionString.Contains(':'))
    redisConnectionString = $"{redisConnectionString.Trim()}:6379";

var redLockEndpoints = new List<RedLockMultiplexer>
{
    ConnectionMultiplexer.Connect(redisConnectionString ?? "localhost:6379")
};
var redLockFactory = RedLockFactory.Create(redLockEndpoints);
builder.Services.AddSingleton<IDistributedLockFactory>(redLockFactory);

var signalRBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrEmpty(redisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(redisConnectionString);
    Log.Information("SignalR configured with Redis backplane: {Redis}", redisConnectionString);
}
else
{
    Log.Warning("Redis connection string is not configured. SignalR runs in-memory (single instance only).");
}

builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddMemoryCache();
builder.Services.AddPermissionDiscovery("NotificationService");
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IAuditLogExportService, EvnHanoi.NotificationService.Services.AuditLogExportService>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Repositories.IAuditLogRepository, EvnHanoi.NotificationService.Repositories.AuditLogRepository>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IAuditLogService, EvnHanoi.NotificationService.Services.AuditLogService>();
builder.Services.AddScoped<IAuditLogRetentionSettingsClient, AuditLogRetentionSettingsClient>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Repositories.IDossierEnrichmentRepository, EvnHanoi.NotificationService.Repositories.DossierEnrichmentRepository>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Repositories.ILookupTrackingRepository, EvnHanoi.NotificationService.Repositories.LookupTrackingRepository>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IDossierDocumentBuilder, EvnHanoi.NotificationService.Services.DossierDocumentBuilder>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IDossierIndexer, EvnHanoi.NotificationService.Services.DossierIndexer>();

builder.Services.AddScoped<EvnHanoi.NotificationService.Repositories.IDossierSearchRepository, EvnHanoi.NotificationService.Repositories.DossierSearchRepository>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IDossierSearchService, EvnHanoi.NotificationService.Services.DossierSearchService>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IDossierMenuScopeValidator, EvnHanoi.NotificationService.Services.DossierMenuScopeValidator>();

builder.Services.AddScoped<EvnHanoi.NotificationService.Repositories.IDocumentEnrichmentRepository, EvnHanoi.NotificationService.Repositories.DocumentEnrichmentRepository>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Repositories.IDocumentSearchRepository, EvnHanoi.NotificationService.Repositories.DocumentSearchRepository>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IDocumentIndexer, EvnHanoi.NotificationService.Services.DocumentIndexer>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IDocumentSearchService, EvnHanoi.NotificationService.Services.DocumentSearchService>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IMinioOcrTextReader, EvnHanoi.NotificationService.Services.MinioOcrTextReader>();

// Elasticsearch setup — hỗ trợ cả key Url (Development) và Uri (appsettings gốc)
var esUri = builder.Configuration["Elasticsearch:Url"]
            ?? builder.Configuration["Elasticsearch:Uri"]
            ?? "http://localhost:9200";
Log.Information("NotificationService Elasticsearch: {ElasticsearchUrl}", esUri);
var esSettings = new ElasticsearchClientSettings(new Uri(esUri))
    .DefaultIndex("equipments");
builder.Services.AddSingleton(new ElasticsearchClient(esSettings));

builder.Services.AddHostedService<ElasticsearchSetupService>();

// Worker
// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_12345678901234567890";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Map sub/nameid → ClaimTypes để FindFirst(NameIdentifier) ổn định trên mọi phiên bản .NET.
        options.MapInboundClaims = true;
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

// Configure HttpClient for IdentityService communication
builder.Services.AddHttpClient("IdentityService", client =>
{
    var identityUrl = builder.Configuration["Services:IdentityService"] ?? "http://identityservice";
    client.BaseAddress = new Uri(identityUrl);
    client.DefaultRequestHeaders.Add("X-Internal-Token", builder.Configuration["Internal:Token"] ?? "");
});

builder.Services.AddScoped<EvnHanoi.NotificationService.Repositories.INotificationRepository, EvnHanoi.NotificationService.Repositories.NotificationRepository>();
builder.Services.AddSingleton<EvnHanoi.NotificationService.Services.IIdentityServiceClient, EvnHanoi.NotificationService.Services.IdentityServiceClient>();

var auditLogRetentionJobKey = new JobKey("AuditLogRetentionJob");
builder.Services.AddQuartz(q =>
{
    q.AddJob<AuditLogRetentionJob>(opts => opts.WithIdentity(auditLogRetentionJobKey));
    q.AddTrigger(opts => opts
        .ForJob(auditLogRetentionJobKey)
        .WithIdentity("AuditLogRetentionJob-trigger")
        .WithCronSchedule("0 0 1 * * ?", x => x.InTimeZone(TimeZoneInfo.Utc)));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

builder.Services.AddHostedService<EquipmentIndexWorker>();
builder.Services.AddHostedService<DossierIndexWorker>();
builder.Services.AddHostedService<DocumentIndexWorker>();
builder.Services.AddHostedService<AuditEventWorker>();
builder.Services.AddHostedService<NotificationEventsConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Lifetime.ApplicationStopping.Register(redLockFactory.Dispose);

app.Run();
