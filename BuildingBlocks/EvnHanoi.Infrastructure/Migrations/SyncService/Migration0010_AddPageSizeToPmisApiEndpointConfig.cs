using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Bổ sung cột PAGE_SIZE vào PMIS_API_ENDPOINT_CONFIG — số bản ghi mỗi trang ("take") khi phân trang gọi
/// từng API PMIS, admin tự cấu hình qua "Cấu hình kết nối API" thay vì hard-code rải rác trong code
/// (1000 ở PmisScheduledSyncJob/PmisSyncExecutionService, 100 trong các DTO tra cứu tương tác) — xem
/// PmisEndpointConfigProvider.GetEndpointAsync (áp PmisPaging.DefaultPageSize=100 khi null).
/// </summary>
public class Migration0010_AddPageSizeToPmisApiEndpointConfig : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = "ALTER TABLE PMIS_API_ENDPOINT_CONFIG ADD PAGE_SIZE NUMBER NULL";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // Cột đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }
}
