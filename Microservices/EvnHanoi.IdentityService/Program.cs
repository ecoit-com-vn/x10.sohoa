using System.Text;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.IdentityService.Controllers;
using EvnHanoi.IdentityService.Infrastructure.Repositories;
using EvnHanoi.IdentityService.Infrastructure.Services;
using EvnHanoi.IdentityService.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Scalar.AspNetCore;

using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;

var builder = WebApplication.CreateBuilder(args);

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
    options.Filters.Add<EvnHanoi.IdentityService.Infrastructure.Security.DynamicPermissionFilter>();
    options.Filters.Add<AuditActionFilter>();
});
builder.Services.AddStructuredValidationErrors();
builder.Services.AddOpenApi();

// DI Configuration
builder.Services.AddDapperInfrastructure(builder.Configuration);
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<ISystemParamRepository, SystemParamRepository>();
builder.Services.AddScoped<IOrganizationUnitRepository, OrganizationUnitRepository>();
builder.Services.AddScoped<IMenuRepository, MenuRepository>();
builder.Services.AddScoped<IUserGroupRepository, UserGroupRepository>();
builder.Services.AddScoped<IUserUnitRoleRepository, UserUnitRoleRepository>();
builder.Services.AddScoped<IUploadConfigRepository, UploadConfigRepository>();
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();
builder.Services.AddScoped<IAvatarStorageService, AvatarStorageService>();
builder.Services.AddScoped<EvnHanoi.IdentityService.Infrastructure.Security.DynamicSeederService>();
builder.Services.AddScoped<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();

// RabbitMQ Configuration & Consumer Registration
var rabbitFactory = new RabbitMQ.Client.ConnectionFactory
{
    HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
    Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var port) ? port : 5672
};
var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
builder.Services.AddSingleton<RabbitMQ.Client.IConnection>(rabbitConnection);
builder.Services.AddAuditInfrastructure("IdentityService");
builder.Services.AddHostedService<EvnHanoi.IdentityService.Infrastructure.Security.PermissionRegistrationConsumer>();


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
    DatabaseMigrationHelper.RunMigrations(app.Configuration, "IdentityService", runSeeds: app.Environment.IsDevelopment());
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
