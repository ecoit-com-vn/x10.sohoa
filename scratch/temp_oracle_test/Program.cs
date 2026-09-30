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

        Console.WriteLine("== 0) Cleanup any leftover test objects from a previous run ==");
        foreach (var sql in new[]
        {
            "DROP TABLE TEST_SCHEMAVERSIONS_REPAIR",
            "DROP SEQUENCE TEST_SCHEMAVERSIONS_REPAIR_SEQ"
        })
        {
            try { await conn.ExecuteAsync(sql); } catch { /* ignore if not exists */ }
        }

        Console.WriteLine("== 1) Create a table mimicking the broken production SCHEMAVERSIONS (PK, NOT NULL, NO default) ==");
        await conn.ExecuteAsync(@"
            CREATE TABLE TEST_SCHEMAVERSIONS_REPAIR (
                SCHEMAVERSIONID NUMBER NOT NULL,
                SCRIPTNAME VARCHAR2(255) NOT NULL,
                APPLIED TIMESTAMP(6) NOT NULL,
                CONSTRAINT PK_TEST_SCHEMAVERSIONS_REPAIR PRIMARY KEY (SCHEMAVERSIONID)
            )");
        await conn.ExecuteAsync("CREATE SEQUENCE TEST_SCHEMAVERSIONS_REPAIR_SEQ START WITH 1 INCREMENT BY 1");

        Console.WriteLine("== 2) Seed with gappy ids up to 1021, sequence lagging behind at 961 (mirrors real prod state) ==");
        // Insert a handful of rows with explicit ids, ending at 1021, to mimic "186 rows, max id 1021".
        var ids = new[] { 1, 2, 3, 500, 960, 1000, 1021 };
        foreach (var id in ids)
        {
            await conn.ExecuteAsync(
                "INSERT INTO TEST_SCHEMAVERSIONS_REPAIR (SCHEMAVERSIONID, SCRIPTNAME, APPLIED) VALUES (:Id, :Name, SYSTIMESTAMP)",
                new { Id = id, Name = $"Seed_{id}" });
        }
        // Advance the sequence to 961 (lagging behind max id 1021), matching prod's LAST_NUMBER=961.
        await conn.ExecuteAsync("ALTER SEQUENCE TEST_SCHEMAVERSIONS_REPAIR_SEQ INCREMENT BY 960");
        await conn.QuerySingleAsync<int>("SELECT TEST_SCHEMAVERSIONS_REPAIR_SEQ.NEXTVAL FROM DUAL"); // consumes -> now at 961
        await conn.ExecuteAsync("ALTER SEQUENCE TEST_SCHEMAVERSIONS_REPAIR_SEQ INCREMENT BY 1");
        var seqNow = await conn.QuerySingleAsync<int>("SELECT LAST_NUMBER FROM ALL_SEQUENCES WHERE SEQUENCE_NAME = 'TEST_SCHEMAVERSIONS_REPAIR_SEQ'");
        Console.WriteLine($"   Sequence LAST_NUMBER after seeding: {seqNow} (should be 961)");

        Console.WriteLine();
        Console.WriteLine("== 3) Confirm the bug reproduces: naive insert (no id) fails with ORA-01400 ==");
        try
        {
            await conn.ExecuteAsync("INSERT INTO TEST_SCHEMAVERSIONS_REPAIR (SCRIPTNAME, APPLIED) VALUES ('WillFail', SYSTIMESTAMP)");
            Console.WriteLine("   UNEXPECTED: insert succeeded without repair!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("   Reproduced as expected: " + ex.Message.Split('\n')[0]);
        }

        Console.WriteLine();
        Console.WriteLine("== 4) Run the REPAIR logic (same as _REPAIR_FixSchemaVersionsIdentity.sql, targeting the test objects) ==");
        var repairSql = @"
DECLARE
    v_current NUMBER;
    v_max_id  NUMBER;
    v_target  NUMBER;
    v_diff    NUMBER;
BEGIN
    SELECT NVL(MAX(SCHEMAVERSIONID), 0) INTO v_max_id FROM TEST_SCHEMAVERSIONS_REPAIR;
    v_target := v_max_id + 100;

    SELECT TEST_SCHEMAVERSIONS_REPAIR_SEQ.NEXTVAL INTO v_current FROM DUAL;

    IF v_current < v_target THEN
        v_diff := v_target - v_current;
        EXECUTE IMMEDIATE 'ALTER SEQUENCE TEST_SCHEMAVERSIONS_REPAIR_SEQ INCREMENT BY ' || v_diff;
        SELECT TEST_SCHEMAVERSIONS_REPAIR_SEQ.NEXTVAL INTO v_current FROM DUAL;
        EXECUTE IMMEDIATE 'ALTER SEQUENCE TEST_SCHEMAVERSIONS_REPAIR_SEQ INCREMENT BY 1';
        DBMS_OUTPUT.PUT_LINE('Da day sequence toi ' || v_current || ' (max id hien tai: ' || v_max_id || ').');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Sequence da vuot qua, khong can day.');
    END IF;
END;";
        using (var cmd = new OracleCommand(repairSql, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
        await conn.ExecuteAsync("ALTER TABLE TEST_SCHEMAVERSIONS_REPAIR MODIFY (SCHEMAVERSIONID DEFAULT TEST_SCHEMAVERSIONS_REPAIR_SEQ.NEXTVAL)");
        Console.WriteLine("   Repair steps executed without error.");

        Console.WriteLine();
        Console.WriteLine("== 5) Verify: insert without specifying the id now succeeds, and id is > 1021 (no PK collision) ==");
        await conn.ExecuteAsync("INSERT INTO TEST_SCHEMAVERSIONS_REPAIR (SCRIPTNAME, APPLIED) VALUES ('AfterRepair1', SYSTIMESTAMP)");
        await conn.ExecuteAsync("INSERT INTO TEST_SCHEMAVERSIONS_REPAIR (SCRIPTNAME, APPLIED) VALUES ('AfterRepair2', SYSTIMESTAMP)");
        var rows = await conn.QueryAsync("SELECT SCHEMAVERSIONID, SCRIPTNAME FROM TEST_SCHEMAVERSIONS_REPAIR WHERE SCRIPTNAME LIKE 'AfterRepair%' ORDER BY SCHEMAVERSIONID");
        foreach (var r in rows)
            Console.WriteLine($"   Inserted: Id={r.SCHEMAVERSIONID} Name={r.SCRIPTNAME}");

        Console.WriteLine();
        Console.WriteLine("== 6) Sanity: exact DbUp-style insert (SCRIPTNAME, APPLIED) also now works ==");
        await conn.ExecuteAsync("INSERT INTO TEST_SCHEMAVERSIONS_REPAIR (SCRIPTNAME, APPLIED) VALUES (:ScriptName, :Applied)",
            new { ScriptName = "DbUpStyleInsert", Applied = DateTime.UtcNow });
        Console.WriteLine("   OK — DbUp-style 2-column insert succeeded.");

        Console.WriteLine();
        Console.WriteLine("== 7) Cleanup test objects ==");
        await conn.ExecuteAsync("DROP TABLE TEST_SCHEMAVERSIONS_REPAIR");
        await conn.ExecuteAsync("DROP SEQUENCE TEST_SCHEMAVERSIONS_REPAIR_SEQ");
        Console.WriteLine("   Cleaned up.");
    }
}
