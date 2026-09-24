-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0013_AddDocumentSyncCursorToSyncConfig.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0013_AddDocumentSyncCursorToSyncConfig.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0013_AddDocumentSyncCursorToSyncConfig.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cột DOCUMENT_SYNC_CURSOR vào SYNC_CONFIG — cursor RIÊNG (độc lập với SYNC_CURSOR ở
-- Migration0011, chỉ phục vụ phân trang dữ liệu chính) cho việc xoay vòng ưu tiên đồng bộ tài liệu đính
-- kèm/ảnh QR của Trạm biến áp/Đường dây — trước đây luôn ưu tiên đúng ~2000 owner ĐẦU danh sách mỗi lượt
-- (ngân sách MaxDocumentSyncCallsPerRun luôn cạn ở cùng vị trí do PMIS trả thứ tự ổn định), các owner còn
-- lại không bao giờ được đồng bộ tài liệu. NULL = lượt trước hoàn tất trọn vẹn, lượt sau bắt đầu lại từ đầu.
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or view
-- does not exist" (unqualified name sẽ tìm nhầm trong schema của user đang kết nối, không phải
-- QLSHX10), và kiểm tra tồn tại cột dùng ALL_TAB_COLUMNS lọc theo OWNER thay vì USER_TAB_COLUMNS
-- (USER_TAB_COLUMNS chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối THẲNG bằng user
-- QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   ALTER TABLE QLSHX10.SYNC_CONFIG DROP COLUMN DOCUMENT_SYNC_CURSOR;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0013_AddDocumentSyncCursorToSyncConfig%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_tab_columns
    WHERE owner = 'QLSHX10' AND table_name = 'SYNC_CONFIG' AND column_name = 'DOCUMENT_SYNC_CURSOR';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.SYNC_CONFIG ADD DOCUMENT_SYNC_CURSOR VARCHAR2(200 CHAR) NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot DOCUMENT_SYNC_CURSOR vao SYNC_CONFIG.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot DOCUMENT_SYNC_CURSOR da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, nullable FROM all_tab_columns
-- WHERE owner = 'QLSHX10' AND table_name = 'SYNC_CONFIG' AND column_name = 'DOCUMENT_SYNC_CURSOR';
