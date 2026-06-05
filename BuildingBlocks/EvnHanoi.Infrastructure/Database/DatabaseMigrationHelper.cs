using DbUp;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Reflection;
using DbUp.Oracle;

namespace EvnHanoi.Infrastructure.Database;

public static class DatabaseMigrationHelper
{
    public static bool RunMigrations(IConfiguration configuration, string serviceFolder, bool runSeeds = false, string connectionStringName = "DefaultConnection")
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);

        if (configuration.GetValue<bool>("DbUp:RunSeeds"))
        {
            runSeeds = true;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Log.Error("Database connection string '{ConnectionStringName}' is not found.", connectionStringName);
            return false;
        }

        Log.Information("Starting Database Migration for {ServiceFolder} ({ConnectionStringName})...", serviceFolder, connectionStringName);

        // Run schema migrations for the service folder
        var schemaUpgrader = DeployChanges.To
            .OracleDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.Contains($".Migrations.{serviceFolder}."))
            .WithVariablesDisabled()
            .LogToConsole()
            .Build();

        var schemaResult = schemaUpgrader.PerformUpgrade();

        if (!schemaResult.Successful)
        {
            Log.Error(schemaResult.Error, "Database schema migration for {ServiceFolder} failed!", serviceFolder);
            return false;
        }

        Log.Information("Database schema migration for {ServiceFolder} completed successfully!", serviceFolder);

        // If runSeeds is enabled, run seeds migrations from the Seeds folder
        if (runSeeds)
        {
            Log.Information("Starting Seed Migrations for {ServiceFolder}...", serviceFolder);
            var seedUpgrader = DeployChanges.To
                .OracleDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(
                    Assembly.GetExecutingAssembly(),
                    name => name.Contains(".Migrations.Seeds.") && name.Contains($".{serviceFolder}."))
                .WithVariablesDisabled()
                .LogToConsole()
                .Build();

            var seedResult = seedUpgrader.PerformUpgrade();

            if (!seedResult.Successful)
            {
                Log.Error(seedResult.Error, "Database seed migration for {ServiceFolder} failed!", serviceFolder);
                return false;
            }

            Log.Information("Database seed migration for {ServiceFolder} completed successfully!", serviceFolder);
        }

        return true;
    }
}
