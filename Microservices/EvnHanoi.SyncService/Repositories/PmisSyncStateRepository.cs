using System.Data;
using System.Text;
using Dapper;

namespace EvnHanoi.SyncService.Repositories;

public class PmisSyncStateRepository : IPmisSyncStateRepository
{
    private const int InChunkSize = 900;    // Oracle: tối đa 1000 phần tử trong 1 danh sách IN
    private const int MergeChunkSize = 200; // số dòng / câu MERGE

    private readonly IDbConnection _connection;

    public PmisSyncStateRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    private const string SelectColumns = @"
        PMIS_CODE AS PmisCode, CONTENT_HASH AS ContentHash, HASH_VERSION AS HashVersion,
        DETAIL_SYNCED AS DetailSynced, LAST_PUSHED_AT AS LastPushedAt, LAST_SCAN_AT AS LastScanAt";

    public async Task<Dictionary<string, PmisSyncStateRow>> GetAsync(string objectType, IReadOnlyCollection<string> codes)
    {
        var result = new Dictionary<string, PmisSyncStateRow>(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0) return result;
        EnsureOpen();

        var sql = $"SELECT {SelectColumns} FROM PMIS_SYNC_STATE WHERE OBJECT_TYPE = :ObjectType AND PMIS_CODE IN :Codes";
        foreach (var chunk in codes.Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.OrdinalIgnoreCase).Chunk(InChunkSize))
        {
            var rows = await _connection.QueryAsync<PmisSyncStateRow>(sql, new { ObjectType = objectType, Codes = chunk });
            foreach (var row in rows) result[row.PmisCode] = row;
        }
        return result;
    }

    public async Task<Dictionary<string, PmisSyncStateRow>> GetAllAsync(string objectType)
    {
        EnsureOpen();
        var rows = await _connection.QueryAsync<PmisSyncStateRow>(
            $"SELECT {SelectColumns} FROM PMIS_SYNC_STATE WHERE OBJECT_TYPE = :ObjectType",
            new { ObjectType = objectType });
        var result = new Dictionary<string, PmisSyncStateRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows) result[row.PmisCode] = row;
        return result;
    }

    public async Task UpsertPushedAsync(string objectType, IReadOnlyCollection<PmisSyncStateUpsert> rows, int hashVersion)
    {
        if (rows.Count == 0) return;
        EnsureOpen();

        // Cùng 1 mã xuất hiện 2 lần trong lô (PMIS lặp bản ghi) sẽ làm MERGE lỗi ORA-30926 — giữ bản cuối.
        var distinct = rows.GroupBy(r => r.PmisCode, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
        var now = DateTime.UtcNow;

        foreach (var chunk in distinct.Chunk(MergeChunkSize))
        {
            // Tên tham số dùng riêng cho từng lần xuất hiện và khai báo đúng thứ tự xuất hiện trong SQL —
            // không phụ thuộc việc ODP.NET bind theo tên hay theo vị trí.
            var p = new DynamicParameters();
            var sql = new StringBuilder("MERGE INTO PMIS_SYNC_STATE t USING (");
            for (var i = 0; i < chunk.Length; i++)
            {
                if (i > 0) sql.Append(" UNION ALL ");
                sql.Append($"SELECT :c{i} AS PMIS_CODE, :h{i} AS CONTENT_HASH, :d{i} AS DETAIL_SYNCED FROM DUAL");
                p.Add($"c{i}", chunk[i].PmisCode);
                p.Add($"h{i}", chunk[i].ContentHash);
                p.Add($"d{i}", chunk[i].DetailSynced ? 1 : 0);
            }
            sql.Append(") s ON (t.OBJECT_TYPE = :ot1 AND t.PMIS_CODE = s.PMIS_CODE) ");
            p.Add("ot1", objectType);
            sql.Append("WHEN MATCHED THEN UPDATE SET t.CONTENT_HASH = s.CONTENT_HASH, t.HASH_VERSION = :hv1, ");
            p.Add("hv1", hashVersion);
            sql.Append("t.DETAIL_SYNCED = s.DETAIL_SYNCED, t.LAST_PUSHED_AT = :pu1, t.LAST_SEEN_AT = :se1 ");
            p.Add("pu1", now);
            p.Add("se1", now);
            sql.Append("WHEN NOT MATCHED THEN INSERT (OBJECT_TYPE, PMIS_CODE, CONTENT_HASH, HASH_VERSION, DETAIL_SYNCED, LAST_PUSHED_AT, LAST_SEEN_AT) ");
            sql.Append("VALUES (:ot2, s.PMIS_CODE, s.CONTENT_HASH, :hv2, s.DETAIL_SYNCED, :pu2, :se2)");
            p.Add("ot2", objectType);
            p.Add("hv2", hashVersion);
            p.Add("pu2", now);
            p.Add("se2", now);

            await _connection.ExecuteAsync(sql.ToString(), p);
        }
    }

    public async Task TouchSeenAsync(string objectType, IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0) return;
        EnsureOpen();
        const string sql = "UPDATE PMIS_SYNC_STATE SET LAST_SEEN_AT = :Now WHERE OBJECT_TYPE = :ObjectType AND PMIS_CODE IN :Codes";
        var now = DateTime.UtcNow;
        foreach (var chunk in codes.Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.OrdinalIgnoreCase).Chunk(InChunkSize))
        {
            await _connection.ExecuteAsync(sql, new { Now = now, ObjectType = objectType, Codes = chunk });
        }
    }

    public Task MarkParentScannedAsync(string parentPmisCode) =>
        MergeTimestampAsync("PARENT_SCAN", parentPmisCode, scanColumn: true);

    public async Task<DateTime?> GetSweepAtAsync(string objectType)
    {
        EnsureOpen();
        return await _connection.QuerySingleOrDefaultAsync<DateTime?>(
            "SELECT LAST_PUSHED_AT FROM PMIS_SYNC_STATE WHERE OBJECT_TYPE = 'SWEEP' AND PMIS_CODE = :ObjectType",
            new { ObjectType = objectType });
    }

    public Task SetSweepAtAsync(string objectType) =>
        MergeTimestampAsync("SWEEP", objectType, scanColumn: false);

    /// <summary>MERGE 1 dòng đặc biệt (SWEEP/PARENT_SCAN): đặt LAST_SCAN_AT (quét cha) hoặc LAST_PUSHED_AT (quét đầy đủ).</summary>
    private async Task MergeTimestampAsync(string objectType, string code, bool scanColumn)
    {
        EnsureOpen();
        var column = scanColumn ? "LAST_SCAN_AT" : "LAST_PUSHED_AT";
        var sql = $@"
            MERGE INTO PMIS_SYNC_STATE t
            USING (SELECT :Code1 AS PMIS_CODE FROM DUAL) s
            ON (t.OBJECT_TYPE = :ObjectType1 AND t.PMIS_CODE = s.PMIS_CODE)
            WHEN MATCHED THEN UPDATE SET t.{column} = :Now1, t.LAST_SEEN_AT = :Now2
            WHEN NOT MATCHED THEN INSERT (OBJECT_TYPE, PMIS_CODE, {column}, LAST_SEEN_AT)
            VALUES (:ObjectType2, s.PMIS_CODE, :Now3, :Now4)";
        var now = DateTime.UtcNow;
        await _connection.ExecuteAsync(sql, new
        {
            Code1 = code, ObjectType1 = objectType, Now1 = now, Now2 = now,
            ObjectType2 = objectType, Now3 = now, Now4 = now
        });
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
