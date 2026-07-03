using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.Infrastructure.Security;
using Serilog;
using Scalar.AspNetCore;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Infrastructure.Repositories;
using EvnHanoi.WorkflowService.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Register Dapper Type Handler for Guid conversion from string columns
Dapper.SqlMapper.AddTypeHandler(new EvnHanoi.WorkflowService.Infrastructure.Repositories.GuidTypeHandler());

// Map string to DbType.AnsiString globally in Dapper to prevent Oracle Implicit Type Conversion (VARCHAR2 vs NVARCHAR2) causing index suppression
Dapper.SqlMapper.AddTypeMap(typeof(string), System.Data.DbType.AnsiString);

builder.AddServiceDefaults();

// 1. Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    SerilogSetupHelper.ConfigureSerilog(context, configuration);
});

// 2. Add services to the container
builder.Services.AddMemoryCache();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<EvnHanoi.Infrastructure.Security.DynamicPermissionFilter>();
});
builder.Services.AddStructuredValidationErrors();
builder.Services.AddOpenApi();

// CORS — cho phép Angular frontend gọi API
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddDapperInfrastructure(builder.Configuration);
builder.Services.AddPermissionDiscovery("WorkflowService");

builder.Services.AddHttpClient("IdentityService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:IdentityService"] ?? "http://identityservice");
});

// HttpClient gọi EquipmentService để đồng bộ trạng thái hồ sơ (API nội bộ, kèm shared-secret).
builder.Services.AddHttpClient("EquipmentService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:EquipmentService"] ?? "http://equipmentservice");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("X-Internal-Token", builder.Configuration["Internal:Token"] ?? "");
});

builder.Services.AddScoped<IBorrowRecordRepository, BorrowRecordRepository>();
builder.Services.AddScoped<IBorrowRecordService, BorrowRecordService>();
builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
builder.Services.AddScoped<WorkflowDefinitionCacheService>();
builder.Services.AddScoped<IWorkflowEngineService, WorkflowEngineService>();
builder.Services.AddScoped<IWorkflowIntegrationHandler, BorrowRecordWorkflowHandler>();
builder.Services.AddScoped<IWorkflowIntegrationHandler, DossierWorkflowHandler>();
builder.Services.AddScoped<IWorkflowIntegrationHandler, DossierDigitizationWorkflowHandler>();
builder.Services.AddScoped<IBpmnValidatorService, BpmnValidatorService>();
builder.Services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();

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

// 4. Run DbUp Migrations
try
{
    DatabaseMigrationHelper.RunMigrations(app.Configuration, "WorkflowService", runSeeds: app.Environment.IsDevelopment());
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
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
