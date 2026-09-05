-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0006_AddWarningStatusToSyncHistory.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0006_AddWarningStatusToSyncHistory.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0006_AddWarningStatusToSyncHistory.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: nới CHECK constraint SYNC_HISTORY.STATUS và SYNC_HISTORY_DETAIL.STATUS để chấp nhận thêm
-- giá trị 'WARNING' — dùng khi 1 bước phụ (đồng bộ tài liệu đính kèm) lỗi nhưng không làm hỏng cả lượt
-- đồng bộ chính (Trạm/Đường dây/Thiết bị vẫn lưu thành công).
--
-- ROLLBACK thủ công:
--   ALTER TABLE SYNC_HISTORY DROP CONSTRAINT CK_SYNC_HISTORY_STATUS;
--   ALTER TABLE SYNC_HISTORY ADD CONSTRAINT CK_SYNC_HISTORY_STATUS CHECK (STATUS IN ('RUNNING','SUCCESS','FAILED'));
--   ALTER TABLE SYNC_HISTORY_DETAIL DROP CONSTRAINT CK_SYNC_HISTORY_DETAIL_STATUS;
--   ALTER TABLE SYNC_HISTORY_DETAIL ADD CONSTRAINT CK_SYNC_HISTORY_DETAIL_STATUS CHECK (STATUS IN ('SUCCESS','FAILED'));
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0006_AddWarningStatusToSyncHistory%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_constraints WHERE constraint_name = 'CK_SYNC_HISTORY_STATUS';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYNC_HISTORY DROP CONSTRAINT CK_SYNC_HISTORY_STATUS';
    END IF;
    EXECUTE IMMEDIATE 'ALTER TABLE SYNC_HISTORY ADD CONSTRAINT CK_SYNC_HISTORY_STATUS CHECK (STATUS IN (''RUNNING'',''SUCCESS'',''FAILED'',''WARNING''))';
    DBMS_OUTPUT.PUT_LINE('Da noi CK_SYNC_HISTORY_STATUS them WARNING.');

    SELECT COUNT(*) INTO v_exists FROM user_constraints WHERE constraint_name = 'CK_SYNC_HISTORY_DETAIL_STATUS';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYNC_HISTORY_DETAIL DROP CONSTRAINT CK_SYNC_HISTORY_DETAIL_STATUS';
    END IF;
    EXECUTE IMMEDIATE 'ALTER TABLE SYNC_HISTORY_DETAIL ADD CONSTRAINT CK_SYNC_HISTORY_DETAIL_STATUS CHECK (STATUS IN (''SUCCESS'',''FAILED'',''WARNING''))';
    DBMS_OUTPUT.PUT_LINE('Da noi CK_SYNC_HISTORY_DETAIL_STATUS them WARNING.');

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT constraint_name, search_condition FROM user_constraints
-- WHERE constraint_name IN ('CK_SYNC_HISTORY_STATUS','CK_SYNC_HISTORY_DETAIL_STATUS');
