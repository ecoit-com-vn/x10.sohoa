using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Bảng lưu tài liệu đính kèm đồng bộ từ PMIS (API 8 SUBSTATION_DOCUMENT_LIST / API 9
/// LINE_DOCUMENT_LIST) — mỗi dòng 1 tài liệu, liên kết đa hình tới INFRASTRUCTURE hoặc EQUIPMENTS tuỳ
/// OwnerType (không ràng buộc FK cứng vì OwnerId trỏ tới 1 trong 2 bảng khác nhau tuỳ dòng, theo đúng
/// tinh thần đơn giản hoá đã dùng cho các bảng ánh xạ PMIS trước đây).
///
/// UQ_PMIS_DOCUMENT_CODE (theo MaTaiLieu PMIS) dùng để BỎ QUA tải lại file nếu tài liệu đã đồng bộ
/// trước đó — tránh tải/upload lại MinIO mỗi lần resync 1 thiết bị/trạm không đổi tài liệu.
/// </summary>
public class Migration0055_CreatePmisDocumentTable : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();
        try
        {
            cmd.CommandText = @"
                CREATE TABLE PMIS_DOCUMENT (
                    Id                VARCHAR2(36)   NOT NULL,
                    PmisDocumentCode  VARCHAR2(100)  NOT NULL,
                    OwnerType         VARCHAR2(20)   NOT NULL,
                    OwnerId           VARCHAR2(36)   NOT NULL,
                    DocumentName      NVARCHAR2(500) NULL,
                    DocumentType      NVARCHAR2(200) NULL,
                    ObjectKey         VARCHAR2(1000) NULL,
                    FileSize          NUMBER         NULL,
                    SyncHistoryId     VARCHAR2(36)   NULL,
                    SyncedAt          TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
                    RowVersion        NUMBER         DEFAULT 1 NOT NULL,
                    CreatedBy         VARCHAR2(100)  NULL,
                    CreatedDate       TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
                    ModifiedBy        VARCHAR2(100)  NULL,
                    ModifiedDate      TIMESTAMP      NULL,
                    IsDeleted         NUMBER(1)      DEFAULT 0 NOT NULL,
                    CONSTRAINT PK_PMIS_DOCUMENT PRIMARY KEY (Id),
                    CONSTRAINT UQ_PMIS_DOCUMENT_CODE UNIQUE (PmisDocumentCode),
                    CONSTRAINT CK_PMIS_DOCUMENT_OWNER_TYPE CHECK (OwnerType IN ('INFRASTRUCTURE', 'EQUIPMENT')),
                    CONSTRAINT CK_PMIS_DOCUMENT_DEL CHECK (IsDeleted IN (0, 1))
                )";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Bảng đã tồn tại.
        }

        try
        {
            using var idxCmd = dbCommandFactory();
            idxCmd.CommandText = "CREATE INDEX IDX_PMIS_DOCUMENT_OWNER ON PMIS_DOCUMENT (OwnerType, OwnerId)";
            idxCmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Index đã tồn tại.
        }

        return string.Empty;
    }
}
