using System;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Dapper;

class Program
{
    static async Task Main()
    {
        string connStr = "Data Source=192.168.1.199:1521/orcl;User Id=qlshx10;Password=Ecoit@123qwe;Pooling=false;";
        using var conn = new OracleConnection(connStr);
        conn.Open();

        // Replicate DebugSqlController's fixed wrap: ":MaxRows" instead of the old invalid ":__maxRows".
        var wrappedSql = "SELECT * FROM (SELECT 1 AS X FROM DUAL) WHERE ROWNUM <= :MaxRows";
        try
        {
            var rows = await conn.QueryAsync(wrappedSql, new { MaxRows = 5 });
            Console.WriteLine("Fixed bind (:MaxRows) WORKS. Rows: " + System.Linq.Enumerable.Count(rows));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fixed bind FAILS: " + ex.Message);
        }

        // Confirm the OLD wrap really does reproduce ORA-00911 (root-cause confirmation).
        var oldWrappedSql = "SELECT * FROM (SELECT 1 AS X FROM DUAL) WHERE ROWNUM <= :__maxRows";
        try
        {
            var rows = await conn.QueryAsync(oldWrappedSql, new { __maxRows = 5 });
            Console.WriteLine("Old bind (:__maxRows) unexpectedly WORKS: " + System.Linq.Enumerable.Count(rows));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Old bind (:__maxRows) FAILS as expected: " + ex.Message);
        }
    }
}
