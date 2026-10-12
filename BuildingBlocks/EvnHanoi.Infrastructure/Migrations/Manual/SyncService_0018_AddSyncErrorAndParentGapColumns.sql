-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0018_AddSyncErrorAndParentGapColumns.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0018_AddSyncErrorAndParentGapColumns.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0018_AddSyncErrorAndParentGapColumns.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- PHỤ THUỘC: SyncService_0014_CreatePmisSyncStateTable, _0017_AddDocumentListSync.
--
-- NỘI DUNG:
--  1. Bảng PMIS_SYNC_ERROR (mỗi bản ghi lỗi 1 dòng theo ENTITY_KIND + PMIS_CODE + ERROR_CODE; trạng thái OPEN/QUARANTINED/RESOLVED).
--  2. PMIS_SYNC_STATE thêm QUARANTINED_COUNT, GAP_FLAG, MISSING_STREAK. Idempotent.
-- SCHEMA: tiền tố "QLSHX10." tường minh (xem EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql).
--
-- ROLLBACK:
--   DROP TABLE QLSHX10.PMIS_SYNC_ERROR;
--   ALTER TABLE QLSHX10.PMIS_SYNC_STATE DROP (QUARANTINED_COUNT, GAP_FLAG, MISSING_STREAK);
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0018_AddSyncErrorAndParentGapColumns%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_count NUMBER;

    PROCEDURE add_col(p_col VARCHAR2, p_def VARCHAR2) IS
    BEGIN
        SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS
         WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'PMIS_SYNC_STATE' AND COLUMN_NAME = p_col;
        IF v_count = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.PMIS_SYNC_STATE ADD ' || p_col || ' ' || p_def;
            DBMS_OUTPUT.PUT_LINE('Đã thêm cột ' || p_col);
        END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_TABLES WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'PMIS_SYNC_ERROR';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TABLE QLSHX10.PMIS_SYNC_ERROR (
            ID              VARCHAR2(36)   NOT NULL,
            ENTITY_KIND     VARCHAR2(20)   NOT NULL,
            PMIS_CODE       VARCHAR2(150)  NOT NULL,
            ERROR_CODE      VARCHAR2(40)   NOT NULL,
            MESSAGE         VARCHAR2(1000) NULL,
            STATE           VARCHAR2(16)   DEFAULT ''OPEN'' NOT NULL,
            OCCURRENCES     NUMBER(10)     DEFAULT 1 NOT NULL,
            FIRST_SEEN_AT   TIMESTAMP      NOT NULL,
            LAST_SEEN_AT    TIMESTAMP      NOT NULL,
            NEXT_RETRY_AT   TIMESTAMP      NULL,
            RESOLVED_AT     TIMESTAMP      NULL,
            SOURCE_HASH     VARCHAR2(64)   NULL,
            SYNC_HISTORY_ID VARCHAR2(36)   NULL,
            CONSTRAINT PK_PMIS_SYNC_ERROR PRIMARY KEY (ID),
            CONSTRAINT UQ_PMIS_SYNC_ERROR UNIQUE (ENTITY_KIND, PMIS_CODE, ERROR_CODE),
            CONSTRAINT CK_PMIS_SYNC_ERROR_STATE CHECK (STATE IN (''OPEN'', ''QUARANTINED'', ''RESOLVED''))
        )';
        DBMS_OUTPUT.PUT_LINE('Đã tạo bảng PMIS_SYNC_ERROR.');
    END IF;

    SELECT COUNT(*) INTO v_count FROM ALL_INDEXES WHERE OWNER = 'QLSHX10' AND INDEX_NAME = 'IDX_PMIS_SYNC_ERROR_STATE';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IDX_PMIS_SYNC_ERROR_STATE ON QLSHX10.PMIS_SYNC_ERROR (STATE, ERROR_CODE)';
    END IF;
    SELECT COUNT(*) INTO v_count FROM ALL_INDEXES WHERE OWNER = 'QLSHX10' AND INDEX_NAME = 'IDX_PMIS_SYNC_ERROR_CODE';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IDX_PMIS_SYNC_ERROR_CODE ON QLSHX10.PMIS_SYNC_ERROR (ENTITY_KIND, PMIS_CODE)';
    END IF;

    add_col('QUARANTINED_COUNT', 'NUMBER(10) NULL');
    add_col('GAP_FLAG', 'NUMBER(1) NULL');
    add_col('MISSING_STREAK', 'NUMBER(3) NULL');
END;
/
