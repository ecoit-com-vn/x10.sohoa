-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0005_AddDeviceQrImageApiEndpoint.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0005_AddDeviceQrImageApiEndpoint.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0005_AddDeviceQrImageApiEndpoint.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: bổ sung API thứ 10 'DEVICE_QR_IMAGE' (API ảnh QR thiết bị) vào PMIS_API_ENDPOINT_CONFIG.
-- API này KHÔNG có trong tài liệu docx gốc (9 API), phát hiện khi gọi thật vào gateway PMIS — trả về
-- ẢNH JPEG nhị phân, không phải JSON; field maQRCode ở các API khác chỉ là URL trỏ tới nó. Xem
-- BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md §4.7.
-- Phải nới CHECK constraint cũ (chỉ cho phép đúng 9 mã) mới insert được mã mới.
--
-- Dòng seed để IS_ACTIVE = 0 và URL rỗng — Admin tự điền URL rồi bật ở màn
-- "Quản trị hệ thống > Cấu hình kết nối PMIS" (đường dẫn thật: <gateway>/api/PmisDongBo/AnhQRCode).
-- Chưa cấu hình thì đồng bộ vẫn chạy, chỉ bỏ trống ảnh QR.
--
-- ROLLBACK thủ công:
--   DELETE FROM PMIS_API_ENDPOINT_CONFIG WHERE API_CODE = 'DEVICE_QR_IMAGE';
--   ALTER TABLE PMIS_API_ENDPOINT_CONFIG DROP CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE;
--   ALTER TABLE PMIS_API_ENDPOINT_CONFIG ADD CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE CHECK (API_CODE IN (
--       'SUBSTATION_LIST', 'LINE_LIST', 'SUBSTATION_DEVICE_TYPE_LIST', 'SUBSTATION_DEVICE_LIST',
--       'LINE_DEVICE_TYPE_LIST', 'LINE_DEVICE_LIST', 'DEVICE_DETAIL',
--       'SUBSTATION_DOCUMENT_LIST', 'LINE_DOCUMENT_LIST'));
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0005_AddDeviceQrImageApiEndpoint%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    -- 1. Nới CHECK constraint để chấp nhận mã thứ 10.
    SELECT COUNT(*) INTO v_exists
    FROM user_constraints
    WHERE constraint_name = 'CK_PMIS_API_ENDPOINT_CONFIG_CODE'
      AND table_name = 'PMIS_API_ENDPOINT_CONFIG';

    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE PMIS_API_ENDPOINT_CONFIG DROP CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE';
        DBMS_OUTPUT.PUT_LINE('Da bo CHECK constraint cu.');
    END IF;

    EXECUTE IMMEDIATE '
        ALTER TABLE PMIS_API_ENDPOINT_CONFIG ADD CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE CHECK (API_CODE IN (
            ''SUBSTATION_LIST'', ''LINE_LIST'', ''SUBSTATION_DEVICE_TYPE_LIST'', ''SUBSTATION_DEVICE_LIST'',
            ''LINE_DEVICE_TYPE_LIST'', ''LINE_DEVICE_LIST'', ''DEVICE_DETAIL'',
            ''SUBSTATION_DOCUMENT_LIST'', ''LINE_DOCUMENT_LIST'', ''DEVICE_QR_IMAGE''
        ))';
    DBMS_OUTPUT.PUT_LINE('Da tao lai CHECK constraint voi 10 ma API.');

    -- 2. Seed dòng cấu hình cho API ảnh QR.
    SELECT COUNT(*) INTO v_exists
    FROM PMIS_API_ENDPOINT_CONFIG WHERE API_CODE = 'DEVICE_QR_IMAGE';

    IF v_exists = 0 THEN
        INSERT INTO PMIS_API_ENDPOINT_CONFIG (ID, API_CODE, DISPLAY_NAME, IS_ACTIVE)
        VALUES (
            LOWER(REGEXP_REPLACE(RAWTOHEX(SYS_GUID()),
                '(.{8})(.{4})(.{4})(.{4})(.{12})', '\1-\2-\3-\4-\5')),
            'DEVICE_QR_IMAGE', 'API ảnh QR thiết bị', 0);
        DBMS_OUTPUT.PUT_LINE('Da seed dong DEVICE_QR_IMAGE (IS_ACTIVE = 0, cho Admin dien URL).');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Dong DEVICE_QR_IMAGE da ton tai — bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA: phải ra đủ 10 dòng, trong đó có DEVICE_QR_IMAGE.
-- SELECT API_CODE, DISPLAY_NAME, URL, IS_ACTIVE FROM PMIS_API_ENDPOINT_CONFIG
-- WHERE IS_DELETED = 0 ORDER BY API_CODE;
