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

        var anyInfraId = await conn.QuerySingleAsync<string>("SELECT ID FROM INFRASTRUCTURE WHERE ROWNUM <= 1");
        var staleDoneId = Guid.NewGuid().ToString();
        var staleCode = "TEST_WATCHDOG_STALE_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        Console.WriteLine("== Mo phong dung bug da sua: 1 dong DONE that su cu (SyncedAt 10 ngay truoc), ==");
        Console.WriteLine("== nhung ModifiedDate vua bi UpdateOwnerAsync bump len 'vua xong' (gia lap) ==");
        await conn.ExecuteAsync(@"
            INSERT INTO PMIS_DOCUMENT (ID, PmisDocumentCode, OwnerType, OwnerId, DocumentName, ObjectKey, FileSize,
                FILE_STATUS, SyncedAt, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, IsDeleted)
            VALUES (:Id, :Code, 'INFRASTRUCTURE', :OwnerId, 'Test stale-done doc', 'fake/objectkey.pdf', 1000,
                'DONE', SYSTIMESTAMP - INTERVAL '10' DAY, 'TEST', SYSTIMESTAMP - INTERVAL '10' DAY, 'PMIS_SYNC', SYSTIMESTAMP, 0)",
            new { Id = staleDoneId, Code = staleCode, OwnerId = anyInfraId });

        // Cung tao 1 dong dang PENDING de watchdog co hang doi (PendingCount > 0), gia lap job da "chet" that su.
        var pendingId = Guid.NewGuid().ToString();
        var pendingCode = "TEST_WATCHDOG_PENDING_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        await conn.ExecuteAsync(@"
            INSERT INTO PMIS_DOCUMENT (ID, PmisDocumentCode, OwnerType, OwnerId, DocumentName, FILE_URL, FILE_STATUS, CreatedBy, CreatedDate, IsDeleted)
            VALUES (:Id, :Code, 'INFRASTRUCTURE', :OwnerId, 'Test pending doc', 'http://example.com/test.pdf', 'PENDING', 'TEST', SYSTIMESTAMP, 0)",
            new { Id = pendingId, Code = pendingCode, OwnerId = anyInfraId });

        Console.WriteLine("== Goi GET internal/v1/documents/pending-summary (sau khi sua dung SyncedAt) ==");
        using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://localhost:5254") };
        http.DefaultRequestHeaders.Add("X-Internal-Token", "evnhanoi-internal-sync-token");
        var resp = await http.GetAsync("internal/v1/documents/pending-summary");
        var body = await resp.Content.ReadAsStringAsync();
        Console.WriteLine("  HTTP " + (int)resp.StatusCode + ": " + body);
        Console.WriteLine();
        Console.WriteLine("  KY VONG: lastDownloadedAt phai la moc 10 NGAY TRUOC (SyncedAt that), KHONG phai 'vua xong'");
        Console.WriteLine("  (neu con dung ModifiedDate se thay moc gan day do UpdateOwnerAsync-style bump).");

        Console.WriteLine();
        Console.WriteLine("== Don dep ==");
        await conn.ExecuteAsync("DELETE FROM PMIS_DOCUMENT WHERE ID IN (:Id1, :Id2)", new { Id1 = staleDoneId, Id2 = pendingId });
        Console.WriteLine("  Da don sach.");
    }
}
