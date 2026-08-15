-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/0004_SeedSyncConfigRows.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0004_SeedSyncConfigRows.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0004_SeedSyncConfigRows.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: seed 3 dòng SYNC_CONFIG (SUBSTATION/TRANSMISSION_LINE/EQUIPMENT) — bảng đã có sẵn từ
-- Migration0001 nhưng chưa từng được seed, khiến SYNC_HISTORY.SYNC_CONFIG_ID (NOT NULL) không có
-- gì để tham chiếu (lỗi thật gặp khi test tay: ORA-01400 cannot insert NULL). Mặc định tắt
-- (IS_ENABLED=0), tần suất 60 phút.
-- ROLLBACK thủ công:
--   DELETE FROM SYNC_CONFIG WHERE OBJECT_TYPE IN ('SUBSTATION','TRANSMISSION_LINE','EQUIPMENT');
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0004_SeedSyncConfigRows%';
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    FOR obj_type IN (SELECT 'SUBSTATION' AS code FROM DUAL UNION ALL
                      SELECT 'TRANSMISSION_LINE' FROM DUAL UNION ALL
                      SELECT 'EQUIPMENT' FROM DUAL)
    LOOP
        SELECT COUNT(*) INTO v_exists FROM SYNC_CONFIG WHERE OBJECT_TYPE = obj_type.code;
        IF v_exists = 0 THEN
            INSERT INTO SYNC_CONFIG (ID, OBJECT_TYPE, FREQUENCY_VALUE, FREQUENCY_UNIT, IS_ENABLED)
            VALUES (RAWTOHEX(SYS_GUID()), obj_type.code, 60, 'MINUTE', 0);
            DBMS_OUTPUT.PUT_LINE('Đã seed SYNC_CONFIG cho ' || obj_type.code);
        ELSE
            DBMS_OUTPUT.PUT_LINE('SYNC_CONFIG cho ' || obj_type.code || ' đã tồn tại, bỏ qua');
        END IF;
    END LOOP;

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Hoàn tất.');
END;
/
