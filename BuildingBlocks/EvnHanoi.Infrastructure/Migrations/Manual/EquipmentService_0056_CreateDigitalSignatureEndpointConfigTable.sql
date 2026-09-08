-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0056_CreateDigitalSignatureEndpointConfigTable.cs
-- ============================================================================
-- KHÔNG chạy tự động. Đặt ở Migrations/Manual/ là CÓ Ý:
--   * DatabaseMigrationHelper nạp script theo filter name.Contains(".Migrations.<Service>.")
--     nên tên resource "...Migrations.Manual.EquipmentService_0056_..." KHÔNG khớp
--     -> DbUp bỏ qua file này, không có nguy cơ chạy trùng với bản .cs.
--   * Không đặt bản .sql vào thẳng Migrations/EquipmentService/: helper dùng
--     OracleDatabaseWithSemicolonDelimiter, nó cắt script theo ';' nên khối PL/SQL
--     (BEGIN/EXCEPTION/END) bên dưới sẽ bị xé thành câu lệnh rời và chết ORA-06550.
--
-- KHI NÀO DÙNG: bản .cs không chạy được (EquipmentService không khởi động được), hoặc DBA
-- muốn áp dụng thay đổi ngoài luồng deploy ứng dụng — ví dụ chạy tay trên 1 server khác.
--
-- CÁCH CHẠY (sqlplus xử lý PL/SQL bình thường, kết thúc bằng dấu '/'):
--   sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0056_CreateDigitalSignatureEndpointConfigTable.sql
-- Sau khi chạy tay, nhớ ghi journal để bản .cs không chạy lại (tên script đúng như DbUp đặt):
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0056_CreateDigitalSignatureEndpointConfigTable.cs', SYSTIMESTAMP);
--   COMMIT;
--   (kiểm tra tên cột của bảng SCHEMAVERSIONS trước khi insert — DbUp tạo SCRIPTNAME/APPLIED.)
--
-- NỘI DUNG: tạo bảng DIGITAL_SIGNATURE_ENDPOINT_CONFIG (cấu hình Url + trạng thái cho 3 API tích
-- hợp ký số ngoài — xem HUONG_DAN_TICH_HOP_KY_SO.md và KySoClient.cs) và seed sẵn đúng 3 dòng.
-- Bảng chỉ dùng để tra cứu/quản lý qua màn "Thiết lập đồng bộ ký số" — KySoClient hiện vẫn đọc URL
-- thực tế từ appsettings ("Endpoints:KySo"), CHƯA đọc từ bảng này. Idempotent: chạy lại trên DB đã
-- có bảng/dữ liệu thì không làm gì.
--
-- ROLLBACK thủ công (không có down-migration):
--   DROP TABLE DIGITAL_SIGNATURE_ENDPOINT_CONFIG;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0056_CreateDigitalSignatureEndpointConfigTable%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

