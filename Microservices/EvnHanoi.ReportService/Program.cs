using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using Serilog;
using Scalar.AspNetCore;
using System;

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
builder.Services.AddScoped<EvnHanoi.ReportService.Core.Interfaces.IReportRepository, EvnHanoi.ReportService.Infrastructure.Repositories.ReportRepository>();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
