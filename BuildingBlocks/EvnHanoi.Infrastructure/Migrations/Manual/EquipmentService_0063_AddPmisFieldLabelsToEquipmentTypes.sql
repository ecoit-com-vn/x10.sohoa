-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0063_AddPmisFieldLabelsToEquipmentTypes.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0063_AddPmisFieldLabelsToEquipmentTypes.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0063_AddPmisFieldLabelsToEquipmentTypes.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cột EquipmentTypes.PmisFieldLabels (CLOB) — bản sao "theo LOẠI thiết bị" của
-- EQUIPMENT_PMIS_SPEC.FieldLabels (Migration0062, lưu theo TỪNG thiết bị) — nhãn tiếng Việt của 1 khoá
-- thongSoKyThuat là hằng số theo loại thiết bị, ghi 1 lần/loại thay vì lặp lại theo từng thiết bị.
-- Không backfill dữ liệu cũ (chỉ có từ lần đồng bộ tiếp theo).
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   ALTER TABLE QLSHX10.EquipmentTypes DROP COLUMN PmisFieldLabels;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0063_AddPmisFieldLabelsToEquipmentTypes%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM ALL_TAB_COLUMNS
    WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'EQUIPMENTTYPES' AND COLUMN_NAME = 'PMISFIELDLABELS';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.EquipmentTypes ADD (PmisFieldLabels CLOB NULL)';
        DBMS_OUTPUT.PUT_LINE('Da them cot EquipmentTypes.PmisFieldLabels.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot EquipmentTypes.PmisFieldLabels da ton tai — bo qua.');
    END IF;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type FROM all_tab_columns WHERE owner = 'QLSHX10' AND table_name = 'EQUIPMENTTYPES' ORDER BY column_id;
