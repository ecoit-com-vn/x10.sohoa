using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.DigitizationService.Workers;
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

var builder = WebApplication.CreateBuilder(args);

// Register custom font resolver for PdfSharpCore
PdfSharpCore.Fonts.GlobalFontSettings.FontResolver = new EvnHanoi.DigitizationService.Helpers.CustomFontResolver();

builder.AddServiceDefaults();

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    SerilogSetupHelper.ConfigureSerilog(context, configuration);
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditActionFilter>();
});
builder.Services.AddStructuredValidationErrors();
builder.Services.AddOpenApi();

// DI Configuration
var rabbitFactory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
    VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
    Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var port) ? port : 5672,
    // Cho phép OcrWorker/ExtractionWorker xử lý nhiều message song song — 1 message treo/chậm
    // không còn chặn toàn bộ các message khác phía sau trong cùng queue.
    //
    // Hạ 4 -> 2: nghẽn thật của luồng OCR là GPU của ocr_vl_server (đo được ~4,4 crop/giây, GPU
    // 86–100%), không phải service này. Đẩy nhiều tài liệu song song KHÔNG tăng thông lượng mà chỉ
    // làm mỗi trang chờ lâu hơn, vượt timeout phía client rồi bị retry — vòng xoáy tự làm nặng
    // thêm. Với 2, mỗi instance xử lý tối đa 2 tài liệu cùng lúc.
    ConsumerDispatchConcurrency = 2
};
var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
builder.Services.AddSingleton<IConnection>(rabbitConnection);
builder.Services.AddAuditInfrastructure("DigitizationService");
builder.Services.AddHostedService<DigitizationMessagingTopologyInitializer>();

builder.Services.AddDapperInfrastructure(builder.Configuration);
builder.Services.AddScoped<IMinioStorageService, MinioStorageService>();
builder.Services.AddScoped<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddScoped<IFileAttachmentRepository, FileAttachmentRepository>();
builder.Services.AddScoped<IDigitizationTaskRepository, DigitizationTaskRepository>();
builder.Services.AddScoped<IOcrTrainingDataRepository, OcrTrainingDataRepository>();
builder.Services.AddScoped<IVirtualFolderRepository, VirtualFolderRepository>();
builder.Services.AddScoped<EvnHanoi.DigitizationService.Repositories.OcrModule.IOcrModuleRepository, EvnHanoi.DigitizationService.Repositories.OcrModule.OcrModuleRepository>();
builder.Services.AddScoped<EvnHanoi.DigitizationService.Services.OcrModule.IOcrJsonMaterializer, EvnHanoi.DigitizationService.Services.OcrModule.OcrJsonMaterializer>();
builder.Services.AddScoped<EvnHanoi.DigitizationService.Core.Services.OcrModule.IOcrModuleSealSignatureService, EvnHanoi.DigitizationService.Core.Services.OcrModule.OcrModuleSealSignatureService>();
builder.Services.AddScoped<EvnHanoi.DigitizationService.Core.Services.OcrModule.IOcrModuleSpellcheckService, EvnHanoi.DigitizationService.Core.Services.OcrModule.OcrModuleSpellcheckService>();
builder.Services.AddScoped<EvnHanoi.DigitizationService.Core.Services.OcrModule.IOcrModuleErrorAnalysisAggregator, EvnHanoi.DigitizationService.Core.Services.OcrModule.OcrModuleErrorAnalysisAggregator>();
builder.Services.AddScoped<EvnHanoi.DigitizationService.Core.Services.OcrModule.IOcrModuleRegionCorrectionService, EvnHanoi.DigitizationService.Core.Services.OcrModule.OcrModuleRegionCorrectionService>();
builder.Services.AddScoped<ISearchablePdfBuilder, SearchablePdfBuilder>();
builder.Services.AddScoped<EvnHanoi.DocumentProcessing.IDocumentCompressionService, EvnHanoi.DocumentProcessing.DocumentCompressionService>();

