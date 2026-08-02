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
    Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var port) ? port : 5672
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

builder.Services.AddHttpClient("OcrVlClient", client => 
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
    options.AttemptTimeout.Timeout = TimeSpan.FromHours(1);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromHours(1);
    // SamplingDuration phải >= 2 × AttemptTimeout
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromHours(2);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
});

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
    options.AttemptTimeout.Timeout = TimeSpan.FromHours(1);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromHours(1);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromHours(2);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("NoTimeout", client => 
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
    options.AttemptTimeout.Timeout = TimeSpan.FromHours(1);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromHours(1);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromHours(2);
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

