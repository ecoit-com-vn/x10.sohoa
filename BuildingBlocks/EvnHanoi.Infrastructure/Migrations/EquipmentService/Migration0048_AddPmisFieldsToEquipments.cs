using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Khóa map thiết bị (EQUIPMENTS) với mã PMIS (maTB) để đồng bộ dữ liệu, và lưu mã QRCode
/// (maQRCode, base64) do PMIS cấp — xem tính năng "Đồng bộ PMIS", module QRCode thiết bị.
/// </summary>
public class Migration0048_AddPmisFieldsToEquipments : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = @"
                ALTER TABLE EQUIPMENTS ADD (
                    PMIS_CODE VARCHAR2(100) NULL,
                    QR_CODE CLOB NULL,
                    LAST_SYNCED_FROM_PMIS_AT TIMESTAMP NULL
                )";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // Cột đã tồn tại.
        }

        try
        {
            cmd.CommandText = "CREATE INDEX IDX_EQUIPMENTS_PMIS_CODE ON EQUIPMENTS (PMIS_CODE)";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Index đã tồn tại.
        }

        return string.Empty;
    }
}
