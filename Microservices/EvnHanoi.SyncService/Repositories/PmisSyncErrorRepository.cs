using System.Data;
using Dapper;
using EvnHanoi.Infrastructure.Pmis;
using EvnHanoi.SyncService.Services;

namespace EvnHanoi.SyncService.Repositories;

public class PmisSyncErrorRepository : IPmisSyncErrorRepository
{
    private const int InChunkSize = 900; // Oracle: tối đa 1000 phần tử trong 1 danh sách IN

    private readonly IDbConnection _connection;

    public PmisSyncErrorRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    private sealed class UnresolvedRow
    {
        public string PmisCode { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public DateTime? NextRetryAt { get; set; }
        public string? SourceHash { get; set; }
    }

    public async Task<Dictionary<string, SyncErrorState>> GetUnresolvedAsync(string kind, IReadOnlyCollection<string> codes)
    {
        var result = new Dictionary<string, SyncErrorState>(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0) return result;
        EnsureOpen();

        const string sql = @"
            SELECT PMIS_CODE AS PmisCode, ERROR_CODE AS ErrorCode, STATE AS State, NEXT_RETRY_AT AS NextRetryAt, SOURCE_HASH AS SourceHash
            FROM PMIS_SYNC_ERROR
            WHERE ENTITY_KIND = :Kind AND PMIS_CODE IN :Codes AND STATE <> 'RESOLVED'";
        foreach (var chunk in codes.Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.OrdinalIgnoreCase).Chunk(InChunkSize))
        {
            foreach (var row in await _connection.QueryAsync<UnresolvedRow>(sql, new { Kind = kind, Codes = chunk }))
            {
                var state = new SyncErrorState(row.ErrorCode, row.State, row.NextRetryAt, row.SourceHash);
                // 1 mã có thể có nhiều dòng lỗi (khác ERROR_CODE): ưu tiên dòng đang bị cách ly.
                if (!result.TryGetValue(row.PmisCode, out var existing) || (state.State == SyncErrorStates.Quarantined && existing.State != SyncErrorStates.Quarantined))
                    result[row.PmisCode] = state;
            }
        }
        return result;
    }

    public async Task RecordFailuresAsync(IReadOnlyCollection<SyncErrorRecord> failures, PmisSyncErrorOptions options)
    {
        if (failures.Count == 0) return;
        EnsureOpen();
        var now = DateTime.UtcNow;

        // Tham số đặt tên riêng theo từng lần xuất hiện, đúng thứ tự trong SQL — không phụ thuộc ODP.NET bind theo tên hay vị trí.
        const string mergeSql = @"
            MERGE INTO PMIS_SYNC_ERROR t
            USING (SELECT :Kind1 AS K, :Code1 AS C, :Err1 AS E FROM DUAL) s
            ON (t.ENTITY_KIND = s.K AND t.PMIS_CODE = s.C AND t.ERROR_CODE = s.E)
            WHEN MATCHED THEN UPDATE SET
                t.MESSAGE = :Msg1, t.LAST_SEEN_AT = :Now1, t.SOURCE_HASH = :Hash1, t.SYNC_HISTORY_ID = :Hist1,
                t.OCCURRENCES = CASE WHEN t.STATE = 'RESOLVED' THEN 1 ELSE t.OCCURRENCES + 1 END,
                t.FIRST_SEEN_AT = CASE WHEN t.STATE = 'RESOLVED' THEN :Now2 ELSE t.FIRST_SEEN_AT END,
                t.STATE = CASE WHEN t.STATE = 'RESOLVED' THEN 'OPEN' ELSE t.STATE END,
                t.RESOLVED_AT = NULL
            WHEN NOT MATCHED THEN INSERT
                (ID, ENTITY_KIND, PMIS_CODE, ERROR_CODE, MESSAGE, STATE, OCCURRENCES, FIRST_SEEN_AT, LAST_SEEN_AT, SOURCE_HASH, SYNC_HISTORY_ID)
                VALUES (:Id1, s.K, s.C, s.E, :Msg2, 'OPEN', 1, :Now3, :Now4, :Hash2, :Hist2)";

        // Lỗi do dữ liệu nguồn đủ số lần → cách ly; mỗi lần lỗi lại sau hạn thử lại thì thời gian cách ly nhân đôi (tối đa QuarantineMaxMinutes).
        const string quarantineSql = @"
            UPDATE PMIS_SYNC_ERROR
            SET STATE = 'QUARANTINED',
                NEXT_RETRY_AT = :Now5 + NUMTODSINTERVAL(LEAST(:BaseMin * POWER(2, LEAST(GREATEST(OCCURRENCES - :K1, 0), 6)), :MaxMin), 'MINUTE')
            WHERE ENTITY_KIND = :Kind2 AND PMIS_CODE = :Code2 AND ERROR_CODE = :Err2 AND OCCURRENCES >= :K2";

        foreach (var f in failures)
        {
            var errorCode = PmisSyncErrorCodes.Normalize(f.ErrorCode);
            var message = f.Message is { Length: > 1000 } ? f.Message[..1000] : f.Message;
            await _connection.ExecuteAsync(mergeSql, new
            {
                Kind1 = f.Kind, Code1 = f.PmisCode, Err1 = errorCode,
                Msg1 = message, Now1 = now, Hash1 = f.SourceHash, Hist1 = f.SyncHistoryId, Now2 = now,
                Id1 = Guid.NewGuid().ToString(), Msg2 = message, Now3 = now, Now4 = now, Hash2 = f.SourceHash, Hist2 = f.SyncHistoryId
            });

            if (PmisSyncErrorCodes.IsQuarantinable(errorCode))
            {
                await _connection.ExecuteAsync(quarantineSql, new
                {
                    Now5 = now, BaseMin = options.QuarantineBaseMinutes, K1 = options.QuarantineAfter, MaxMin = options.QuarantineMaxMinutes,
                    Kind2 = f.Kind, Code2 = f.PmisCode, Err2 = errorCode, K2 = options.QuarantineAfter
                });
            }
        }
    }

