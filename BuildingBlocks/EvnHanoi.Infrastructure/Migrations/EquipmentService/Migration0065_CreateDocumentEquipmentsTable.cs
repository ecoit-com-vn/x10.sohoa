using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Liên kết Tài liệu &lt;-&gt; Thiết bị (nhiều-nhiều) — thay cho liên kết Hồ sơ &lt;-&gt; Thiết bị cũ
/// (DOSSIER_EQUIPMENTS), vì giờ thiết bị được phân loại ở cấp Tài liệu đính kèm, không phải ở cấp Hồ sơ.
/// </summary>
public class Migration0065_CreateDocumentEquipmentsTable : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            cmd.CommandText = @"
                CREATE TABLE DOCUMENT_EQUIPMENTS (
                    DocumentId  VARCHAR2(36) NOT NULL,
                    EquipmentId VARCHAR2(36) NOT NULL,
                    CONSTRAINT PK_DOCUMENT_EQUIPMENTS PRIMARY KEY (DocumentId, EquipmentId),
                    CONSTRAINT FK_DOC_EQUIP_DOCUMENT FOREIGN KEY (DocumentId) REFERENCES DOCUMENTS(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_DOC_EQUIP_EQUIPMENT FOREIGN KEY (EquipmentId) REFERENCES Equipments(Id) ON DELETE CASCADE
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
            cmd.CommandText = "CREATE INDEX IDX_DOC_EQUIP_EQUIPMENT ON DOCUMENT_EQUIPMENTS (EquipmentId)";
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
