using System;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Dapper;

class Program
{
    static async Task Main()
    {
        string connStr = "Data Source=192.168.1.199:1521/orcl;User Id=qlshx10;Password=Ecoit@123qwe;Pooling=false;Connection Timeout=20;";
        using var conn = new OracleConnection(connStr);
        conn.Open();

        Console.WriteLine("restored_by_0073 = " + await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM EQUIPMENTS WHERE MODIFIEDBY='MIGRATION_0073_RESTORE_BORN_GHOST'"));
        Console.WriteLine("ghost_left       = " + await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM EQUIPMENTS WHERE STATUSTRANSITION=0 AND ISDELETED=0"));
        Console.WriteLine("dup_infra_code   = " + await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM (SELECT 1 FROM EQUIPMENTS WHERE ISDELETED=0 AND STATUSTRANSITION IS NULL AND INFRASTRUCTURE_ID IS NOT NULL GROUP BY INFRASTRUCTURE_ID, CODE HAVING COUNT(*)>1)"));
        Console.WriteLine("dup_pmis_code    = " + await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM (SELECT 1 FROM EQUIPMENTS WHERE ISDELETED=0 AND STATUSTRANSITION IS NULL AND PMIS_CODE IS NOT NULL GROUP BY UPPER(TRIM(PMIS_CODE)) HAVING COUNT(*)>1)"));
        foreach (var r in await conn.QueryAsync<string>("SELECT SCRIPTNAME FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0072%' OR SCRIPTNAME LIKE '%0073%' ORDER BY 1"))
            Console.WriteLine("journal: " + r.Replace("EvnHanoi.Infrastructure.Migrations.EquipmentService.", ""));
    }
}
