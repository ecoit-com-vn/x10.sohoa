using System;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Dapper;

class Program
{
    static async Task Main()
    {
        using var conn = new OracleConnection("Data Source=192.168.1.199:1521/orcl;User Id=qlshx10;Password=Ecoit@123qwe;Pooling=false;Connection Timeout=20;");
        conn.Open();
        foreach (var r in await conn.QueryAsync("SELECT TABLE_NAME, COLUMN_NAME, CHAR_LENGTH, CHAR_USED, NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='QLSHX10' AND ((TABLE_NAME='INFRASTRUCTURE' AND COLUMN_NAME IN ('NAME','NORMALIZED_NAME')) OR (TABLE_NAME='EQUIPMENTS' AND COLUMN_NAME='NAME')) ORDER BY 1,2"))
            Console.WriteLine($"{r.TABLE_NAME}.{r.COLUMN_NAME} len={r.CHAR_LENGTH} used={r.CHAR_USED} nullable={r.NULLABLE}");
        // thử thật: chèn tên đúng cỡ ca lỗi (265 byte) rồi rollback
        using var tx = conn.BeginTransaction();
        var name = new string('Đ', 300);
        var n = await conn.ExecuteAsync("UPDATE INFRASTRUCTURE SET NAME = :N WHERE ROWNUM = 1", new { N = name }, tx);
        Console.WriteLine($"update tên 300 ký tự (~600 byte): {n} dòng OK");
        tx.Rollback();
    }
}
