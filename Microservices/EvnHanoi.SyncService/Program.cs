using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Schedulers;
using EvnHanoi.SyncService.Security;
using EvnHanoi.SyncService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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

builder.Services.AddMemoryCache();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<DynamicPermissionFilter>();
    options.Filters.Add<AuditActionFilter>();
});
builder.Services.AddStructuredValidationErrors();

builder.Services.AddDapperInfrastructure(builder.Configuration);
builder.Services.AddScoped<IPmisEndpointConfigRepository, PmisEndpointConfigRepository>();
builder.Services.AddSingleton<IPmisHeaderValueProtector, PmisHeaderValueProtector>();
// Scoped (không phải Singleton): phụ thuộc IPmisEndpointConfigRepository (Scoped, dùng IDbConnection
// Scoped) — IMemoryCache bên trong vẫn là Singleton nên cache 5' vẫn dùng chung xuyên suốt request.
builder.Services.AddScoped<IPmisEndpointConfigProvider, PmisEndpointConfigProvider>();
builder.Services.AddScoped<ISyncConfigRepository, SyncConfigRepository>();
builder.Services.AddScoped<ISyncHistoryRepository, SyncHistoryRepository>();
builder.Services.AddScoped<IPmisClient, PmisClient>();
builder.Services.AddScoped<IEquipmentServiceClient, EquipmentServiceClient>();
builder.Services.AddScoped<IPmisSyncExecutionService, PmisSyncExecutionService>();

builder.Services.AddHttpClient("EquipmentServiceInternal", client =>
{
    var baseUrl = builder.Configuration["Services:EquipmentService"] ?? "http://localhost:5254";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
});

// Configure JWT Authentication (đồng bộ với các microservice khác — Gateway forward token,
// từng service tự validate)
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

// 5. Quartz Scheduler — PmisScheduledSyncJob thay PmisSyncScheduler cũ (chỉ log, chưa lưu gì).
// Tick mỗi phút, tự kiểm tra SYNC_CONFIG của từng đối tượng để biết có tới hạn hay không — giữ
// nguyên JobKey "PmisSyncJob" để endpoint POST /api/v1/sync/trigger-now (SyncController) không
// cần đổi.
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("PmisSyncJob");
    q.AddJob<PmisScheduledSyncJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("PmisSyncJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInMinutes(1).RepeatForever())
    );
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// 6. RabbitMQ Connection & Workers
// VirtualHost phải đọc từ config như EquipmentService/WorkflowService/DigitizationService/ReportService
// và AuditServiceCollectionExtensions — thiếu dòng này khiến SyncService luôn nối vhost "/" bất kể
// RabbitMQ:VirtualHost cấu hình gì, gây lỗi khi trỏ vào RabbitMQ có vhost khác "/" (ví dụ RabbitMQ local
// docker-compose dùng vhost riêng).
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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Lifetime.ApplicationStopping.Register(() => {
    redlockFactory.Dispose();
});

app.Run();