-- 1. Tạo bảng (bỏ qua nếu đã tồn tại)
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tables WHERE table_name = 'DIGITAL_SIGNATURE_ENDPOINT_CONFIG';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE DIGITAL_SIGNATURE_ENDPOINT_CONFIG (
                ID              VARCHAR2(36)    NOT NULL,
                API_CODE        VARCHAR2(50)    NOT NULL,
                DISPLAY_NAME    NVARCHAR2(250)  NOT NULL,
                URL             VARCHAR2(500)   NULL,
                IS_ACTIVE       NUMBER(1)       DEFAULT 1 NOT NULL,
                ROW_VERSION     NUMBER          DEFAULT 1 NOT NULL,
                CREATED_BY      VARCHAR2(100)   NULL,
                CREATED_DATE    TIMESTAMP       DEFAULT SYSTIMESTAMP NOT NULL,
                MODIFIED_BY     VARCHAR2(100)   NULL,
                MODIFIED_DATE   TIMESTAMP       NULL,
                IS_DELETED      NUMBER(1)       DEFAULT 0 NOT NULL,
                CONSTRAINT PK_DIGITAL_SIGNATURE_ENDPOINT_CONFIG PRIMARY KEY (ID),
                CONSTRAINT UQ_DIGITAL_SIGNATURE_ENDPOINT_CONFIG_CODE UNIQUE (API_CODE),
                CONSTRAINT CK_DIGITAL_SIGNATURE_ENDPOINT_CONFIG_CODE CHECK (API_CODE IN (
                    ''KYSO_GET_SERIAL'', ''KYSO_GET_SIGNATURE_IMAGE'', ''KYSO_SIGN_PDF''
                )),
                CONSTRAINT CK_DIGITAL_SIGNATURE_ENDPOINT_CONFIG_ACTIVE CHECK (IS_ACTIVE IN (0, 1)),
                CONSTRAINT CK_DIGITAL_SIGNATURE_ENDPOINT_CONFIG_DELETED CHECK (IS_DELETED IN (0, 1))
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao bang DIGITAL_SIGNATURE_ENDPOINT_CONFIG.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Bang DIGITAL_SIGNATURE_ENDPOINT_CONFIG da ton tai, bo qua.');
    END IF;
END;
/

-- 2. Seed 3 dòng API ký số (bỏ qua từng dòng nếu API_CODE đã tồn tại)
DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM DIGITAL_SIGNATURE_ENDPOINT_CONFIG WHERE API_CODE = 'KYSO_GET_SERIAL';
    IF v_exists = 0 THEN
        INSERT INTO DIGITAL_SIGNATURE_ENDPOINT_CONFIG (ID, API_CODE, DISPLAY_NAME, URL, IS_ACTIVE)
        VALUES (
            SYS_GUID(),
            'KYSO_GET_SERIAL',
            N'Lấy thông tin serial chứng thư số',
            'https://gwlocal.evnhanoi.vn/api/DigitalSignature/lay-thong-tin-serial-number',
            1
        );
        DBMS_OUTPUT.PUT_LINE('Da seed KYSO_GET_SERIAL.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('KYSO_GET_SERIAL da ton tai, bo qua.');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM DIGITAL_SIGNATURE_ENDPOINT_CONFIG WHERE API_CODE = 'KYSO_GET_SIGNATURE_IMAGE';
    IF v_exists = 0 THEN
        INSERT INTO DIGITAL_SIGNATURE_ENDPOINT_CONFIG (ID, API_CODE, DISPLAY_NAME, URL, IS_ACTIVE)
        VALUES (
            SYS_GUID(),
            'KYSO_GET_SIGNATURE_IMAGE',
            N'Lấy ảnh chữ ký',
            'https://gwlocal.evnhanoi.vn/hrms/api/Hrms/lay-anh-chu-ky',
            1
        );
        DBMS_OUTPUT.PUT_LINE('Da seed KYSO_GET_SIGNATURE_IMAGE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('KYSO_GET_SIGNATURE_IMAGE da ton tai, bo qua.');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM DIGITAL_SIGNATURE_ENDPOINT_CONFIG WHERE API_CODE = 'KYSO_SIGN_PDF';
    IF v_exists = 0 THEN
        INSERT INTO DIGITAL_SIGNATURE_ENDPOINT_CONFIG (ID, API_CODE, DISPLAY_NAME, URL, IS_ACTIVE)
        VALUES (
            SYS_GUID(),
            'KYSO_SIGN_PDF',
            N'Ký số file PDF (đóng dấu ảnh chữ ký)',
            'https://gwlocal.evnhanoi.vn/kyso/api/DigitalSignature/sign-pdf-base64-image',
            1
        );
        DBMS_OUTPUT.PUT_LINE('Da seed KYSO_SIGN_PDF.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('KYSO_SIGN_PDF da ton tai, bo qua.');
    END IF;

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Hoan tat.');
END;
/

-- KIỂM TRA:
-- SELECT API_CODE, DISPLAY_NAME, URL, IS_ACTIVE FROM DIGITAL_SIGNATURE_ENDPOINT_CONFIG ORDER BY API_CODE;
