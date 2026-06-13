using Oracle.ManagedDataAccess.Client;

const string connStr = "Data Source=192.168.1.199:1521/orcl;User Id=c##qlshx10;Password=Ecoit@123qwe;Pooling=false;";

Console.WriteLine("=== Migration Fix Runner ===");
using var conn = new OracleConnection(connStr);
conn.Open();
Console.WriteLine("Connected OK.\n");

// Kiểm tra CATALOG có NULL CatalogTypeId không
Console.WriteLine("Catalog rows với CatalogTypeId = NULL:");
PrintQuery(conn, "SELECT ID, CODE, NAME, CATALOGTYPEID FROM CATALOG WHERE CATALOGTYPEID IS NULL");

Console.WriteLine("\nCATALOG_TYPE hiện có:");
PrintQuery(conn, "SELECT ID, CODE, NAME FROM CATALOG_TYPE ORDER BY ID");

var steps = new List<(string Desc, string Sql, int[] IgnoreOra)>
{
    // ── Seed UnitOfMeasure vào CATALOG_TYPE nếu chưa có ──────────────────
    ("Fix-1 Seed CATALOG_TYPE: UnitOfMeasure",
        @"MERGE INTO CATALOG_TYPE t USING DUAL ON (t.Code = 'UnitOfMeasure')
          WHEN MATCHED THEN UPDATE SET t.Name = 'Đơn vị tính', t.HasParent = 0
          WHEN NOT MATCHED THEN INSERT (Id, Code, Name, HasParent, Description)
              VALUES (SEQ_CATALOG_TYPE_ID.NEXTVAL, 'UnitOfMeasure', 'Đơn vị tính', 0, 'Danh mục Đơn vị tính')",
        []),

    ("Fix-1b COMMIT",
        "COMMIT",
        []),

    // ── Điền lại CatalogTypeId cho các row còn NULL ───────────────────────
    ("Fix-2 Điền lại CatalogTypeId cho row NULL",
        @"UPDATE CATALOG c
          SET c.CatalogTypeId = (
              SELECT ct.Id FROM CATALOG_TYPE ct
              WHERE ct.Code = 'UnitOfMeasure'
          )
          WHERE c.CatalogTypeId IS NULL
            AND EXISTS (SELECT 1 FROM CATALOG_TYPE ct WHERE ct.Code = 'UnitOfMeasure')",
        []),

    ("Fix-2b COMMIT",
        "COMMIT",
        []),

    // ── Kiểm tra còn NULL không trước khi đặt NOT NULL ────────────────────
    // (sẽ báo fail nếu vẫn còn NULL, nhờ đó biết có dữ liệu bẩn)

    // ── Đặt NOT NULL cho CatalogTypeId ───────────────────────────────────
    ("Fix-3 Đặt CatalogTypeId NOT NULL",
        "ALTER TABLE CATALOG MODIFY CatalogTypeId NUMBER NOT NULL",
        [1442]),                                   // ORA-01442 = already NOT NULL

    // ── Đảm bảo CATALOG_TYPE.ID là PK (idempotent) ───────────────────────
    ("Fix-4 Đảm bảo pk_catalog_type tồn tại",
        "ALTER TABLE CATALOG_TYPE ADD CONSTRAINT pk_catalog_type PRIMARY KEY (Id)",
        [2260, 2261]),                             // ORA-02260/02261 = already exists
};

Console.WriteLine("\n─── CHẠY FIX ────────────────────────────────────────────");
int ok = 0, skipped = 0, failed = 0;
foreach (var (desc, sql, ignoreOra) in steps)
{
    Console.Write($"  [{ok + skipped + failed + 1:D2}] {desc} ... ");
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var rows = cmd.ExecuteNonQuery();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(rows >= 0 ? $"OK ({rows} rows)" : "OK");
        Console.ResetColor();
        ok++;
    }
    catch (OracleException ex)
    {
        bool ignored = ignoreOra.Any(c => ex.Number == c);
        if (ignored)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"SKIP (ORA-{ex.Number}: {ex.Message.Split('\n')[0].Trim()})");
            Console.ResetColor();
            skipped++;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  *** FAILED ORA-{ex.Number}: {ex.Message.Split('\n')[0]} ***");
            Console.ResetColor();
            failed++;
        }
    }
}

Console.WriteLine($"\n─── KẾT QUẢ: {ok} OK | {skipped} SKIP | {failed} FAIL ───");

Console.WriteLine("\n─── TRẠNG THÁI SAU FIX ──────────────────────────────────");
Console.WriteLine("CATALOG columns:");
PrintColumns(conn, "CATALOG");
Console.WriteLine("\nCATALOG_TYPE data:");
PrintQuery(conn, "SELECT ID, CODE, NAME, HASPARENT FROM CATALOG_TYPE ORDER BY ID");
Console.WriteLine("\nCATALOG data (tất cả rows):");
PrintQuery(conn, "SELECT ID, CODE, NAME, CATALOGTYPEID FROM CATALOG ORDER BY ID");
Console.WriteLine("\nConstraints trên CATALOG:");
PrintQuery(conn, @"SELECT CONSTRAINT_NAME, CONSTRAINT_TYPE, STATUS
                   FROM USER_CONSTRAINTS WHERE TABLE_NAME = 'CATALOG' ORDER BY CONSTRAINT_TYPE");

static void PrintColumns(OracleConnection conn, string table)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $@"SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH, NULLABLE
                         FROM USER_TAB_COLUMNS WHERE TABLE_NAME = '{table.ToUpper()}' ORDER BY COLUMN_ID";
    using var reader = cmd.ExecuteReader();
    Console.WriteLine($"  {"COLUMN",-28} {"TYPE",-18} {"LEN",6}  {"NULL",4}");
    Console.WriteLine("  " + new string('─', 60));
    while (reader.Read())
        Console.WriteLine($"  {reader.GetString(0),-28} {reader.GetString(1),-18} {reader.GetValue(2),6}  {reader.GetString(3),4}");
}

static void PrintQuery(OracleConnection conn, string sql)
{
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)).ToList();
        Console.WriteLine("  " + string.Join(" | ", cols.Select(c => c.PadRight(20))));
        Console.WriteLine("  " + new string('─', cols.Count * 23));
        int rows = 0;
        while (reader.Read() && rows < 30)
        {
            Console.WriteLine("  " + string.Join(" | ", cols.Select((_, i) =>
                (reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "").PadRight(20))));
            rows++;
        }
        if (rows == 0) Console.WriteLine("  (không có dữ liệu)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  (lỗi: {ex.Message.Split('\n')[0].Trim()})");
    }
}
