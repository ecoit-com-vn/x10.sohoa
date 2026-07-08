using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.SyncService.Schedulers;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using Serilog;
using StackExchange.Redis;
using Quartz;
using Scalar.AspNetCore;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Setup Serilog
builder.Host.UseSerilog(SerilogSetupHelper.ConfigureSerilog);

builder.Services.AddOpenApi();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditActionFilter>();
});

// Run DbUp Migrations
DatabaseMigrationHelper.RunMigrations(builder.Configuration, "SyncService");

// 1. Configure Redis and RedLock
var redisEndpoints = new List<RedLockMultiplexer>
{
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379")
};
var redlockFactory = RedLockFactory.Create(redisEndpoints);
builder.Services.AddSingleton<RedLockNet.IDistributedLockFactory>(redlockFactory);

// 2. Polly Policies
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .Or<TimeoutRejectedException>()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10));

var bulkheadPolicy = Policy.BulkheadAsync<HttpResponseMessage>(10, 20); // Concurrency Limiter for CA

// 3. PMIS HttpClient
builder.Services.AddHttpClient("PMIS", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Endpoints:PMIS"] ?? "https://api.pmis.mock/");
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy)
.AddPolicyHandler(timeoutPolicy);

// 4. CA HttpClient
builder.Services.AddHttpClient("CA", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Endpoints:CA"] ?? "https://api.ca.mock/");
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy)
.AddPolicyHandler(bulkheadPolicy);

// 5. Quartz Scheduler
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("PmisSyncJob");
    q.AddJob<PmisSyncScheduler>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("PmisSyncJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever()) // run every 5 mins
    );
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// 6. RabbitMQ Connection & Workers
var rabbitFactory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
    Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var port) ? port : 5672
};
var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
builder.Services.AddSingleton<IConnection>(rabbitConnection);
builder.Services.AddAuditInfrastructure("SyncService");

builder.Services.AddSingleton<EvnHanoi.SyncService.Services.IPmisSyncTriggerService, EvnHanoi.SyncService.Services.PmisSyncTriggerService>();
builder.Services.AddHostedService<EvnHanoi.SyncService.Workers.EquipmentSyncWorker>();
builder.Services.AddHostedService<EvnHanoi.SyncService.Workers.PmisSyncWorker>();
builder.Services.AddHostedService<EvnHanoi.SyncService.Workers.PmisPublisherWorker>();
builder.Services.AddPermissionDiscovery("SyncService");

var app = builder.Build();

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
app.MapControllers();

app.Lifetime.ApplicationStopping.Register(() => {
    redlockFactory.Dispose();
});

app.Run();
