using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Bổ sung API ảnh QR (DEVICE_QR_IMAGE) vào PMIS_API_ENDPOINT_CONFIG — API thứ 10, phát hiện thêm khi
/// gọi thật vào gateway PMIS (không nằm trong 9 API gốc của tài liệu docx), xem
/// BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md. Phải nới CHECK constraint cũ (chỉ cho phép đúng 9 mã gốc) mới
/// insert được mã mới.
/// </summary>
public class Migration0005_AddDeviceQrImageApiEndpoint : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory, "ALTER TABLE PMIS_API_ENDPOINT_CONFIG DROP CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE", "ORA-02443");

        Execute(dbCommandFactory, @"
            ALTER TABLE PMIS_API_ENDPOINT_CONFIG ADD CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE CHECK (API_CODE IN (
                'SUBSTATION_LIST', 'LINE_LIST', 'SUBSTATION_DEVICE_TYPE_LIST', 'SUBSTATION_DEVICE_LIST',
                'LINE_DEVICE_TYPE_LIST', 'LINE_DEVICE_LIST', 'DEVICE_DETAIL',
                'SUBSTATION_DOCUMENT_LIST', 'LINE_DOCUMENT_LIST', 'DEVICE_QR_IMAGE'
            ))", "ORA-02264");

        using var command = dbCommandFactory();
        command.CommandText = @"
            INSERT INTO PMIS_API_ENDPOINT_CONFIG (ID, API_CODE, DISPLAY_NAME, IS_ACTIVE)
            VALUES (:Id, 'DEVICE_QR_IMAGE', 'API ảnh QR thiết bị', 0)";
        AddParameter(command, "Id", Guid.CreateVersion7().ToString());

        try
        {
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
        {
            // Đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }

    private static void Execute(Func<IDbCommand> dbCommandFactory, string sql, string ignoreOraCode)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains(ignoreOraCode, StringComparison.OrdinalIgnoreCase))
        {
            // Constraint đã ở đúng trạng thái mong muốn (chạy lại migration thủ công) — bỏ qua.
        }
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
