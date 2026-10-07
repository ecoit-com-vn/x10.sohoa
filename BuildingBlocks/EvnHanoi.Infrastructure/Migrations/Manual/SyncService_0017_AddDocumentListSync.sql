-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0017_AddDocumentListSync.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0017_AddDocumentListSync.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0017_AddDocumentListSync.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- PHỤ THUỘC: SyncService_0001 (SYNC_CONFIG/SYNC_HISTORY), _0004 (seed), _0014_CreatePmisSyncStateTable.
--
-- NỘI DUNG:
--  1. PMIS_SYNC_STATE: thêm REMOTE_TOTAL, LOCAL_COUNT, LAST_DOC_FETCH_AT, LAST_DOC_COUNT_AT, LAST_DOC_FULL_AT, DOC_SCAN_SKIP
--     (trạng thái đồng bộ tài liệu theo owner 'DOC_OWNER' và theo khoảng ngày 'DOC_WINDOW').
--  2. Nới CHECK OBJECT_TYPE của SYNC_CONFIG và SYNC_HISTORY thêm 'DOCUMENT'.
--  3. Seed SYNC_CONFIG 'DOCUMENT' (TẮT, 2 giờ). Idempotent.
-- SCHEMA: tiền tố "QLSHX10." tường minh (xem EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql).
--
-- ROLLBACK (thủ công):
--   DELETE FROM QLSHX10.SYNC_HISTORY WHERE OBJECT_TYPE = 'DOCUMENT';  -- chi tiết xoá theo cascade/ON DELETE nếu có
--   DELETE FROM QLSHX10.SYNC_CONFIG WHERE OBJECT_TYPE = 'DOCUMENT';
--   DELETE FROM QLSHX10.PMIS_SYNC_STATE WHERE OBJECT_TYPE IN ('DOC_OWNER', 'DOC_WINDOW');
--   (đặt lại 2 CHECK như Migration0001; bỏ các cột mới nếu cần)
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0017_AddDocumentListSync%';
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

    PROCEDURE widen_check(p_table VARCHAR2, p_constraint VARCHAR2) IS
    BEGIN
        SELECT COUNT(*) INTO v_count FROM ALL_CONSTRAINTS
         WHERE OWNER = 'QLSHX10' AND TABLE_NAME = p_table AND CONSTRAINT_NAME = p_constraint;
        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.' || p_table || ' DROP CONSTRAINT ' || p_constraint;
        END IF;
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.' || p_table || ' ADD CONSTRAINT ' || p_constraint
            || ' CHECK (OBJECT_TYPE IN (''SUBSTATION'', ''TRANSMISSION_LINE'', ''EQUIPMENT'', ''DOCUMENT''))';
        DBMS_OUTPUT.PUT_LINE('Đã nới CHECK ' || p_constraint);
    END;
BEGIN
    add_col('REMOTE_TOTAL', 'NUMBER(10) NULL');
    add_col('LOCAL_COUNT', 'NUMBER(10) NULL');
    add_col('LAST_DOC_FETCH_AT', 'TIMESTAMP NULL');
    add_col('LAST_DOC_COUNT_AT', 'TIMESTAMP NULL');
    add_col('LAST_DOC_FULL_AT', 'TIMESTAMP NULL');
    add_col('DOC_SCAN_SKIP', 'NUMBER(10) NULL');

    widen_check('SYNC_CONFIG', 'CK_SYNC_CONFIG_OBJECT_TYPE');
    widen_check('SYNC_HISTORY', 'CK_SYNC_HISTORY_OBJECT_TYPE');

    SELECT COUNT(*) INTO v_count FROM QLSHX10.SYNC_CONFIG WHERE OBJECT_TYPE = 'DOCUMENT';
    IF v_count = 0 THEN
        INSERT INTO QLSHX10.SYNC_CONFIG (ID, OBJECT_TYPE, FREQUENCY_VALUE, FREQUENCY_UNIT, IS_ENABLED)
        VALUES (LOWER(REGEXP_REPLACE(RAWTOHEX(SYS_GUID()), '(.{8})(.{4})(.{4})(.{4})(.{12})', '\1-\2-\3-\4-\5')),
                'DOCUMENT', 2, 'HOUR', 0);
        DBMS_OUTPUT.PUT_LINE('Đã seed SYNC_CONFIG DOCUMENT (tắt, 2 giờ).');
    END IF;
    COMMIT;
END;
/
