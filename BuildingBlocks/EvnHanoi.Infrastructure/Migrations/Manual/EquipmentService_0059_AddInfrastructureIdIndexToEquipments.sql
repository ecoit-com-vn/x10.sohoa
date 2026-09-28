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
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IDX_EQUIPMENTS_INFRASTRUCTURE_ID;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0059_AddInfrastructureIdIndexToEquipments%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM ALL_INDEXES WHERE OWNER = 'QLSHX10' AND INDEX_NAME = 'IDX_EQUIPMENTS_INFRASTRUCTURE_ID';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IDX_EQUIPMENTS_INFRASTRUCTURE_ID ON QLSHX10.EQUIPMENTS (INFRASTRUCTURE_ID)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IDX_EQUIPMENTS_INFRASTRUCTURE_ID.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_EQUIPMENTS_INFRASTRUCTURE_ID da ton tai — bo qua.');
    END IF;

    COMMIT;
END;
/