    public async Task ResolveAsync(string kind, IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0) return;
        EnsureOpen();
        const string sql = @"
            UPDATE PMIS_SYNC_ERROR
            SET STATE = 'RESOLVED', RESOLVED_AT = :Now, NEXT_RETRY_AT = NULL
            WHERE ENTITY_KIND = :Kind AND PMIS_CODE IN :Codes AND STATE <> 'RESOLVED'";
        var now = DateTime.UtcNow;
        foreach (var chunk in codes.Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.OrdinalIgnoreCase).Chunk(InChunkSize))
            await _connection.ExecuteAsync(sql, new { Now = now, Kind = kind, Codes = chunk });
    }

    public async Task<List<SyncErrorSummaryRow>> GetSummaryAsync()
    {
        EnsureOpen();
        return (await _connection.QueryAsync<SyncErrorSummaryRow>(@"
            SELECT ENTITY_KIND AS EntityKind, ERROR_CODE AS ErrorCode, STATE AS State, COUNT(*) AS Count
            FROM PMIS_SYNC_ERROR
            GROUP BY ENTITY_KIND, ERROR_CODE, STATE
            ORDER BY COUNT(*) DESC")).ToList();
    }

    public async Task<(List<SyncErrorRow> Items, int Total)> ListAsync(string? kind, string? errorCode, string? state, string? keyword, int page, int pageSize)
    {
        EnsureOpen();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var where = "WHERE 1 = 1";
        var p = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(kind)) { where += " AND ENTITY_KIND = :Kind"; p.Add("Kind", kind.Trim().ToUpperInvariant()); }
        if (!string.IsNullOrWhiteSpace(errorCode)) { where += " AND ERROR_CODE = :ErrorCode"; p.Add("ErrorCode", errorCode.Trim().ToUpperInvariant()); }
        if (!string.IsNullOrWhiteSpace(state)) { where += " AND STATE = :State"; p.Add("State", state.Trim().ToUpperInvariant()); }
        if (!string.IsNullOrWhiteSpace(keyword)) { where += " AND UPPER(PMIS_CODE) LIKE :Keyword"; p.Add("Keyword", "%" + keyword.Trim().ToUpperInvariant() + "%"); }

        var total = await _connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM PMIS_SYNC_ERROR {where}", p);
        p.Add("Skip", (page - 1) * pageSize);
        p.Add("Take", pageSize);
        var items = await _connection.QueryAsync<SyncErrorRow>($@"
            SELECT ID AS Id, ENTITY_KIND AS EntityKind, PMIS_CODE AS PmisCode, ERROR_CODE AS ErrorCode, MESSAGE AS Message, STATE AS State,
                   OCCURRENCES AS Occurrences, FIRST_SEEN_AT AS FirstSeenAt, LAST_SEEN_AT AS LastSeenAt, NEXT_RETRY_AT AS NextRetryAt
            FROM PMIS_SYNC_ERROR {where}
            ORDER BY LAST_SEEN_AT DESC, ID
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY", p);
        return (items.ToList(), total);
    }

    public async Task<int> RetryAsync(string? kind, string? errorCode, string? pmisCode)
    {
        EnsureOpen();
        var where = "WHERE STATE = 'QUARANTINED'";
        var p = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(kind)) { where += " AND ENTITY_KIND = :Kind"; p.Add("Kind", kind.Trim().ToUpperInvariant()); }
        if (!string.IsNullOrWhiteSpace(errorCode)) { where += " AND ERROR_CODE = :ErrorCode"; p.Add("ErrorCode", errorCode.Trim().ToUpperInvariant()); }
        if (!string.IsNullOrWhiteSpace(pmisCode)) { where += " AND PMIS_CODE = :PmisCode"; p.Add("PmisCode", pmisCode.Trim()); }
        return await _connection.ExecuteAsync($"UPDATE PMIS_SYNC_ERROR SET STATE = 'OPEN', NEXT_RETRY_AT = NULL {where}", p);
    }
}
