using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.DigitizationService.Workers;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using Serilog;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    SerilogSetupHelper.ConfigureSerilog(context, configuration);
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// DI Configuration
builder.Services.AddScoped<IMinioStorageService, MinioStorageService>();
builder.Services.AddScoped<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddScoped<IFileAttachmentRepository, FileAttachmentRepository>();
builder.Services.AddScoped<IDigitizationTaskRepository, DigitizationTaskRepository>();
builder.Services.AddScoped<IOcrTrainingDataRepository, OcrTrainingDataRepository>();
builder.Services.AddScoped<IVirtualFolderRepository, VirtualFolderRepository>();
builder.Services.AddHostedService<OcrTaskConsumer>();

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
    DatabaseMigrationHelper.RunMigrations(app.Configuration);
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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

