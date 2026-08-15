using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Bản "thông số kỹ thuật" (thongSoKyThuat) đồng bộ riêng từ PMIS cho từng thiết bị — KHÔNG ghi đè
/// EQUIPMENTS.FORM_VALUES (dữ liệu người dùng chỉnh sửa nội bộ). 1 dòng/thiết bị, ghi đè mỗi lần
/// đồng bộ vì đây là "bản sao mới nhất từ PMIS". Dùng để tính năng so sánh sai khác trên màn chi
/// tiết thiết bị đối chiếu với EQUIPMENTS.FORM_VALUES theo cùng FormSchema.
/// </summary>
public class Migration0049_CreateEquipmentPmisSpecTable : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = @"
                CREATE TABLE EQUIPMENT_PMIS_SPEC (
                    Id                     VARCHAR2(36)  NOT NULL,
                    EquipmentId            VARCHAR2(36)  NOT NULL,
                    FormTemplateVersionId  VARCHAR2(36)  NULL,
                    FormValues             CLOB          NULL,
                    SyncedAt               TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
                    SyncHistoryId          VARCHAR2(36)  NULL,
                    RowVersion             NUMBER        DEFAULT 1 NOT NULL,
                    CreatedBy              VARCHAR2(100) NULL,
                    CreatedDate            TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
                    ModifiedBy             VARCHAR2(100) NULL,
                    ModifiedDate           TIMESTAMP     NULL,
                    IsDeleted              NUMBER(1)     DEFAULT 0 NOT NULL,
                    CONSTRAINT PK_EQUIPMENT_PMIS_SPEC PRIMARY KEY (Id),
                    CONSTRAINT UQ_EQUIPMENT_PMIS_SPEC_EQUIP UNIQUE (EquipmentId),
                    CONSTRAINT FK_EQUIPMENT_PMIS_SPEC_EQUIP FOREIGN KEY (EquipmentId)
                        REFERENCES EQUIPMENTS(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_EQUIPMENT_PMIS_SPEC_FORMVER FOREIGN KEY (FormTemplateVersionId)
                        REFERENCES EavFormTemplateVersions(Id),
                    CONSTRAINT CK_EQUIPMENT_PMIS_SPEC_DELETED CHECK (IsDeleted IN (0, 1))
                )";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Bảng đã tồn tại.
        }

        return string.Empty;
    }
}
