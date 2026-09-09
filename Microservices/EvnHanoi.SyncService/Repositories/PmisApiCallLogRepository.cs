using System.Data;
using Dapper;
using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public class PmisApiCallLogRepository : IPmisApiCallLogRepository
{
    private readonly IDbConnection _connection;

    public PmisApiCallLogRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task InsertAsync(PmisApiCallLog log)
    {
        EnsureOpen();
        var id = string.IsNullOrWhiteSpace(log.Id) ? Guid.CreateVersion7().ToString() : log.Id;

        // RECORD_COUNT (có gạch dưới) — khác quy ước không gạch dưới của các cột còn lại trong bảng này,
        // vì cột được thêm sau bằng ALTER TABLE (Migration0009) lỡ đặt tên khác — không đổi lại tên cột
        // thật (đã chạy thật trên môi trường), chỉ khớp đúng tên trong code.
        const string sql = @"
            INSERT INTO PMIS_API_CALL_LOG (
                Id, ApiCode, HttpMethod, Url, RequestPayload, StatusCode, IsSuccess, ErrorMessage,
                DurationMs, HttpClientName, RECORD_COUNT
            ) VALUES (
                :Id, :ApiCode, :HttpMethod, :Url, :RequestPayload, :StatusCode, :IsSuccess, :ErrorMessage,
                :DurationMs, :HttpClientName, :RecordCount
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            log.ApiCode,
            log.HttpMethod,
            log.Url,
            log.RequestPayload,
            log.StatusCode,
            IsSuccess = log.IsSuccess ? 1 : 0,
            log.ErrorMessage,
            log.DurationMs,
            log.HttpClientName,
            log.RecordCount
        });
    }

    public async Task<(IEnumerable<PmisApiCallLog> Items, int TotalCount)> GetPagedAsync(string apiCode, int page, int pageSize)
    {
        EnsureOpen();
        var parameters = new DynamicParameters();
        parameters.Add("ApiCode", apiCode);
        parameters.Add("Skip", (page - 1) * pageSize);
        parameters.Add("Take", pageSize);

        var totalCount = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM PMIS_API_CALL_LOG WHERE ApiCode = :ApiCode", parameters);

        var items = await _connection.QueryAsync<PmisApiCallLog>(@"
            SELECT Id, ApiCode, HttpMethod, Url, RequestPayload, StatusCode, IsSuccess, ErrorMessage,
                   DurationMs, HttpClientName, CalledAt, RECORD_COUNT AS RecordCount
            FROM PMIS_API_CALL_LOG
            WHERE ApiCode = :ApiCode
            ORDER BY CalledAt DESC
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY", parameters);

        return (items, totalCount);
    }

    public async Task<int> DeleteOlderThanAsync(int retentionDays)
    {
        EnsureOpen();
        // Để Oracle tự tính mốc cắt bằng SYSTIMESTAMP thay vì tính DateTime.UtcNow bên C# rồi so với
        // CalledAt (cột ghi bằng SYSTIMESTAMP — giờ server/session, không chắc chắn là UTC) — tránh lệch
        // múi giờ có thể xoá nhầm dòng còn trong hạn giữ hoặc giữ lại dòng đã quá hạn.
        return await _connection.ExecuteAsync(
            "DELETE FROM PMIS_API_CALL_LOG WHERE CalledAt < SYSTIMESTAMP - :RetentionDays",
            new { RetentionDays = retentionDays });
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
