-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0007_AddConsecutiveFailureCountToSyncConfig.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0007_AddConsecutiveFailureCountToSyncConfig.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0007_AddConsecutiveFailureCountToSyncConfig.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cột CONSECUTIVE_FAILURE_COUNT vào SYNC_CONFIG — đếm số lần đồng bộ tự động lỗi
-- liên tiếp ngay từ bước gọi danh sách PMIS (timeout/401/404/circuit breaker), dùng để
-- PmisScheduledSyncJob backoff tăng dần thay vì retry mỗi phút vô hạn, và quyết định khi nào cần
-- cảnh báo admin.
--
-- ROLLBACK thủ công:
--   ALTER TABLE SYNC_CONFIG DROP COLUMN CONSECUTIVE_FAILURE_COUNT;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0007_AddConsecutiveFailureCountToSyncConfig%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tab_columns
    WHERE table_name = 'SYNC_CONFIG' AND column_name = 'CONSECUTIVE_FAILURE_COUNT';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYNC_CONFIG ADD CONSECUTIVE_FAILURE_COUNT NUMBER DEFAULT 0 NOT NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot CONSECUTIVE_FAILURE_COUNT vao SYNC_CONFIG.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot CONSECUTIVE_FAILURE_COUNT da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, data_default, nullable FROM user_tab_columns
-- WHERE table_name = 'SYNC_CONFIG' AND column_name = 'CONSECUTIVE_FAILURE_COUNT';
