using DbUp;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Reflection;
using DbUp.Oracle;

namespace EvnHanoi.Infrastructure.Database;

public static class DatabaseMigrationHelper
{
    public static bool RunMigrations(IConfiguration configuration, string connectionStringName = "DefaultConnection")
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Log.Error("Database connection string '{ConnectionStringName}' is not found.", connectionStringName);
            return false;
        }

        Log.Information("Starting Database Migration for {ConnectionStringName}...", connectionStringName);

        // For Oracle, EnsureDatabase.For.OracleDatabase is available if DbUp.Oracle supports it,
        // but typically schemas are pre-created in Oracle. We will just execute scripts.
        var upgrader = DeployChanges.To
            .OracleDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .WithVariablesDisabled()
            .LogToConsole() // or use custom Serilog adapter
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            Log.Error(result.Error, "Database migration failed!");
            return false;
        }

        Log.Information("Database migration completed successfully!");
        return true;
    }
}
