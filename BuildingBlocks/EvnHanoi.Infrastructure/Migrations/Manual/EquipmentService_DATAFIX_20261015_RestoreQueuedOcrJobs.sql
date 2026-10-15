-- ============================================================================
-- SCRIPT SỬA DỮ LIỆU THỦ CÔNG (không phải migration DbUp): khôi phục trạng thái đúng cho job OCR bị watchdog đánh "Lỗi" nhầm khi còn XẾP HÀNG.
-- Bối cảnh: OcrJobWatchdogService (bản cũ) đánh Failed mọi job đứng yên >30 phút; job chỉ xếp hàng trong RabbitMQ (TOTAL_PAGES=0) cũng bị đánh Lỗi
-- dù message vẫn còn — khi worker tới lượt, HandleProgressMessageAsync tự đưa về Running/Completed (349 job đã tự hoàn tất, thông báo lỗi cũ còn sót).
-- Chạy SAU khi deploy EquipmentService bản mới (watchdog tách ngưỡng chờ hàng đợi 24 giờ), nếu không bản cũ sẽ đánh Lỗi lại sau 30 phút.
--
-- KHỐI 2 gồm 2 phần, điều khiển bằng hằng số ở đầu khối:
--   A. (luôn chạy) Xoá ErrorMessage "Job vượt quá…" của job ĐÃ Completed/OcrCompleted/Running/Extracting (thông báo sót — chỉ ảnh hưởng hiển thị).
--   B. (c_requeue = TRUE) Đưa job Failed "Job vượt quá…" mà TOTAL_PAGES=0 (chưa từng được xử lý) về Status='Pending', Action='ocr.process.task',
--      xoá ErrorMessage, đặt MODIFIED_DATE=bây giờ (UTC) để đếm lại thời gian chờ. CHỈ bật sau khi RabbitMQ xác nhận hàng `ocr_task_queue`
--      vẫn còn message tương ứng (nếu message đã mất, job sẽ chờ tới hết ngưỡng 24 giờ rồi bị đánh Lỗi lại với thông báo "có thể message đã mất").
-- Sao lưu các dòng sẽ sửa vào QLSHX10.DOCUMENT_OCR_PROGRESS_BAK_20261015 trước khi ghi; mọi UPDATE chỉ áp dụng cho dòng ĐÃ có trong bảng sao lưu.
--
-- ROLLBACK (khôi phục các cột đã sửa từ bản sao lưu):
--   UPDATE QLSHX10.DOCUMENT_OCR_PROGRESS p SET (STATUS, ACTION, ERROR_MESSAGE, MODIFIED_DATE) =
--      (SELECT b.STATUS, b.ACTION, b.ERROR_MESSAGE, b.MODIFIED_DATE FROM QLSHX10.DOCUMENT_OCR_PROGRESS_BAK_20261015 b WHERE b.ID = p.ID)
--    WHERE p.ID IN (SELECT ID FROM QLSHX10.DOCUMENT_OCR_PROGRESS_BAK_20261015);  COMMIT;
--   (xoá bảng sao lưu khi đã xác nhận: DROP TABLE QLSHX10.DOCUMENT_OCR_PROGRESS_BAK_20261015 PURGE;)
-- ============================================================================
SET SERVEROUTPUT ON

-- Khối 1: sao lưu. Tạo bảng nếu chưa có, rồi LUÔN bổ sung các dòng thuộc phạm vi mà chưa được sao lưu (chạy lại/bật c_requeue sau không sửa dòng nào chưa sao lưu).
DECLARE
    v_count NUMBER;
    v_added NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_TABLES WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'DOCUMENT_OCR_PROGRESS_BAK_20261015';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TABLE QLSHX10.DOCUMENT_OCR_PROGRESS_BAK_20261015 AS SELECT p.* FROM QLSHX10.DOCUMENT_OCR_PROGRESS p WHERE 1 = 0';
        DBMS_OUTPUT.PUT_LINE('Đã tạo bảng sao lưu DOCUMENT_OCR_PROGRESS_BAK_20261015.');
    END IF;
    EXECUTE IMMEDIATE 'INSERT INTO QLSHX10.DOCUMENT_OCR_PROGRESS_BAK_20261015
        SELECT p.* FROM QLSHX10.DOCUMENT_OCR_PROGRESS p
         WHERE p.IS_DELETED = 0 AND p.ERROR_MESSAGE LIKE ''Job vượt quá%''
           AND NOT EXISTS (SELECT 1 FROM QLSHX10.DOCUMENT_OCR_PROGRESS_BAK_20261015 b WHERE b.ID = p.ID)';
    v_added := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Đã sao lưu thêm ' || v_added || ' dòng job có thông báo "Job vượt quá…".');
END;
/

-- Khối 2: sửa dữ liệu.
DECLARE
    c_requeue CONSTANT BOOLEAN := FALSE;   -- TRUE chỉ sau khi RabbitMQ xác nhận message còn trong hàng đợi
    v_a NUMBER := 0;
    v_b NUMBER := 0;
BEGIN
    UPDATE QLSHX10.DOCUMENT_OCR_PROGRESS
       SET ERROR_MESSAGE = NULL
     WHERE IS_DELETED = 0 AND ERROR_MESSAGE LIKE 'Job vượt quá%' AND STATUS IN ('Completed', 'OcrCompleted', 'Running', 'Extracting')
       AND ID IN (SELECT ID FROM QLSHX10.DOCUMENT_OCR_PROGRESS_BAK_20261015);
    v_a := SQL%ROWCOUNT;

    IF c_requeue THEN
        UPDATE QLSHX10.DOCUMENT_OCR_PROGRESS
           SET STATUS = 'Pending', ACTION = 'ocr.process.task', ERROR_MESSAGE = NULL, MODIFIED_DATE = SYS_EXTRACT_UTC(SYSTIMESTAMP)
         WHERE IS_DELETED = 0 AND STATUS = 'Failed' AND PHASE = 'ocr' AND NVL(TOTAL_PAGES, 0) = 0
           AND ERROR_MESSAGE LIKE 'Job vượt quá%'
           AND ID IN (SELECT ID FROM QLSHX10.DOCUMENT_OCR_PROGRESS_BAK_20261015);
        v_b := SQL%ROWCOUNT;
    END IF;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('A) Đã xoá thông báo lỗi sót: ' || v_a || ' dòng. B) Đưa về Pending: ' || v_b || ' dòng' || CASE WHEN c_requeue THEN '.' ELSE ' (c_requeue=FALSE, chưa chạy).' END);
END;
/
