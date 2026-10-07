-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0075_ResetPmisDocumentFileQueue.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0075_ResetPmisDocumentFileQueue.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0075_ResetPmisDocumentFileQueue.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- PHỤ THUỘC: chạy SAU EquipmentService_0069 (cột FILE_STATUS/FILE_ATTEMPTS/FILE_NEXT_RETRY_AT/FILE_LAST_ERROR).
--
-- NỘI DUNG: đưa mọi tài liệu CHƯA có file (ObjectKey IS NULL, chưa xoá mềm) về hàng đợi sạch — FILE_STATUS='PENDING',
-- FILE_ATTEMPTS=0, FILE_NEXT_RETRY_AT=NULL, FILE_LAST_ERROR=NULL — vì file giờ tải theo MÃ tài liệu qua API cấu hình
-- DOCUMENT_FILE_DOWNLOAD, các lỗi/lịch thử lại cũ (404 route chưa có, HTTP 500, FAILED chờ 24 giờ, NO_URL) không còn đúng.
-- Chia lô 10.000, COMMIT mỗi lô; điều kiện loại các dòng đã sạch nên idempotent.
--
-- KIỂM TRA TRƯỚC KHI CHẠY (số dòng sẽ đặt lại, xấp xỉ):
--   SELECT FILE_STATUS, COUNT(*) FROM QLSHX10.PMIS_DOCUMENT WHERE ObjectKey IS NULL AND IsDeleted = 0 GROUP BY FILE_STATUS;
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10, tiền tố thừa nhưng vô hại.
--
-- ROLLBACK: không có — migration chỉ đặt lại TRẠNG THÁI CHỜ TẢI (không mất file/dữ liệu); lỗi/lịch thử lại cũ không được lưu lại.
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    c_batch       CONSTANT PLS_INTEGER := 10000;
    c_max_batches CONSTANT PLS_INTEGER := 200;
    v_batch_no    PLS_INTEGER := 0;
    v_reset       NUMBER;
    v_total       NUMBER := 0;
BEGIN
    LOOP
        v_batch_no := v_batch_no + 1;

        UPDATE QLSHX10.PMIS_DOCUMENT
        SET FILE_STATUS = 'PENDING', FILE_ATTEMPTS = 0, FILE_NEXT_RETRY_AT = NULL, FILE_LAST_ERROR = NULL
        WHERE ROWID IN (
            SELECT ROWID FROM QLSHX10.PMIS_DOCUMENT
            WHERE ObjectKey IS NULL AND IsDeleted = 0
              AND (FILE_STATUS <> 'PENDING' OR FILE_ATTEMPTS <> 0 OR FILE_NEXT_RETRY_AT IS NOT NULL OR FILE_LAST_ERROR IS NOT NULL)
              AND ROWNUM <= c_batch
        );
        v_reset := SQL%ROWCOUNT;
        COMMIT;
        EXIT WHEN v_reset = 0;

        v_total := v_total + v_reset;
        DBMS_OUTPUT.PUT_LINE('Lo ' || v_batch_no || ': dat lai ' || v_reset || ' tai lieu (tong ' || v_total || ').');
        EXIT WHEN v_batch_no >= c_max_batches;
    END LOOP;

    DBMS_OUTPUT.PUT_LINE('Hoan tat: da dat lai ' || v_total || ' tai lieu chua co file ve hang doi.');
END;
/

-- KIỂM TRA SAU KHI CHẠY:
-- SELECT FILE_STATUS, MAX(FILE_ATTEMPTS) AS MAX_LAN_THU, COUNT(*) FROM QLSHX10.PMIS_DOCUMENT WHERE ObjectKey IS NULL AND IsDeleted = 0 GROUP BY FILE_STATUS;
