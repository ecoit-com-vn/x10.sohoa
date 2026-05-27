using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    SerilogSetupHelper.ConfigureSerilog(context, configuration);
});

// 2. Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddScoped<EvnHanoi.EquipmentService.Core.Interfaces.IDossierRepository, EvnHanoi.EquipmentService.Infrastructure.Repositories.DossierRepository>();

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
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
