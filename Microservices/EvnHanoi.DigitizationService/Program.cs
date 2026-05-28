using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    SerilogSetupHelper.ConfigureSerilog(context, configuration);
});
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
app.UseAuthorization();
app.MapControllers();

app.Run();
