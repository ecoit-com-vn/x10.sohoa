using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
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

// RemoveAllResilienceHandlers(): builder.AddServiceDefaults() gắn "Standard Resilience Handler" (timeout
// 10 phút/lần thử, 22 phút tổng — tinh chỉnh cho LLM/OCR) làm mặc định cho MỌI HttpClient, kể cả client
// nội bộ nhanh này giữa 2 service cùng cluster — bỏ đi để dùng đúng HttpClient.Timeout mặc định (100s)
// thay vì có thể treo tới 22 phút nếu EquipmentService phản hồi chậm.
#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient("EquipmentServiceInternal", client =>
{
    var baseUrl = builder.Configuration["Services:EquipmentService"] ?? "http://localhost:5254";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
})
.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

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
//
// RemoveAllResilienceHandlers() BẮT BUỘC phải gọi trước — builder.AddServiceDefaults() (Program.cs đầu
// file) gắn "Standard Resilience Handler" (retry 3 lần + circuit breaker + timeout riêng) làm MẶC ĐỊNH
// cho MỌI HttpClient của MỌI microservice (ConfigureHttpClientDefaults trong
// EvnHanoi.ServiceDefaults/Extensions.cs, vốn tinh chỉnh cho các API LLM/OCR chạy hàng chục phút, không
// hợp với 1 API REST nhanh như PMIS). Trước đây KHÔNG gọi hàm này — "PMIS"/"PMIS-Interactive" vẫn âm
// thầm bị handler mặc định đó retry 3 lần/lỗi (xác nhận qua log thật: "Source: '-standard//Standard-
// Retry'"), NGƯỢC HẲN với comment/ý định ở trên. Hệ quả: 1 lỗi PMIS thực tế thành 3-4 lần thử kết nối
// TCP thật mỗi lần, nhân với PmisScheduledSyncJob (tick mỗi phút x 3 đối tượng x tới 50 trang) +
// PmisSyncWorker/PmisPublisherWorker (RabbitMQ) + tra cứu tương tác cùng dùng chung client này chạy suốt
// vòng đời pod — dễ dồn cạn cổng/kết nối cục bộ theo thời gian dù test tay (`curl` 1 lần) luôn thành
// công vì chỉ mở đúng 1 kết nối. Giờ bỏ hẳn handler mặc định, chỉ giữ đúng policy tự khai báo bên dưới.
// RemoveAllResilienceHandlers() còn đánh dấu "experimental" (EXTEXP0001) ở version SDK hiện tại — API ổn
// định về hành vi (chỉ gỡ handler đã gắn qua ConfigureHttpClientDefaults), tắt cảnh báo có chủ đích.
#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient("PMIS", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Endpoints:PMIS"] ?? "https://api.pmis.mock/");
})
.RemoveAllResilienceHandlers()
.AddPolicyHandler(timeoutPolicy);

// 3b. PMIS HttpClient — bản dành cho API tra cứu/tìm kiếm tương tác, cùng cấu hình base URL/timeout —
// xem InteractivePmisClient. Cũng không retry, cùng lý do như HttpClient "PMIS" ở trên (kể cả việc bỏ
// Standard Resilience Handler mặc định).
builder.Services.AddHttpClient("PMIS-Interactive", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Endpoints:PMIS"] ?? "https://api.pmis.mock/");
})
.RemoveAllResilienceHandlers()
.AddPolicyHandler(timeoutPolicy);

// 4. CA HttpClient — cũng bỏ Standard Resilience Handler mặc định, cùng lý do như "PMIS" ở trên: retry
// mặc định (3 lần) + timeout 10 phút/lần thử sẽ chồng thêm lên trên retryPolicy/caCircuitBreakerPolicy
// đã tự khai báo, khiến 1 lỗi CA thực tế bị thử lại tới 2 lớp lồng nhau thay vì đúng 1 lớp như ý định.
builder.Services.AddHttpClient("CA", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Endpoints:CA"] ?? "https://api.ca.mock/");
})
.RemoveAllResilienceHandlers()
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(caCircuitBreakerPolicy)
.AddPolicyHandler(bulkheadPolicy);
#pragma warning restore EXTEXP0001

// 5. Quartz Scheduler — PmisScheduledSyncJob thay PmisSyncScheduler cũ (chỉ log, chưa lưu gì).
// Tick mỗi phút, tự kiểm tra SYNC_CONFIG của từng đối tượng để biết có tới hạn hay không.
//
// 3 JobKey RIÊNG (PmisSyncJob-Substation/TransmissionLine/Equipment) dùng CHUNG 1 class
// PmisScheduledSyncJob, mỗi JobDetail mang đúng 1 SyncObjectType qua JobDataMap — KHÔNG còn 1 JobKey
// duy nhất lặp cả 3 loại trong 1 Execute() như trước. Lý do: [DisallowConcurrentExecution] scope theo
// JobKey, nên trước đây nếu Thiết bị chạy nhiều giờ (dữ liệu lớn), nó chặn luôn Execute() mới của CHÍNH
// JobKey đó — khiến Trạm/Đường dây (xử lý SAU Thiết bị trong cùng vòng lặp cũ) bị đói theo lịch suốt
// thời gian đó dù đã tới hạn từ lâu. Tách JobKey khiến 3 loại độc lập hoàn toàn về lịch chạy, khớp với
// RedLock vốn đã tách theo objectType (sync:lock:pmis:{objectType}) từ trước.
builder.Services.AddQuartz(q =>
{
    foreach (var objectType in new[] { SyncObjectType.Substation, SyncObjectType.TransmissionLine, SyncObjectType.Equipment })
    {
        var jobKey = new JobKey($"PmisSyncJob-{objectType}");
        q.AddJob<PmisScheduledSyncJob>(opts => opts
            .WithIdentity(jobKey)
            .UsingJobData(PmisScheduledSyncJob.ObjectTypeDataKey, objectType));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"PmisSyncJob-{objectType}-trigger")
            .WithSimpleSchedule(x => x.WithIntervalInMinutes(1).RepeatForever())
        );
    }

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

    // Đối chiếu nhẹ 1 lần/ngày (đối chiếu mã PMIS thiếu do phân trang lệch + đếm thiết bị chuyển TBA gần
    // đây) — KHÔNG tự sửa gì, chỉ log cảnh báo cho admin, xem PmisReconciliationJob.
    var reconciliationJobKey = new JobKey("PmisReconciliationJob");
    q.AddJob<PmisReconciliationJob>(opts => opts.WithIdentity(reconciliationJobKey));
    q.AddTrigger(opts => opts
        .ForJob(reconciliationJobKey)
        .WithIdentity("PmisReconciliationJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInHours(24).RepeatForever())
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
