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

// Fail-fast: Internal:Token là shared-secret bắt buộc cho mọi endpoint "/internal/v1/..."
// (vd. InternalSyncTriggerController.TriggerNow). Thiếu key này trước đây KHÔNG bị phát hiện lúc khởi
// động — service vẫn "chạy được" bình thường nhưng âm thầm trả 503 cho MỌI request nội bộ, khiến
// EquipmentService tưởng nhầm là do đang có tiến trình đồng bộ khác chạy (xem báo cáo phân tích log
// sự cố 503 dây chuyền). Kiểm tra ngay tại đây để pod crash rõ ràng (CrashLoopBackOff) thay vì lỗi mù mờ.
// Dùng Console.Error trực tiếp thay vì Log.Fatal: Serilog (builder.Host.UseSerilog) chưa thực sự ghi ra
// sink nào cho tới khi builder.Build() chạy xong — gọi Log.* trước đó bị âm thầm nuốt mất (đã kiểm
// chứng: DatabaseMigrationHelper cũng gặp y hệt, các dòng "Starting Database Migration..." của nó không
// hề xuất hiện trong log SyncService/... thật, dù luôn được gọi trước Build()).
if (string.IsNullOrEmpty(builder.Configuration["Internal:Token"]))
{
    Console.Error.WriteLine("[FATAL] Internal:Token chưa được cấu hình — mọi endpoint internal/v1/... sẽ luôn trả 503. Dừng khởi động SyncService.");
    throw new InvalidOperationException("Missing required configuration: Internal:Token");
}

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
builder.Services.AddScoped<IPmisApiCallLogRepository, PmisApiCallLogRepository>();
builder.Services.AddScoped<IPmisClient, PmisClient>();
builder.Services.AddScoped<IInteractivePmisClient, InteractivePmisClient>();
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

// Circuit breaker cho PMIS ("PMIS"/"PMIS-Interactive") KHÔNG gắn ở mức HttpClientFactory nữa. PMIS có
// ~10 API code khác nhau (SUBSTATION_LIST, LINE_LIST, DEVICE_QR_IMAGE...) dùng CHUNG 1 HttpClient — nếu
// gắn 1 policy instance dùng chung cho cả named client, 5 lỗi liên tiếp của RIÊNG 1 API code (vd.
// DEVICE_QR_IMAGE lỗi cấu hình) sẽ "mở mạch" luôn cho TẤT CẢ API code khác, kể cả những API vẫn gọi
// PMIS bình thường (vd. SUBSTATION_LIST) — đã gặp thực tế trên production. Circuit breaker giờ được
// tạo/áp dụng RIÊNG cho từng cặp (HttpClient name, apiCode) ngay trong
// PmisClient.SendCoreAsync/DownloadDocumentFileAsync (xem PmisClient.GetCircuitBreaker) — vẫn giữ
// nguyên việc tách riêng nền ("PMIS") và tương tác ("PMIS-Interactive"), cộng thêm tách riêng theo
// từng API code trong cùng 1 HttpClient.
var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(60));

// Circuit breaker RIÊNG cho CA (Certificate Authority) — trước đây dùng chung 1 instance với PMIS,
// khiến lỗi gọi CA có thể mở luôn circuit của PMIS (và ngược lại) dù 2 dịch vụ hoàn toàn không liên quan.
var caCircuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

var bulkheadPolicy = Policy.BulkheadAsync<HttpResponseMessage>(10, 20); // Concurrency Limiter for CA

// 3. PMIS HttpClient — KHÔNG gắn retryPolicy: PMIS lỗi thì đánh dấu thất bại ngay lập tức, không tự
// thử lại nhiều lần trong 1 lượt gọi — đồng bộ tự động chỉ cần đúng theo tần suất đã cấu hình
// (PmisScheduledSyncJob), không cần dồn thêm các lượt retry nội bộ của HttpClient.
builder.Services.AddHttpClient("PMIS", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Endpoints:PMIS"] ?? "https://api.pmis.mock/");
})
.AddPolicyHandler(timeoutPolicy);

// 3b. PMIS HttpClient — bản dành cho API tra cứu/tìm kiếm tương tác, cùng cấu hình base URL/timeout —
// xem InteractivePmisClient. Cũng không retry, cùng lý do như HttpClient "PMIS" ở trên.
builder.Services.AddHttpClient("PMIS-Interactive", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Endpoints:PMIS"] ?? "https://api.pmis.mock/");
})
.AddPolicyHandler(timeoutPolicy);

// 4. CA HttpClient
builder.Services.AddHttpClient("CA", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Endpoints:CA"] ?? "https://api.ca.mock/");
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(caCircuitBreakerPolicy)
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

    // Dọn PMIS_API_CALL_LOG (lịch sử gọi PMIS thật) cũ hơn 30 ngày — bảng có thể phình rất nhanh vì
    // mỗi trang trong 1 lượt đồng bộ là 1 dòng log, xem PmisApiCallLogCleanupJob.
    var cleanupJobKey = new JobKey("PmisApiCallLogCleanupJob");
    q.AddJob<PmisApiCallLogCleanupJob>(opts => opts.WithIdentity(cleanupJobKey));
    q.AddTrigger(opts => opts
        .ForJob(cleanupJobKey)
        .WithIdentity("PmisApiCallLogCleanupJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInHours(24).RepeatForever())
    );

    // Dọn SYNC_HISTORY/SYNC_HISTORY_DETAIL cũ hơn 30 ngày — bảng này trước đây KHÔNG có cơ chế dọn
    // nào, đã gây tràn tablespace (ORA-01653) thật trên production, xem SyncHistoryCleanupJob.
    var syncHistoryCleanupJobKey = new JobKey("SyncHistoryCleanupJob");
    q.AddJob<SyncHistoryCleanupJob>(opts => opts.WithIdentity(syncHistoryCleanupJobKey));
    q.AddTrigger(opts => opts
        .ForJob(syncHistoryCleanupJobKey)
        .WithIdentity("SyncHistoryCleanupJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInHours(24).RepeatForever())
    );

    // Đánh dấu FAILED cho các dòng SYNC_HISTORY kẹt RUNNING do pod crash giữa lượt chạy — xem
    // SyncHistoryWatchdogJob.
    var syncHistoryWatchdogJobKey = new JobKey("SyncHistoryWatchdogJob");
    q.AddJob<SyncHistoryWatchdogJob>(opts => opts.WithIdentity(syncHistoryWatchdogJobKey));
    q.AddTrigger(opts => opts
        .ForJob(syncHistoryWatchdogJobKey)
        .WithIdentity("SyncHistoryWatchdogJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever())
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
builder.Services.AddScoped<EvnHanoi.SyncService.Infrastructure.Messaging.IMessageProducer, EvnHanoi.SyncService.Infrastructure.Messaging.RabbitMQProducer>();

builder.Services.AddSingleton<EvnHanoi.SyncService.Services.IPmisSyncTriggerService, EvnHanoi.SyncService.Services.PmisSyncTriggerService>();
builder.Services.AddHostedService<EvnHanoi.SyncService.Workers.EquipmentSyncWorker>();
// PmisSyncWorker: luồng RabbitMQ (equipment_sync_queue) độc lập với PmisScheduledSyncJob (Quartz) ở
// trên — xem XML doc trên class PmisSyncWorker. Cảnh báo "Pmis:ApiUrl chưa được thiết lập" lúc khởi
// động chỉ tắt riêng luồng này, không phải dấu hiệu đồng bộ PMIS chính đang hỏng.
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
