using DbUp;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DbUp.Oracle;

namespace EvnHanoi.Infrastructure.Database;

/// <summary>
/// Sắp xếp script migration theo ĐÚNG số thứ tự, bất kể là file SQL (<c>NNNN_Ten.sql</c>) hay
/// migration code (<c>MigrationNNNN_Ten.cs</c>).
///
/// VÌ SAO CẦN: DbUp sắp xếp theo tên script; với script code, tên là FullName của class nên luôn
/// bắt đầu bằng "Migration". Ký tự 'M' > '0', nên MỌI file .cs bị đẩy xuống chạy SAU TOÀN BỘ file
/// .sql — dù số thứ tự nhỏ hơn. Hệ quả thực tế: Migration0028_RbacPermissionGroupAndRole_V2.cs
/// (tạo PERMISSION_GROUP, PERMISSION_GROUP_PERMISSION, ROLE...) chạy sau 0029_SeedOperatorAndUser
/// Permissions.sql (dùng chính các bảng đó) -> ORA-00942 trên mọi database mới, và DbUp dừng cả
/// lượt migrate nên các service sau cũng không tạo được bảng.
///
/// Chuẩn hoá bằng cách bỏ tiền tố "Migration" khi nó đứng ngay trước một chữ số, rồi so sánh
/// ordinal. Nếu 2 tên chuẩn hoá bằng nhau thì so tiếp tên gốc — BẮT BUỘC phải có bước này: DbUp
/// dùng cùng comparer để đối chiếu với journal (script coi là đã chạy khi Compare == 0), nên hai
/// tên khác nhau không bao giờ được phép trả về 0.
/// </summary>
internal sealed class MigrationScriptNameComparer : IComparer<string>
{
    private static readonly Regex MigrationPrefix = new(@"\.Migration(?=\d)", RegexOptions.Compiled);

    public int Compare(string? x, string? y)
    {
        var normalized = string.CompareOrdinal(Normalize(x), Normalize(y));
        return normalized != 0 ? normalized : string.CompareOrdinal(x, y);
    }

    private static string Normalize(string? name) =>
        string.IsNullOrEmpty(name) ? string.Empty : MigrationPrefix.Replace(name, ".");
}

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
            .OracleDatabaseWithSemicolonDelimiter(connectionString)
            .WithScriptsAndCodeEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.Contains($".Migrations.{serviceFolder}."))
            .WithVariablesDisabled()
            .WithScriptNameComparer(new MigrationScriptNameComparer())
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
                .OracleDatabaseWithSemicolonDelimiter(connectionString)
                .WithScriptsAndCodeEmbeddedInAssembly(
                    Assembly.GetExecutingAssembly(),
                    name => name.Contains(".Migrations.Seeds.") && name.Contains($".{serviceFolder}."))
                .WithVariablesDisabled()
                .WithScriptNameComparer(new MigrationScriptNameComparer())
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
