using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using Serilog;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 1. Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    SerilogSetupHelper.ConfigureSerilog(context, configuration);
});

// 2. Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var rabbitFactory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
    Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var port) ? port : 5672
};
var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
builder.Services.AddSingleton<IConnection>(rabbitConnection);

builder.Services.AddDapperInfrastructure(builder.Configuration);
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDossierRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.DossierRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IEquipmentRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.EquipmentRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IEquipmentTypeRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.EquipmentTypeRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IPhysicalStorageRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.PhysicalStorageRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.ICatalogRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.CatalogRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IEavFormTemplateRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.EavFormTemplateRepository>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IEavFormTemplateService, EvnHanoi.EquipmentService.Core.Services.EavFormTemplateService>();
builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IElasticsearchService, EvnHanoi.EquipmentService.Infrastructure.Services.ElasticsearchService>();
builder.Services.AddSingleton<EvnHanoi.EquipmentService.Core.Interfaces.IMessageProducer, EvnHanoi.EquipmentService.Infrastructure.Messaging.RabbitMQProducer>();

// 3. Configure JWT Authentication
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
    DatabaseMigrationHelper.RunMigrations(app.Configuration);
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to run database migrations.");
}

try
{
    using var scope = app.Services.CreateScope();
    var elasticService = scope.ServiceProvider.GetRequiredService<EvnHanoi.EquipmentService.Core.Interfaces.IElasticsearchService>();
    await elasticService.CreateIndexAsync();
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to initialize Elasticsearch indices.");
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

