using System;
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
            using (var conn = new OracleConnection(connStr))
            {
                conn.Open();
                await conn.ExecuteAsync("UPDATE ORGANIZATION_UNIT SET ORGIDSSO = 1 WHERE Id = 1");
                Console.WriteLine("Updated ORGIDSSO = 1 for unit Id = 1 successfully!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
