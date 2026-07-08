using Elastic.Clients.Elasticsearch;

using EvnHanoi.ReportService.Core.Interfaces;

using EvnHanoi.ReportService.Infrastructure.Elasticsearch;

using EvnHanoi.ReportService.Infrastructure.Repositories;

using EvnHanoi.ReportService.Infrastructure.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.IdentityModel.Tokens;

using Minio;

using Serilog;

using Scalar.AspNetCore;

using System.Text;

using EvnHanoi.Infrastructure.Database;

using EvnHanoi.Infrastructure.Logging;

using EvnHanoi.Infrastructure.Messaging;

using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;
using RabbitMQ.Client;



var builder = WebApplication.CreateBuilder(args);



builder.AddServiceDefaults();



builder.Host.UseSerilog((context, services, configuration) =>

{

    SerilogSetupHelper.ConfigureSerilog(context, configuration);

});



builder.Services.AddControllers(options =>

{

    options.Filters.Add<DynamicPermissionFilter>();
    options.Filters.Add<AuditActionFilter>();

});

builder.Services.AddOpenApi();

builder.Services.AddMemoryCache();

builder.Services.AddDapperInfrastructure(builder.Configuration);

builder.Services.AddScoped<EvnHanoi.ReportService.Core.Interfaces.IReportRepository, EvnHanoi.ReportService.Infrastructure.Repositories.ReportRepository>();

builder.Services.AddScoped<IReportDossierRepository, ReportDossierRepository>();

builder.Services.AddScoped<IReportDossierSearchService, ReportDossierSearchService>();

builder.Services.AddScoped<IReportDossierDetailRepository, ReportDossierDetailRepository>();

builder.Services.AddScoped<ReportDossierEsSearchRepository>();

builder.Services.AddScoped<IReportFileDownloadTokenService, ReportFileDownloadTokenService>();

builder.Services.AddScoped<IReportFileStorageService, ReportFileStorageService>();

builder.Services.AddPermissionDiscovery("ReportService");

var rabbitFactory = new ConnectionFactory
{
    HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
    VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
    Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var rabbitPort) ? rabbitPort : 5672
};
var rabbitConnection = await rabbitFactory.CreateConnectionAsync();
builder.Services.AddSingleton<IConnection>(rabbitConnection);
builder.Services.AddAuditInfrastructure("ReportService");

builder.Services.AddHttpContextAccessor();



var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrWhiteSpace(redisConnectionString))

{

    builder.Services.AddStackExchangeRedisCache(options =>

    {

        options.Configuration = redisConnectionString;

    });

}

else

{

    builder.Services.AddDistributedMemoryCache();

}



builder.Services.AddSingleton<IMinioClient>(sp =>

{

    var config = sp.GetRequiredService<IConfiguration>();

    var endpoint = config["MinIO:Endpoint"] ?? "localhost:9000";

    var accessKey = config["MinIO:AccessKey"] ?? "minioadmin";

    var secretKey = config["MinIO:SecretKey"] ?? "minioadmin";

    var useSslConfig = config["MinIO:UseSSL"];

    var useSsl = !string.IsNullOrEmpty(useSslConfig) && bool.Parse(useSslConfig);



    return new MinioClient()

        .WithEndpoint(endpoint)

        .WithCredentials(accessKey, secretKey)

        .WithSSL(useSsl)

        .Build();

});



var esUri = builder.Configuration["Elasticsearch:Url"]
            ?? builder.Configuration["Elasticsearch:Uri"]
            ?? "http://localhost:9200";

Log.Information("ReportService Elasticsearch: {ElasticsearchUrl}", esUri);

var esSettings = new ElasticsearchClientSettings(new Uri(esUri))
    .DefaultIndex(DossierMessaging.IndexName);
builder.Services.AddSingleton(new ElasticsearchClient(esSettings));



builder.Services.AddHttpClient("IdentityService", client =>

{

    client.BaseAddress = new Uri(builder.Configuration["Services:IdentityService"] ?? "http://identityservice");

});



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



try

{

    DatabaseMigrationHelper.RunMigrations(app.Configuration, "ReportService", runSeeds: app.Environment.IsDevelopment());

}

catch (Exception ex)

{

    Log.Error(ex, "Failed to run database migrations.");

}



if (app.Environment.IsDevelopment())

{

    app.MapOpenApi();

    app.MapScalarApiReference();

}



app.MapDefaultEndpoints();



if (!app.Environment.IsDevelopment())

{

    app.UseHttpsRedirection();

}



app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();



app.Run();


