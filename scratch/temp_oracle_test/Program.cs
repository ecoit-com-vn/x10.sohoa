using System;
using System.Linq;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Dapper;

class Program
{
    static async Task Main()
    {
        using var conn = new OracleConnection("Data Source=192.168.1.199:1521/orcl;User Id=qlshx10;Password=Ecoit@123qwe;Pooling=false;Connection Timeout=20;");
        conn.Open();
        var some = (await conn.QueryAsync<string>("SELECT PmisDocumentCode FROM PMIS_DOCUMENT WHERE ROWNUM <= 3")).ToList();
        Console.WriteLine("codes: " + string.Join(",", some));
        var rows = await conn.QueryAsync(@"SELECT PmisDocumentCode, FILE_STATUS AS FileStatus, FILE_ATTEMPTS AS FileAttempts, FILE_LAST_ERROR AS FileLastError,
            CASE WHEN ObjectKey IS NOT NULL THEN 1 ELSE 0 END AS HasFile FROM PMIS_DOCUMENT WHERE IsDeleted = 0 AND PmisDocumentCode IN :Codes", new { Codes = some });
        foreach (var r in rows) Console.WriteLine(string.Join(" | ", ((System.Collections.Generic.IDictionary<string, object>)r).Select(kv => kv.Key + "=" + kv.Value)));
    }
}