// Timeout gọi ocr_vl_server/LLM đọc từ cấu hình (AIModelServers) thay vì hard-code — điều chỉnh
// được qua appsettings mà không cần build lại. Áp dụng CHO TỪNG LỆNH GỌI (mỗi trang PDF một lệnh
// gọi riêng trong OcrWorker), không cộng dồn theo số trang của tài liệu.
// Mặc định 900s (appsettings.json cũng đặt 900): đo trên server OCR thật (1 GPU Tesla T4) công suất
// là ~4,4 crop/giây, còn tài liệu thực tế có 96–744 vùng chữ mỗi trang — một trang dày cần ~170s
// NGAY CẢ KHI độc chiếm server, và đo được tới 330s khi nhiều tài liệu chạy song song. Với 180s như
// trước, client bỏ cuộc giữa lúc server vẫn đang xử lý: ocr_vl_server ghi hàng loạt "OCR crop
// failed" rồi trả 0/N vùng, job bị retry và càng làm tải nặng thêm.
// Chi tiết đo đạc: BAO_CAO_SU_CO_OCR_LLM_SERVER_104.md.
var ocrPageAttemptTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("AIModelServers:OcrPageAttemptTimeoutSeconds", 900));
var ocrPageTotalTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("AIModelServers:OcrPageTotalTimeoutSeconds", 900));
// BẮT BUỘC >= 2 × AttemptTimeout, nếu không AddStandardResilienceHandler ném lỗi validate ngay lúc
// khởi động service (900s × 2 = 1800s = 30 phút).
var ocrPageSamplingDuration = TimeSpan.FromMinutes(builder.Configuration.GetValue("AIModelServers:OcrPageCircuitBreakerSamplingMinutes", 30));
var llmAttemptTimeout = TimeSpan.FromMinutes(builder.Configuration.GetValue("AIModelServers:LlmAttemptTimeoutMinutes", 3));
var llmTotalTimeout = TimeSpan.FromMinutes(builder.Configuration.GetValue("AIModelServers:LlmTotalTimeoutMinutes", 6));
var llmSamplingDuration = TimeSpan.FromMinutes(builder.Configuration.GetValue("AIModelServers:LlmCircuitBreakerSamplingMinutes", 12));

// Dùng cho lệnh gọi OCR từng trang (POST /ocr_page) trong OcrWorker — trước đây tên "NoTimeout"
// nhưng thực chất bị treo tới 1 giờ/lần gọi, gây nghẽn cả hàng đợi khi ocr_vl_server không phản hồi.
builder.Services.AddHttpClient("OcrPageClient", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan; // HttpClient.Timeout tắt — để AddStandardResilienceHandler bên dưới kiểm soát
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    KeepAlivePingDelay = TimeSpan.FromSeconds(30),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(15)
})
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = ocrPageAttemptTimeout;
    options.TotalRequestTimeout.Timeout = ocrPageTotalTimeout;
    // SamplingDuration phải >= 2 × AttemptTimeout
    options.CircuitBreaker.SamplingDuration = ocrPageSamplingDuration;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
});

// Dùng cho lệnh gọi LLM (trích xuất, sửa chính tả) — OcrWorker (spellcheck), ExtractionWorker
// (trích xuất), OcrModuleSpellcheckService. Cho phép thời gian rộng rãi hơn OCR ảnh vì sinh nội
// dung dài có thể chậm hơn, nhưng vẫn phải hữu hạn.
builder.Services.AddHttpClient("LlmClient", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    KeepAlivePingDelay = TimeSpan.FromSeconds(30),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(15)
})
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = llmAttemptTimeout;
    options.TotalRequestTimeout.Timeout = llmTotalTimeout;
    options.CircuitBreaker.SamplingDuration = llmSamplingDuration;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
});

builder.Services.AddHostedService<OcrWorker>();
builder.Services.AddHostedService<ExtractionWorker>();

builder.Services.AddPermissionDiscovery("DigitizationService");

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

var app = builder.Build();

// Run DbUp Migrations
try
{
    DatabaseMigrationHelper.RunMigrations(app.Configuration, "DigitizationService", runSeeds: app.Environment.IsDevelopment());
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to run database migrations.");
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

