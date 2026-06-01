using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.NotificationService.Hubs;
using EvnHanoi.NotificationService.Services;
using EvnHanoi.NotificationService.Workers;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Setup Serilog
builder.Host.UseSerilog(SerilogSetupHelper.ConfigureSerilog);

// Setup DbUp
EvnHanoi.Infrastructure.Database.DatabaseMigrationHelper.RunMigrations(builder.Configuration);

builder.Services.AddControllers();

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

// Elasticsearch setup
var esUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
var esSettings = new ElasticsearchClientSettings(new Uri(esUri))
    .DefaultIndex("equipments");
builder.Services.AddSingleton(new ElasticsearchClient(esSettings));

builder.Services.AddHostedService<ElasticsearchSetupService>();

// Worker
builder.Services.AddHostedService<EquipmentIndexWorker>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseRouting();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
