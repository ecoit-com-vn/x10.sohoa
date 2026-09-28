-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0010_AddPageSizeToPmisApiEndpointConfig.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0010_AddPageSizeToPmisApiEndpointConfig.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0010_AddPageSizeToPmisApiEndpointConfig.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cột PAGE_SIZE vào PMIS_API_ENDPOINT_CONFIG — số bản ghi mỗi trang ("take") khi phân
-- trang gọi từng API PMIS, admin tự cấu hình qua "Cấu hình kết nối API" thay vì hard-code trong code.
-- NULL = dùng mặc định 100 (PmisPaging.DefaultPageSize).
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   ALTER TABLE QLSHX10.PMIS_API_ENDPOINT_CONFIG DROP COLUMN PAGE_SIZE;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0010_AddPageSizeToPmisApiEndpointConfig%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM ALL_TAB_COLUMNS
    WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'PMIS_API_ENDPOINT_CONFIG' AND COLUMN_NAME = 'PAGE_SIZE';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.PMIS_API_ENDPOINT_CONFIG ADD PAGE_SIZE NUMBER NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot PAGE_SIZE vao PMIS_API_ENDPOINT_CONFIG.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot PAGE_SIZE da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, nullable FROM all_tab_columns
-- WHERE owner = 'QLSHX10' AND table_name = 'PMIS_API_ENDPOINT_CONFIG' AND column_name = 'PAGE_SIZE';
