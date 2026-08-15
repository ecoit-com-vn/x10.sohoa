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
            using (var conn = new OracleConnection(connStr))
            {
                conn.Open();
                
                Console.WriteLine("=== COLUMNS IN TABLE 'ORGANIZATION_UNIT' ===");
                var columns = await conn.QueryAsync<dynamic>(@"
                    SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH, NULLABLE 
                    FROM USER_TAB_COLUMNS 
                    WHERE TABLE_NAME = 'ORGANIZATION_UNIT'
                    ORDER BY COLUMN_ID");

                foreach (var c in columns)
                {
                    Console.WriteLine($"   - {c.COLUMN_NAME}: {c.DATA_TYPE}({c.DATA_LENGTH}), Nullable: {c.NULLABLE}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
