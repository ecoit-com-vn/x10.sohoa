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

                Console.WriteLine("\n--- Listing Database Tables ---");
                var tables = await conn.QueryAsync<string>("SELECT table_name FROM user_tables ORDER BY table_name");
                foreach (var table in tables)
                {
                    Console.WriteLine(table);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

