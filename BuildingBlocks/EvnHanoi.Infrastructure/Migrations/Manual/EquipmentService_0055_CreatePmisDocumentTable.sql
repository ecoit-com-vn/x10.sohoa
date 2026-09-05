-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0055_CreatePmisDocumentTable.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0055_CreatePmisDocumentTable.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0055_CreatePmisDocumentTable.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: bảng PMIS_DOCUMENT — lưu tài liệu đính kèm đồng bộ từ PMIS (API 8/9). OwnerType/OwnerId liên
-- kết đa hình tới INFRASTRUCTURE hoặc EQUIPMENTS, không ràng buộc FK cứng. UQ_PMIS_DOCUMENT_CODE
-- (theo MaTaiLieu PMIS) dùng để bỏ qua tải lại file khi resync không đổi tài liệu.
--
-- ROLLBACK thủ công:
--   DROP TABLE PMIS_DOCUMENT;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0055_CreatePmisDocumentTable%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tables WHERE table_name = 'PMIS_DOCUMENT';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
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
                CONSTRAINT CK_PMIS_DOCUMENT_OWNER_TYPE CHECK (OwnerType IN (''INFRASTRUCTURE'', ''EQUIPMENT'')),
                CONSTRAINT CK_PMIS_DOCUMENT_DEL CHECK (IsDeleted IN (0, 1))
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao bang PMIS_DOCUMENT.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Bang PMIS_DOCUMENT da ton tai — bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM user_indexes WHERE index_name = 'IDX_PMIS_DOCUMENT_OWNER';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_PMIS_DOCUMENT_OWNER ON PMIS_DOCUMENT (OwnerType, OwnerId)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IDX_PMIS_DOCUMENT_OWNER.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_PMIS_DOCUMENT_OWNER da ton tai — bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT OwnerType, COUNT(*) FROM PMIS_DOCUMENT WHERE IsDeleted = 0 GROUP BY OwnerType;
