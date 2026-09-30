-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0014_CreatePmisSyncStateTable.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0014_CreatePmisSyncStateTable.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0014_CreatePmisSyncStateTable.cs', SYSTIMESTAMP);
--   COMMIT;
--   (nếu SCHEMAVERSIONS báo ORA-01400 thiếu SCHEMAVERSIONID: chạy _REPAIR_FixSchemaVersionsIdentity.sql trước)
--
-- NỘI DUNG: bảng trạng thái đồng bộ PMIS tăng dần (hash nội dung lần đẩy thành công gần nhất của từng bản
-- ghi PMIS). OBJECT_TYPE: SUBSTATION | TRANSMISSION_LINE | EQUIPMENT | PARENT_SCAN | SWEEP.
--
-- SCHEMA: mọi tham chiếu gắn tiền tố QLSHX10; kiểm tra tồn tại dùng ALL_TABLES lọc OWNER.
--
-- ROLLBACK thủ công:
--   DROP TABLE QLSHX10.PMIS_SYNC_STATE;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0014_CreatePmisSyncStateTable%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_tables
    WHERE owner = 'QLSHX10' AND table_name = 'PMIS_SYNC_STATE';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE QLSHX10.PMIS_SYNC_STATE (
                OBJECT_TYPE     VARCHAR2(30)  NOT NULL,
                PMIS_CODE       VARCHAR2(150) NOT NULL,
                CONTENT_HASH    VARCHAR2(64)  NULL,
                HASH_VERSION    NUMBER(3)     DEFAULT 1 NOT NULL,
                DETAIL_SYNCED   NUMBER(1)     DEFAULT 0 NOT NULL,
                LAST_PUSHED_AT  TIMESTAMP     NULL,
                LAST_SEEN_AT    TIMESTAMP     NULL,
                LAST_SCAN_AT    TIMESTAMP     NULL,
                CONSTRAINT PK_PMIS_SYNC_STATE PRIMARY KEY (OBJECT_TYPE, PMIS_CODE),
                CONSTRAINT CK_PMIS_SYNC_STATE_DETAIL CHECK (DETAIL_SYNCED IN (0, 1))
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao bang PMIS_SYNC_STATE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Bang PMIS_SYNC_STATE da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, nullable FROM all_tab_columns
-- WHERE owner = 'QLSHX10' AND table_name = 'PMIS_SYNC_STATE' ORDER BY column_id;
