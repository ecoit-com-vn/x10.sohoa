using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Seed 3 dòng SYNC_CONFIG (SUBSTATION/TRANSMISSION_LINE/EQUIPMENT) — bảng đã có sẵn từ
/// Migration0001 nhưng chưa từng được seed dữ liệu, khiến SYNC_HISTORY.SYNC_CONFIG_ID (NOT NULL)
/// không có gì để tham chiếu khi chạy đồng bộ thủ công/tự động. Mặc định tắt (IsEnabled=0),
/// tần suất 60 phút — admin tự bật + chỉnh qua màn "Lịch đồng bộ PMIS" (Giai đoạn 2).
/// </summary>
public class Migration0004_SeedSyncConfigRows : IScript
{
    private static readonly string[] ObjectTypes = ["SUBSTATION", "TRANSMISSION_LINE", "EQUIPMENT"];

    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        foreach (var objectType in ObjectTypes)
        {
            using var command = dbCommandFactory();
            command.CommandText = @"
                INSERT INTO SYNC_CONFIG (ID, OBJECT_TYPE, FREQUENCY_VALUE, FREQUENCY_UNIT, IS_ENABLED)
                VALUES (:Id, :ObjectType, 60, 'MINUTE', 0)";

            AddParameter(command, "Id", Guid.CreateVersion7().ToString());
            AddParameter(command, "ObjectType", objectType);

            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
            {
                // OBJECT_TYPE đã tồn tại (chạy lại migration thủ công) — bỏ qua.
            }
        }

        return string.Empty;
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
