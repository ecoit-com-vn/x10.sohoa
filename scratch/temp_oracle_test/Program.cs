using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

class Program
{
    static async Task Main()
    {
        string host = "192.168.1.199";
        int port = 1521;
        string user = "qlshx10";
        string password = "Ecoit@123qwe";
        string service = "orcl";

        string connStr = $"Data Source={host}:{port}/{service};User Id={user};Password={password};Pooling=false;";
        using var conn = new OracleConnection(connStr);
        conn.Open();

        var path = "/run/media/hataphu/data/sources/x10/sohoa/sohoa.backend/BuildingBlocks/EvnHanoi.Infrastructure/Migrations/Manual/EquipmentService_0067_AddNormalizedSearchColumnsToInfrastructure.sql";
        var text = File.ReadAllText(path);

        // Extract the 2 anonymous PL/SQL blocks (between "DECLARE" and the "END;\n/" terminator),
        // same as sqlplus would execute them, skipping the sqlplus-only "SET SERVEROUTPUT ON" line
        // and comments.
        var blocks = new System.Collections.Generic.List<string>();
        int idx = 0;
        while (true)
        {
            int declareIdx = text.IndexOf("DECLARE", idx, StringComparison.Ordinal);
            if (declareIdx < 0) break;
            int slashIdx = text.IndexOf("\n/\n", declareIdx, StringComparison.Ordinal);
            if (slashIdx < 0) slashIdx = text.IndexOf("\n/", declareIdx, StringComparison.Ordinal);
            var block = text.Substring(declareIdx, slashIdx - declareIdx).TrimEnd();
            blocks.Add(block);
            idx = slashIdx + 2;
        }

        Console.WriteLine($"Found {blocks.Count} anonymous PL/SQL block(s) to test.");

        int blockNum = 1;
        foreach (var block in blocks)
        {
            Console.WriteLine();
            Console.WriteLine($"== Executing block #{blockNum} (fixed syntax) ==");
            try
            {
                using var cmd = new OracleCommand(block, conn) { BindByName = true };
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine($"  Block #{blockNum} EXECUTED SUCCESSFULLY (no PLS-00103 / syntax error).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Block #{blockNum} FAILED: {ex.Message}");
            }
            blockNum++;
        }

        Console.WriteLine();
        Console.WriteLine("== Sanity check: backfill completeness (idempotent, should be fully backfilled already) ==");
        using var checkCmd = new OracleCommand(
            "SELECT COUNT(*) AS TongSo, COUNT(NORMALIZED_CODE) AS DaBackfill FROM INFRASTRUCTURE", conn);
        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            Console.WriteLine($"  TongSo={reader.GetInt32(0)} DaBackfill={reader.GetInt32(1)}");
    }
}
