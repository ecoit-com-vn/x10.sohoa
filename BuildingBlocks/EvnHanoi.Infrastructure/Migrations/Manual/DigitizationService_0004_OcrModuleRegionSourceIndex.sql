-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/DigitizationService/0004_OcrModuleRegionSourceIndex.cs
-- ============================================================================
-- KHÔNG chạy tự động. Đặt ở Migrations/Manual/ là CÓ Ý:
--   * DatabaseMigrationHelper nạp script theo filter name.Contains(".Migrations.<Service>.")
--     nên tên resource "...Migrations.Manual.DigitizationService_0004_..." KHÔNG khớp
--     -> DbUp bỏ qua file này, không có nguy cơ chạy trùng với bản .cs.
--   * Không đặt bản .sql vào thẳng Migrations/DigitizationService/: helper dùng
--     OracleDatabaseWithSemicolonDelimiter, nó cắt script theo ';' nên khối PL/SQL
--     (BEGIN/EXCEPTION/END) bên dưới sẽ bị xé thành câu lệnh rời và chết ORA-06550
--     (xem cùng lý do đã ghi ở DigitizationService_0003_FileAttachmentIdToVarchar36.sql).
--
-- KHI NÀO DÙNG: bản .cs không chạy được (DbUp lỗi, cần vá gấp bằng tay, hoặc DBA muốn
-- xem/áp dụng thay đổi ngoài luồng deploy).
--
-- CÁCH CHẠY (sqlplus xử lý PL/SQL bình thường, kết thúc bằng dấu '/'):
--   sqlplus <user>/<pass>@<host>:1521/<service> @DigitizationService_0004_OcrModuleRegionSourceIndex.sql
-- Sau khi chạy tay, nhớ ghi journal để bản .cs không chạy lại (tên script đúng như DbUp đặt):
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.DigitizationService.Migration0004_OcrModuleRegionSourceIndex.cs', SYSTIMESTAMP);
--   COMMIT;
--   (kiểm tra tên cột của bảng SCHEMAVERSIONS trước khi insert — DbUp tạo SCRIPTNAME/APPLIED.)
--
-- NỘI DUNG: thêm cột OCR_MODULE_REGION.SOURCE_INDEX (NUMBER, NULL) — lưu vị trí của box trong
-- mảng JSON OCR gốc trên MinIO ("{filePath}_page_{n}.json") để tính năng sửa tay 1 box (tab
-- "Kiểm tra chính tả và hiệu chỉnh nội dung") ghi đè đúng phần tử rồi dựng lại PDF 2 lớp — không
-- match ngược theo toạ độ box vì toạ độ đã quy đổi DPI (150->200, phép nhân float không round-trip
-- chính xác). Cột NULL cho Job materialize trước migration này: các Job đó không patch được ngược
-- vào MinIO, tính năng sửa sẽ chặn có kiểm soát (409 ERR_OCR_MODULE_REGION_NOT_PATCHABLE) thay vì
-- suy đoán sai vị trí. Idempotent: chạy lại trên DB đã có cột thì không làm gì.
--
-- ROLLBACK thủ công (không có down-migration):
--   ALTER TABLE OCR_MODULE_REGION DROP COLUMN SOURCE_INDEX;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0004_OcrModuleRegionSourceIndex%';
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM user_tab_cols
    WHERE table_name = 'OCR_MODULE_REGION'
      AND column_name = 'SOURCE_INDEX';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE OCR_MODULE_REGION ADD SOURCE_INDEX NUMBER';
        DBMS_OUTPUT.PUT_LINE('Đã thêm cột OCR_MODULE_REGION.SOURCE_INDEX');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cột OCR_MODULE_REGION.SOURCE_INDEX đã tồn tại, bỏ qua');
    END IF;

    DBMS_OUTPUT.PUT_LINE('Hoàn tất.');
END;
/
