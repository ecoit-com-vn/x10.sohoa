using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Lưu lịch sử mỗi lần "Chuyển thiết bị" (đổi Trạm/Đường dây quản lý) — cơ chế chuyển hiện tại tạo 1
/// bản ghi EQUIPMENTS mới (Id mới) và đánh dấu bản ghi cũ IsActive=0, không có bảng lịch sử nào lưu lại
/// các lần chuyển trước đó. Gom theo EquipmentCode (không đổi qua các lần chuyển) vì Id đổi mỗi lần
/// chuyển nên không dùng được để truy vết toàn bộ chuỗi lịch sử của 1 thiết bị.
/// </summary>
public class Migration0060_CreateEquipmentTransferHistoryTable : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            cmd.CommandText = @"
                CREATE TABLE EQUIPMENT_TRANSFER_HISTORY (
                    Id                      VARCHAR2(36)   NOT NULL,
                    EquipmentCode           VARCHAR2(100)  NOT NULL,
                    SourceEquipmentId       VARCHAR2(36)   NULL,
                    TargetEquipmentId       VARCHAR2(36)   NOT NULL,
                    SourceInfrastructureId  VARCHAR2(36)   NULL,
                    TargetInfrastructureId  VARCHAR2(36)   NOT NULL,
                    SourceUnitId            NUMBER         NULL,
                    TargetUnitId            NUMBER         NULL,
                    Note                    VARCHAR2(2000) NULL,
                    TransferredBy           VARCHAR2(100)  NULL,
                    TransferredAt           TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
                    CONSTRAINT PK_EQUIP_TRANSFER_HISTORY PRIMARY KEY (Id),
                    CONSTRAINT FK_EQUIP_TRANSFER_HIST_TARGET FOREIGN KEY (TargetEquipmentId)
                        REFERENCES EQUIPMENTS(Id) ON DELETE CASCADE
                )";
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
            {
                // Bảng đã tồn tại.
            }
        }

        using (var cmd = dbCommandFactory())
        {
            cmd.CommandText = "CREATE INDEX IDX_EQUIP_TRANSFER_HIST_CODE ON EQUIPMENT_TRANSFER_HISTORY (EquipmentCode)";
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
            {
                // Index đã tồn tại.
            }
        }

        return string.Empty;
    }
}
