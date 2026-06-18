using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Redesign DOSSIERS + tạo DOSSIER_SETS, DOSSIER_EQUIPMENTS, DOSSIER_VERSIONS.
///
/// Thay thế Migration0009_RedesignDossiers.sql vốn bị lỗi vô tận vì:
///   Oracle lưu "CREATE TABLE DossierVersions" → tên bảng "DOSSIERVERSIONS"
///   (không có underscore). Lệnh "DROP TABLE DOSSIER_VERSIONS" (có underscore)
///   throw ORA-00942 ngay từ lần chạy đầu → migration không được ghi journal
///   → mỗi khởi động lại thử chạy lại → chu kỳ tạo/xóa bảng vô tận.
///
/// Migration này idempotent:
///   DROP dùng ORA-00942 ignore  (table not found)
///   CREATE dùng ORA-00955 ignore (name already used)
///
/// Thứ tự DROP đảm bảo FK: leaf tables trước (DOSSIER_VERSIONS,
/// DOSSIER_EQUIPMENTS) → DOSSIERS → DOSSIER_SETS.
/// </summary>
public class Migration0009_RedesignDossiers : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void Exec(string sql, params int[] ignoreOra)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                foreach (var code in ignoreOra)
                {
                    if (ex.Message.Contains($"ORA-{code:D5}") ||
                        ex.Message.Contains($"ORA-0{code}") ||
                        ex.Message.Contains($"ORA-{code}"))
                        return;
                }
                throw new Exception($"[Migration0009] SQL:\n{sql}\nError: {ex.Message}", ex);
            }
        }

        // ORA-00942 = table or view does not exist  → safe to ignore on DROP
        // ORA-00955 = name already used             → safe to ignore on CREATE

        // ═══════════════════════════════════════════════════════════════
        // BƯỚC 1 — Xóa dữ liệu seed cũ (nếu bảng gốc còn tồn tại)
        // ═══════════════════════════════════════════════════════════════
        Exec("DELETE FROM Dossiers", 942);

        // ═══════════════════════════════════════════════════════════════
        // BƯỚC 2 — DROP các bảng cũ lẫn mới theo thứ tự FK-safe
        //
        // Cần drop CẢ HAI tên vì:
        //   - 0001_Schema.sql tạo "DossierVersions" → Oracle lưu "DOSSIERVERSIONS"
        //   - Lần partial-run trước có thể đã tạo "DOSSIER_VERSIONS" (underscore)
        // ═══════════════════════════════════════════════════════════════

        // Leaf tables (tham chiếu FK đến DOSSIERS)
        Exec("DROP TABLE DOSSIERVERSIONS",  942);   // tên cũ từ 0001_Schema.sql
        Exec("DROP TABLE DOSSIER_VERSIONS", 942);   // tên mới từ partial-run
        Exec("DROP TABLE DOSSIER_EQUIPMENTS", 942);

        // Parent tables
        Exec("DROP TABLE DOSSIERS", 942);
        Exec("DROP TABLE Dossiers", 942);    // tên cũ camelCase

        Exec("DROP TABLE DOSSIER_SETS", 942);

        // ═══════════════════════════════════════════════════════════════
        // BƯỚC 3 — Tạo lại đúng schema
        // ═══════════════════════════════════════════════════════════════

        Exec(@"CREATE TABLE DOSSIER_SETS (
                   Id           VARCHAR2(36)   NOT NULL PRIMARY KEY,
                   Code         VARCHAR2(100)  NOT NULL UNIQUE,
                   Name         VARCHAR2(255)  NOT NULL,
                   UnitId       NUMBER         NULL,
                   CreatedBy    VARCHAR2(100)  NULL,
                   CreatedDate  TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
                   ModifiedBy   VARCHAR2(100)  NULL,
                   ModifiedDate TIMESTAMP      NULL,
                   IsDeleted    NUMBER(1)      DEFAULT 0 NOT NULL
               )", 955);

        Exec(@"CREATE TABLE DOSSIERS (
                   Id                 VARCHAR2(36)  NOT NULL PRIMARY KEY,
                   GridTypeId         NUMBER        NULL,
                   InfrastructureId   VARCHAR2(36)  NULL,
                   DossierSetId       VARCHAR2(36)  NULL,
                   DossierTypeId      VARCHAR2(36)  NOT NULL,
                   FormDataJson       CLOB          NULL,
                   Status             VARCHAR2(50)  DEFAULT 'Draft' NOT NULL,
                   WorkflowInstanceId VARCHAR2(36)  NULL,
                   WorkflowStatusName VARCHAR2(100) NULL,
                   RowVersion         NUMBER        DEFAULT 1 NOT NULL,
                   CreatorId          VARCHAR2(36)  NULL,
                   CreatorUsername    VARCHAR2(100) NULL,
                   CreatorName        VARCHAR2(255) NULL,
                   CreatedBy          VARCHAR2(100) NULL,
                   CreatedDate        TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
                   ModifiedBy         VARCHAR2(100) NULL,
                   ModifiedDate       TIMESTAMP     NULL,
                   IsDeleted          NUMBER(1)     DEFAULT 0 NOT NULL,
                   CONSTRAINT fk_dossier_set   FOREIGN KEY (DossierSetId)
                       REFERENCES DOSSIER_SETS(Id) ON DELETE SET NULL,
                   CONSTRAINT fk_dossier_type  FOREIGN KEY (DossierTypeId)
                       REFERENCES DOSSIER_TYPES(Id),
                   CONSTRAINT fk_dossier_infra FOREIGN KEY (InfrastructureId)
                       REFERENCES INFRASTRUCTURE(ID) ON DELETE SET NULL
               )", 955);

        Exec(@"CREATE TABLE DOSSIER_EQUIPMENTS (
                   DossierId   VARCHAR2(36) NOT NULL,
                   EquipmentId VARCHAR2(36) NOT NULL,
                   CONSTRAINT pk_dossier_equip PRIMARY KEY (DossierId, EquipmentId),
                   CONSTRAINT fk_de_dossier    FOREIGN KEY (DossierId)
                       REFERENCES DOSSIERS(Id) ON DELETE CASCADE,
                   CONSTRAINT fk_de_equip      FOREIGN KEY (EquipmentId)
                       REFERENCES Equipments(Id) ON DELETE CASCADE
               )", 955);

        Exec(@"CREATE TABLE DOSSIER_VERSIONS (
                   Id            VARCHAR2(36)   NOT NULL PRIMARY KEY,
                   DossierId     VARCHAR2(36)   NOT NULL,
                   VersionNumber NUMBER         NOT NULL,
                   FormDataJson  CLOB           NULL,
                   ChangeNote    VARCHAR2(1000) NULL,
                   CreatedBy     VARCHAR2(100)  NULL,
                   CreatedDate   TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
                   CONSTRAINT fk_dossver_dossier FOREIGN KEY (DossierId)
                       REFERENCES DOSSIERS(Id) ON DELETE CASCADE
               )", 955);

        return string.Empty;
    }
}
