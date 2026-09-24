-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0068_AddCmisCodeToInfrastructure.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0068_AddCmisCodeToInfrastructure.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0068_AddCmisCodeToInfrastructure.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: PMIS vừa bổ sung field "maCMIS" vào response của DanhSachTBA (SUBSTATION_LIST) và
-- DanhSachDuongDay (LINE_LIST) (phát hiện 2026-09-24, xem pmis-api-responses/README.md) — mã của hệ thống
-- CMIS (Customer Management/Care Information System, hệ quản lý khách hàng/lưới điện cũ của EVN, khác
-- PMIS), định dạng khác hẳn PMIS_CODE (VD "PD0284251", không dấu "."/"-"). Chỉ lưu tham khảo/hiển thị —
-- KHÔNG dùng làm khoá tra cứu/đối chiếu. Không thêm index.
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng — mọi tham
-- chiếu bảng đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942, và kiểm tra tồn tại cột dùng
-- ALL_TAB_COLUMNS lọc theo OWNER thay vì USER_TAB_COLUMNS. Nếu bạn kết nối THẲNG bằng user QLSHX10, tiền
-- tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   ALTER TABLE QLSHX10.INFRASTRUCTURE DROP COLUMN CMIS_CODE;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0068_AddCmisCodeToInfrastructure%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_tab_columns
    WHERE owner = 'QLSHX10' AND table_name = 'INFRASTRUCTURE' AND column_name = 'CMIS_CODE';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.INFRASTRUCTURE ADD CMIS_CODE VARCHAR2(100) NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot CMIS_CODE vao INFRASTRUCTURE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot CMIS_CODE da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, nullable FROM all_tab_columns
-- WHERE owner = 'QLSHX10' AND table_name = 'INFRASTRUCTURE' AND column_name = 'CMIS_CODE';
