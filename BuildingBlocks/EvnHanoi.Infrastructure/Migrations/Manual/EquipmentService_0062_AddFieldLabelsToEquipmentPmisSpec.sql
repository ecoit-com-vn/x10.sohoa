-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0062_AddFieldLabelsToEquipmentPmisSpec.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0062_AddFieldLabelsToEquipmentPmisSpec.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0062_AddFieldLabelsToEquipmentPmisSpec.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cột EQUIPMENT_PMIS_SPEC.FieldLabels (CLOB) lưu nhãn tiếng Việt của từng khoá thông số
-- kỹ thuật PMIS (field "tenThongSoKyThuat", PMIS bổ sung 2026-09-23) — song song với FormValues (giá
-- trị, field "thongSoKyThuat") đã có sẵn. Không backfill dữ liệu cũ (chỉ có từ lần đồng bộ tiếp theo).
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   ALTER TABLE QLSHX10.EQUIPMENT_PMIS_SPEC DROP COLUMN FieldLabels;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0062_AddFieldLabelsToEquipmentPmisSpec%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM ALL_TAB_COLUMNS
    WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'EQUIPMENT_PMIS_SPEC' AND COLUMN_NAME = 'FIELDLABELS';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.EQUIPMENT_PMIS_SPEC ADD (FieldLabels CLOB NULL)';
        DBMS_OUTPUT.PUT_LINE('Da them cot EQUIPMENT_PMIS_SPEC.FieldLabels.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot EQUIPMENT_PMIS_SPEC.FieldLabels da ton tai — bo qua.');
    END IF;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type FROM all_tab_columns WHERE owner = 'QLSHX10' AND table_name = 'EQUIPMENT_PMIS_SPEC' ORDER BY column_id;
