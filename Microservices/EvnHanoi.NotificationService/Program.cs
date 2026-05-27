using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.NotificationService.Hubs;
using EvnHanoi.NotificationService.Services;
using EvnHanoi.NotificationService.Workers;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Setup Serilog
builder.Host.UseSerilog(SerilogSetupHelper.ConfigureSerilog);

// Setup DbUp
EvnHanoi.Infrastructure.Database.DatabaseMigrationHelper.RunMigrations(builder.Configuration);

builder.Services.AddControllers();

// SignalR with Redis
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

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

app.UseRouting();

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");

app.Run();
