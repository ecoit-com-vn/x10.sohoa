-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0009_AddRecordCountToPmisApiCallLog.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0009_AddRecordCountToPmisApiCallLog.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0009_AddRecordCountToPmisApiCallLog.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cột RECORD_COUNT vào PMIS_API_CALL_LOG — số bản ghi (Items.Count) trả về trong
-- response của các API dạng danh sách khi gọi thành công, hiển thị kèm trạng thái ở màn "Lịch sử gọi
-- API" thay vì chỉ có mã HTTP. Null với API không phải danh sách (ChiTietThietBi, AnhQRCode) hoặc khi lỗi.
--
-- ROLLBACK thủ công:
--   ALTER TABLE PMIS_API_CALL_LOG DROP COLUMN RECORD_COUNT;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0009_AddRecordCountToPmisApiCallLog%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tab_columns
    WHERE table_name = 'PMIS_API_CALL_LOG' AND column_name = 'RECORD_COUNT';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE PMIS_API_CALL_LOG ADD RECORD_COUNT NUMBER NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot RECORD_COUNT vao PMIS_API_CALL_LOG.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot RECORD_COUNT da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, nullable FROM user_tab_columns
-- WHERE table_name = 'PMIS_API_CALL_LOG' AND column_name = 'RECORD_COUNT';
