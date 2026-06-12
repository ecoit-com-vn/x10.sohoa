using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.NotificationService.Hubs;
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

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

builder.Services.AddControllers();
builder.Services.AddStructuredValidationErrors();
builder.Services.AddDapperInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

// SignalR with Redis (Optional in Development)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
var signalRBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrEmpty(redisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(redisConnectionString);
    Log.Information("SignalR is configured with Redis backplane.");
}
else
{
    Log.Warning("Redis connection string is not configured. SignalR is running in local single-server mode (In-Memory).");
}

builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddPermissionDiscovery("NotificationService");
builder.Services.AddScoped<EvnHanoi.NotificationService.Repositories.IAuditLogRepository, EvnHanoi.NotificationService.Repositories.AuditLogRepository>();
builder.Services.AddScoped<EvnHanoi.NotificationService.Services.IAuditLogService, EvnHanoi.NotificationService.Services.AuditLogService>();

// Elasticsearch setup
var esUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
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
});

builder.Services.AddHostedService<EquipmentIndexWorker>();

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

app.Run();
