-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0014_AddRecordKindToSyncHistoryDetail.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0014_AddRecordKindToSyncHistoryDetail.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0014_AddRecordKindToSyncHistoryDetail.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cột RECORD_KIND vào SYNC_HISTORY_DETAIL — phân loại 1 dòng chi tiết là INFRASTRUCTURE
-- (Trạm/Đường dây), EQUIPMENT (Thiết bị) hay DOCUMENT (tài liệu đính kèm). SyncDocumentsRotatingAsync cố ý
-- dùng chung SyncHistoryId với lượt Trạm/Đường dây đang chạy (gộp 1 kết quả tổng), nhưng trước đây không
-- có cột nào phân biệt dòng Trạm/Đường dây thật với dòng tài liệu đính kèm — modal "Danh sách bản ghi đã
-- đồng bộ" vì vậy hiển thị lẫn tên file tài liệu vào tab "Trạm biến áp" (phát hiện thật 2026-10-01).
-- KHÔNG backfill dữ liệu lịch sử cũ — NULL = "trước khi có phân loại", API/FE coi NULL như thuộc tab
-- chính đang xem, chỉ dữ liệu MỚI từ nay mới được gán đúng và lọc đúng.
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   ALTER TABLE QLSHX10.SYNC_HISTORY_DETAIL DROP COLUMN RECORD_KIND;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0014_AddRecordKindToSyncHistoryDetail%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_tab_columns
    WHERE owner = 'QLSHX10' AND table_name = 'SYNC_HISTORY_DETAIL' AND column_name = 'RECORD_KIND';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.SYNC_HISTORY_DETAIL ADD RECORD_KIND VARCHAR2(20) NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot RECORD_KIND vao SYNC_HISTORY_DETAIL.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot RECORD_KIND da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, nullable FROM all_tab_columns
-- WHERE owner = 'QLSHX10' AND table_name = 'SYNC_HISTORY_DETAIL' AND column_name = 'RECORD_KIND';
