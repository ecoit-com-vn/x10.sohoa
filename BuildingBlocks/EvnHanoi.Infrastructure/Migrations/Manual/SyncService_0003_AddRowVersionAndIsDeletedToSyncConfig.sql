-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/0003_AddRowVersionAndIsDeletedToSyncConfig.cs
-- ============================================================================
-- KHÔNG chạy tự động. Đặt ở Migrations/Manual/ là CÓ Ý:
--   * DatabaseMigrationHelper nạp script theo filter name.Contains(".Migrations.<Service>.")
--     nên tên resource "...Migrations.Manual.SyncService_0003_..." KHÔNG khớp
--     -> DbUp bỏ qua file này, không có nguy cơ chạy trùng với bản .cs.
--   * Không đặt bản .sql vào thẳng Migrations/SyncService/: helper dùng
--     OracleDatabaseWithSemicolonDelimiter, nó cắt script theo ';' nên khối PL/SQL
--     (BEGIN/EXCEPTION/END) bên dưới sẽ bị xé thành câu lệnh rời và chết ORA-06550.
--
-- KHI NÀO DÙNG: bản .cs không chạy được (SyncService không khởi động được — ví dụ RabbitMQ
-- xác thực lỗi chặn Program.cs như đã gặp khi làm tính năng PMIS — hoặc DBA muốn áp dụng
-- thay đổi ngoài luồng deploy ứng dụng).
--
-- CÁCH CHẠY (sqlplus xử lý PL/SQL bình thường, kết thúc bằng dấu '/'):
--   sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql
-- Sau khi chạy tay, nhớ ghi journal để bản .cs không chạy lại (tên script đúng như DbUp đặt):
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0003_AddRowVersionAndIsDeletedToSyncConfig.cs', SYSTIMESTAMP);
--   COMMIT;
--   (kiểm tra tên cột của bảng SCHEMAVERSIONS trước khi insert — DbUp tạo SCRIPTNAME/APPLIED.)
--
-- NỘI DUNG: thêm SYNC_CONFIG.ROW_VERSION (NUMBER, mặc định 1, dùng khoá lạc quan khi 2 người cùng
-- sửa lịch đồng bộ 1 đối tượng) và SYNC_CONFIG.IS_DELETED (NUMBER(1), mặc định 0, theo chuẩn audit
-- chung dù 3 dòng SUBSTATION/TRANSMISSION_LINE/EQUIPMENT trên thực tế không bị xoá). Idempotent:
-- chạy lại trên DB đã có cột/ràng buộc thì không làm gì.
--
-- ROLLBACK thủ công (không có down-migration):
--   ALTER TABLE SYNC_CONFIG DROP CONSTRAINT CK_SYNC_CONFIG_DELETED;
--   ALTER TABLE SYNC_CONFIG DROP COLUMN IS_DELETED;
--   ALTER TABLE SYNC_CONFIG DROP COLUMN ROW_VERSION;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0003_AddRowVersionAndIsDeletedToSyncConfig%';
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tab_cols
    WHERE table_name = 'SYNC_CONFIG' AND column_name = 'ROW_VERSION';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYNC_CONFIG ADD (ROW_VERSION NUMBER DEFAULT 1 NOT NULL)';
        DBMS_OUTPUT.PUT_LINE('Đã thêm cột SYNC_CONFIG.ROW_VERSION');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cột SYNC_CONFIG.ROW_VERSION đã tồn tại, bỏ qua');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tab_cols
    WHERE table_name = 'SYNC_CONFIG' AND column_name = 'IS_DELETED';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYNC_CONFIG ADD (IS_DELETED NUMBER(1) DEFAULT 0 NOT NULL)';
        DBMS_OUTPUT.PUT_LINE('Đã thêm cột SYNC_CONFIG.IS_DELETED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cột SYNC_CONFIG.IS_DELETED đã tồn tại, bỏ qua');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_constraints
    WHERE table_name = 'SYNC_CONFIG' AND constraint_name = 'CK_SYNC_CONFIG_DELETED';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE SYNC_CONFIG ADD CONSTRAINT CK_SYNC_CONFIG_DELETED CHECK (IS_DELETED IN (0, 1))';
        DBMS_OUTPUT.PUT_LINE('Đã thêm ràng buộc CK_SYNC_CONFIG_DELETED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Ràng buộc CK_SYNC_CONFIG_DELETED đã tồn tại, bỏ qua');
    END IF;

    DBMS_OUTPUT.PUT_LINE('Hoàn tất.');
END;
/
