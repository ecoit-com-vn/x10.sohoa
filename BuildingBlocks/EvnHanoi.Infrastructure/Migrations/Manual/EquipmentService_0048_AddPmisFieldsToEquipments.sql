-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0048_AddPmisFieldsToEquipments.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0048_AddPmisFieldsToEquipments.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0048_AddPmisFieldsToEquipments.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: EQUIPMENTS.PMIS_CODE (khoá map maTB), QR_CODE (CLOB, base64 mã QR do PMIS cấp),
-- LAST_SYNCED_FROM_PMIS_AT.
-- ROLLBACK thủ công:
--   DROP INDEX IDX_EQUIPMENTS_PMIS_CODE;
--   ALTER TABLE EQUIPMENTS DROP COLUMN LAST_SYNCED_FROM_PMIS_AT;
--   ALTER TABLE EQUIPMENTS DROP COLUMN QR_CODE;
--   ALTER TABLE EQUIPMENTS DROP COLUMN PMIS_CODE;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0048_AddPmisFieldsToEquipments%';
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tab_cols
    WHERE table_name = 'EQUIPMENTS' AND column_name = 'PMIS_CODE';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE EQUIPMENTS ADD (PMIS_CODE VARCHAR2(100) NULL, QR_CODE CLOB NULL, LAST_SYNCED_FROM_PMIS_AT TIMESTAMP NULL)';
        DBMS_OUTPUT.PUT_LINE('Đã thêm cột EQUIPMENTS.PMIS_CODE / QR_CODE / LAST_SYNCED_FROM_PMIS_AT');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cột EQUIPMENTS.PMIS_CODE đã tồn tại, bỏ qua');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_indexes WHERE index_name = 'IDX_EQUIPMENTS_PMIS_CODE';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_EQUIPMENTS_PMIS_CODE ON EQUIPMENTS (PMIS_CODE)';
        DBMS_OUTPUT.PUT_LINE('Đã tạo IDX_EQUIPMENTS_PMIS_CODE');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_EQUIPMENTS_PMIS_CODE đã tồn tại, bỏ qua');
    END IF;

    DBMS_OUTPUT.PUT_LINE('Hoàn tất.');
END;
/
