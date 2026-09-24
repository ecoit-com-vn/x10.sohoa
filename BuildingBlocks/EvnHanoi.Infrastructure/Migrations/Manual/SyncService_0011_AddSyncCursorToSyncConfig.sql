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
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or view
-- does not exist" (unqualified name sẽ tìm nhầm trong schema của user đang kết nối, không phải
-- QLSHX10), và kiểm tra tồn tại cột dùng ALL_TAB_COLUMNS lọc theo OWNER thay vì USER_TAB_COLUMNS
-- (USER_TAB_COLUMNS chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối THẲNG bằng user
-- QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   ALTER TABLE QLSHX10.SYNC_CONFIG DROP COLUMN SYNC_CURSOR;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0011_AddSyncCursorToSyncConfig%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_tab_columns
    WHERE owner = 'QLSHX10' AND table_name = 'SYNC_CONFIG' AND column_name = 'SYNC_CURSOR';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.SYNC_CONFIG ADD SYNC_CURSOR VARCHAR2(200 CHAR) NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot SYNC_CURSOR vao SYNC_CONFIG.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot SYNC_CURSOR da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, nullable FROM all_tab_columns
-- WHERE owner = 'QLSHX10' AND table_name = 'SYNC_CONFIG' AND column_name = 'SYNC_CURSOR';
