-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0047_AddPmisFieldsToInfrastructure.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql (cùng lý do:
-- DatabaseMigrationHelper lọc theo tên resource, và OracleDatabaseWithSemicolonDelimiter xé khối
-- PL/SQL theo dấu ';' nếu đặt trực tiếp vào thư mục migration tự động).
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0047_AddPmisFieldsToInfrastructure.sql
-- Sau khi chạy tay, ghi journal để bản .cs không chạy lại:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0047_AddPmisFieldsToInfrastructure.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: INFRASTRUCTURE.PMIS_CODE (khoá map maTBA/maDuongDay) + LAST_SYNCED_FROM_PMIS_AT.
-- ROLLBACK thủ công:
--   DROP INDEX IDX_INFRASTRUCTURE_PMIS_CODE;
--   ALTER TABLE INFRASTRUCTURE DROP COLUMN LAST_SYNCED_FROM_PMIS_AT;
--   ALTER TABLE INFRASTRUCTURE DROP COLUMN PMIS_CODE;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0047_AddPmisFieldsToInfrastructure%';
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tab_cols
    WHERE table_name = 'INFRASTRUCTURE' AND column_name = 'PMIS_CODE';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INFRASTRUCTURE ADD (PMIS_CODE VARCHAR2(100) NULL, LAST_SYNCED_FROM_PMIS_AT TIMESTAMP NULL)';
        DBMS_OUTPUT.PUT_LINE('Đã thêm cột INFRASTRUCTURE.PMIS_CODE / LAST_SYNCED_FROM_PMIS_AT');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cột INFRASTRUCTURE.PMIS_CODE đã tồn tại, bỏ qua');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_indexes WHERE index_name = 'IDX_INFRASTRUCTURE_PMIS_CODE';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_INFRASTRUCTURE_PMIS_CODE ON INFRASTRUCTURE (PMIS_CODE)';
        DBMS_OUTPUT.PUT_LINE('Đã tạo IDX_INFRASTRUCTURE_PMIS_CODE');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_INFRASTRUCTURE_PMIS_CODE đã tồn tại, bỏ qua');
    END IF;

    DBMS_OUTPUT.PUT_LINE('Hoàn tất.');
END;
/
