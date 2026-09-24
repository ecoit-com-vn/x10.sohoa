-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0011_AddSyncCursorToSyncConfig.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0011_AddSyncCursorToSyncConfig.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0011_AddSyncCursorToSyncConfig.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cột SYNC_CURSOR vào SYNC_CONFIG — điểm "tiếp tục" khi 1 lượt đồng bộ tự động dừng giữa
-- chừng vì chạm giới hạn an toàn, để lượt sau tiếp tục đúng chỗ thay vì luôn bắt đầu lại từ đầu (tránh dữ
-- liệu ở cuối danh sách không bao giờ được đồng bộ khi tổng số bản ghi thật vượt giới hạn an toàn/ngân
-- sách gọi PMIS thật). NULL = lượt trước hoàn tất trọn vẹn, lượt sau bắt đầu lại từ đầu.
--
-- ROLLBACK thủ công:
--   ALTER TABLE SYNC_CONFIG DROP COLUMN SYNC_CURSOR;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0011_AddSyncCursorToSyncConfig%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tab_columns
    WHERE table_name = 'SYNC_CONFIG' AND column_name = 'SYNC_CURSOR';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYNC_CONFIG ADD SYNC_CURSOR VARCHAR2(200 CHAR) NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot SYNC_CURSOR vao SYNC_CONFIG.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot SYNC_CURSOR da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, nullable FROM user_tab_columns
-- WHERE table_name = 'SYNC_CONFIG' AND column_name = 'SYNC_CURSOR';
