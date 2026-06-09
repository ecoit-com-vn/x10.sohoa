using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Dapper;

class Program
{
    static async Task Main()
    {
        string host = "192.168.1.199";
        int port = 1521;
        string user = "c##qlshx10";
        string password = "Ecoit@123qwe";
        string service = "orcl";
        
        string connStr = $"Data Source={host}:{port}/{service};User Id={user};Password={password};Pooling=false;";
        try
        {
            Console.WriteLine("Connecting to Oracle database...");
            using (var conn = new OracleConnection(connStr))
            {
                conn.Open();
                Console.WriteLine("Connected!");

                // Check CATALOG_TYPE table
                Console.WriteLine("\n--- Checking CATALOG_TYPE table ---");
                try
                {
                    var types = await conn.QueryAsync("SELECT * FROM CATALOG_TYPE");
                    Console.WriteLine($"Found {types.Count()} rows in CATALOG_TYPE:");
                    foreach (var t in types)
                    {
                        Console.WriteLine($" - Code: {t.CODE}, Name: {t.NAME}, HasParent: {t.HASPARENT}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error querying CATALOG_TYPE: {ex.Message}");
                }

                // Check CATALOG table columns
                Console.WriteLine("\n--- Checking CATALOG table columns ---");
                try
                {
                    var cols = await conn.QueryAsync<string>(
                        "SELECT column_name FROM user_tab_cols WHERE table_name = 'CATALOG'");
                    Console.WriteLine("Columns in CATALOG table:");
                    Console.WriteLine(string.Join(", ", cols));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error querying CATALOG columns: {ex.Message}");
                }

                // Check APP_MENU submenus
                Console.WriteLine("\n--- Checking APP_MENU catalog submenus ---");
                try
                {
                    var menus = await conn.QueryAsync(
                        "SELECT Id, Name, Url FROM APP_MENU WHERE ParentId = 10 ORDER BY SortOrder");
                    Console.WriteLine($"Found {menus.Count()} submenus under parent menu 10:");
                    foreach (var m in menus)
                    {
                        Console.WriteLine($" - Id: {m.ID}, Name: {m.NAME}, Url: {m.URL}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error querying APP_MENU: {ex.Message}");
                }

                // Check SCHEMA_VERSIONS
                Console.WriteLine("\n--- Checking SchemaVersions ---");
                try
                {
                    var migrations = await conn.QueryAsync<string>("SELECT SchemaVersionID FROM SchemaVersions ORDER BY SchemaVersionID");
                    Console.WriteLine("Migrations run on DB:");
                    foreach (var mig in migrations)
                    {
                        Console.WriteLine($" - {mig}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error querying SchemaVersions: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
