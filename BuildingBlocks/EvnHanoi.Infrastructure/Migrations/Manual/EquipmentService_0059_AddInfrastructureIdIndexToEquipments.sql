-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0059_AddInfrastructureIdIndexToEquipments.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0059_AddInfrastructureIdIndexToEquipments.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0059_AddInfrastructureIdIndexToEquipments.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: EQUIPMENTS.INFRASTRUCTURE_ID có FK (fk_equip_infra) nhưng Oracle không tự tạo index cho FK —
-- cột này bị full scan mỗi khi lọc thiết bị theo 1 Trạm/Đường dây, gây chậm/timeout ở
-- PmisDocumentRepository.GetCatalogTreeAsync (đếm tài liệu thiết bị con cho từng INFRASTRUCTURE).
--
-- ROLLBACK thủ công:
--   DROP INDEX IDX_EQUIPMENTS_INFRASTRUCTURE_ID;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0059_AddInfrastructureIdIndexToEquipments%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_indexes WHERE index_name = 'IDX_EQUIPMENTS_INFRASTRUCTURE_ID';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_EQUIPMENTS_INFRASTRUCTURE_ID ON EQUIPMENTS (INFRASTRUCTURE_ID)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IDX_EQUIPMENTS_INFRASTRUCTURE_ID.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_EQUIPMENTS_INFRASTRUCTURE_ID da ton tai — bo qua.');
    END IF;

    COMMIT;
END;
/
