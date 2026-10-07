-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0015_AddDocumentFileDownloadApiEndpoint.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0015_AddDocumentFileDownloadApiEndpoint.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0015_AddDocumentFileDownloadApiEndpoint.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- PHỤ THUỘC: chạy SAU SyncService_0005 (DEVICE_QR_IMAGE) và _0010 (cột PAGE_SIZE).
--
-- NỘI DUNG:
--  1. Nới CHECK constraint thêm mã 'DOCUMENT_FILE_DOWNLOAD' (API tải file tài liệu: TaiFileTaiLieu?maTaiLieu=...).
--  2. Seed dòng cấu hình, IS_ACTIVE = 0, URL rỗng, GET, timeout 120 giây — admin nhập URL (không kèm maTaiLieu) rồi bật trên màn
--     "Cấu hình kết nối API". Chưa bật thì job tải file tự dừng, không tăng số lần thử của tài liệu.
--  3. Hạ PAGE_SIZE 2 API danh sách tài liệu xuống 10 (chỉ khi NULL hoặc > 10) — payload từng chứa base64 file.
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10, tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DELETE FROM QLSHX10.PMIS_API_ENDPOINT_CONFIG WHERE API_CODE = 'DOCUMENT_FILE_DOWNLOAD';
--   ALTER TABLE QLSHX10.PMIS_API_ENDPOINT_CONFIG DROP CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE;
--   ALTER TABLE QLSHX10.PMIS_API_ENDPOINT_CONFIG ADD CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE CHECK (API_CODE IN (
--       'SUBSTATION_LIST', 'LINE_LIST', 'SUBSTATION_DEVICE_TYPE_LIST', 'SUBSTATION_DEVICE_LIST',
--       'LINE_DEVICE_TYPE_LIST', 'LINE_DEVICE_LIST', 'DEVICE_DETAIL',
--       'SUBSTATION_DOCUMENT_LIST', 'LINE_DOCUMENT_LIST', 'DEVICE_QR_IMAGE'));
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0015_AddDocumentFileDownloadApiEndpoint%';
--   COMMIT;
--   (PAGE_SIZE đã hạ: đặt lại bằng UPDATE tay nếu cần — giá trị cũ không được lưu lại.)
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM ALL_CONSTRAINTS
    WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'PMIS_API_ENDPOINT_CONFIG' AND CONSTRAINT_NAME = 'CK_PMIS_API_ENDPOINT_CONFIG_CODE';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.PMIS_API_ENDPOINT_CONFIG DROP CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE';
    END IF;

    EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.PMIS_API_ENDPOINT_CONFIG ADD CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE CHECK (API_CODE IN (
        ''SUBSTATION_LIST'', ''LINE_LIST'', ''SUBSTATION_DEVICE_TYPE_LIST'', ''SUBSTATION_DEVICE_LIST'',
        ''LINE_DEVICE_TYPE_LIST'', ''LINE_DEVICE_LIST'', ''DEVICE_DETAIL'',
        ''SUBSTATION_DOCUMENT_LIST'', ''LINE_DOCUMENT_LIST'', ''DEVICE_QR_IMAGE'', ''DOCUMENT_FILE_DOWNLOAD''
    ))';

    SELECT COUNT(*) INTO v_exists FROM QLSHX10.PMIS_API_ENDPOINT_CONFIG WHERE API_CODE = 'DOCUMENT_FILE_DOWNLOAD';
    IF v_exists = 0 THEN
        INSERT INTO QLSHX10.PMIS_API_ENDPOINT_CONFIG (ID, API_CODE, DISPLAY_NAME, HTTP_METHOD, TIMEOUT_SECONDS, IS_ACTIVE)
        VALUES (LOWER(RAWTOHEX(SYS_GUID())), 'DOCUMENT_FILE_DOWNLOAD', 'API tải file tài liệu', 'GET', 120, 0);
        DBMS_OUTPUT.PUT_LINE('Da them DOCUMENT_FILE_DOWNLOAD (tat, chua co URL).');
    ELSE
        DBMS_OUTPUT.PUT_LINE('DOCUMENT_FILE_DOWNLOAD da ton tai - bo qua.');
    END IF;

    UPDATE QLSHX10.PMIS_API_ENDPOINT_CONFIG SET PAGE_SIZE = 10
    WHERE API_CODE IN ('SUBSTATION_DOCUMENT_LIST', 'LINE_DOCUMENT_LIST') AND (PAGE_SIZE IS NULL OR PAGE_SIZE > 10);
    DBMS_OUTPUT.PUT_LINE('Ha PAGE_SIZE: ' || SQL%ROWCOUNT || ' dong.');
    COMMIT;
END;
/

-- KIỂM TRA SAU KHI CHẠY:
-- SELECT API_CODE, HTTP_METHOD, TIMEOUT_SECONDS, IS_ACTIVE, PAGE_SIZE, URL FROM QLSHX10.PMIS_API_ENDPOINT_CONFIG
-- WHERE API_CODE IN ('DOCUMENT_FILE_DOWNLOAD', 'SUBSTATION_DOCUMENT_LIST', 'LINE_DOCUMENT_LIST');
