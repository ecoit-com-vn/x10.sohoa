-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0072_DropLegacyEquipmentsInfraCodeIndex.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0072_DropLegacyEquipmentsInfraCodeIndex.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0072_DropLegacyEquipmentsInfraCodeIndex.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- PHỤ THUỘC: chạy SAU EquipmentService_0059_FixEquipmentsInfraCodeUnique (tạo UX_EQUIPMENTS_ACTIVE_INFRA_CODE).
-- Script TỰ DỪNG (RAISE_APPLICATION_ERROR, không xoá gì) nếu index thay thế đó chưa có.
--
-- NỘI DUNG: xoá UNIQUE INDEX cũ UQ_EQUIPMENTS_INFRA_CODE (INFRASTRUCTURE_ID, CODE). Migration 0059 chỉ DROP
-- CONSTRAINT cùng tên — constraint đã mất nhưng INDEX vẫn còn, không điều kiện, tính cả dòng "hồn ma"
-- (StatusTransition=0) nên chặn mọi lần INSERT lại thiết bị (ORA-00001 lặp lại mỗi lượt đồng bộ — đã gặp
-- thật 2026-10-01: đúng 242 bản ghi thất bại mãi trên production). Chống trùng thật vẫn do
-- UX_EQUIPMENTS_ACTIVE_INFRA_CODE (chỉ tính dòng sống) đảm nhiệm.
--
-- KIỂM TRA TRƯỚC KHI CHẠY (phải thấy index cũ còn, index thay thế có):
--   SELECT index_name, uniqueness FROM all_indexes
--   WHERE owner = 'QLSHX10' AND table_name = 'EQUIPMENTS'
--     AND index_name IN ('UQ_EQUIPMENTS_INFRA_CODE', 'UX_EQUIPMENTS_ACTIVE_INFRA_CODE');
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- LƯU Ý VẬN HÀNH: DROP INDEX cần khoá bảng EQUIPMENTS — DDL_LOCK_TIMEOUT bên dưới chờ tối đa 60 giây nếu có
-- phiên khác đang giữ khoá (vd. lượt đồng bộ đang ghi). Nếu vẫn ORA-00054, chạy lại ở lúc ít tải.
--
-- ROLLBACK thủ công (chỉ chạy được nếu không có 2 dòng cùng (INFRASTRUCTURE_ID, CODE), kể cả dòng hồn ma/đã xoá):
--   CREATE UNIQUE INDEX QLSHX10.UQ_EQUIPMENTS_INFRA_CODE ON QLSHX10.EQUIPMENTS (INFRASTRUCTURE_ID, CODE);
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0072_DropLegacyEquipmentsInfraCodeIndex%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM ALL_INDEXES
    WHERE OWNER = 'QLSHX10' AND INDEX_NAME = 'UX_EQUIPMENTS_ACTIVE_INFRA_CODE' AND UNIQUENESS = 'UNIQUE';
    IF v_exists = 0 THEN
        RAISE_APPLICATION_ERROR(-20001,
            'Khong the xoa UQ_EQUIPMENTS_INFRA_CODE: index thay the UX_EQUIPMENTS_ACTIVE_INFRA_CODE (Migration0059) chua ton tai - bang se mat chong trung. Chay EquipmentService_0059_FixEquipmentsInfraCodeUnique.sql truoc.');
    END IF;

    EXECUTE IMMEDIATE 'ALTER SESSION SET DDL_LOCK_TIMEOUT = 60';

    -- Constraint (nếu còn) phải bỏ trước, nếu không DROP INDEX báo ORA-02429.
    SELECT COUNT(*) INTO v_exists FROM ALL_CONSTRAINTS
    WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'EQUIPMENTS' AND CONSTRAINT_NAME = 'UQ_EQUIPMENTS_INFRA_CODE';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.EQUIPMENTS DROP CONSTRAINT UQ_EQUIPMENTS_INFRA_CODE';
        DBMS_OUTPUT.PUT_LINE('Da bo constraint UQ_EQUIPMENTS_INFRA_CODE.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM ALL_INDEXES
    WHERE OWNER = 'QLSHX10' AND INDEX_NAME = 'UQ_EQUIPMENTS_INFRA_CODE';
    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'DROP INDEX QLSHX10.UQ_EQUIPMENTS_INFRA_CODE';
        DBMS_OUTPUT.PUT_LINE('Da xoa index UQ_EQUIPMENTS_INFRA_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index UQ_EQUIPMENTS_INFRA_CODE khong ton tai - bo qua.');
    END IF;
END;
/

-- KIỂM TRA SAU KHI CHẠY: chỉ còn PK + 2 index "sống" UX_EQUIPMENTS_ACTIVE_INFRA_CODE/UX_EQUIPMENTS_ACTIVE_PMIS_CODE.
-- SELECT index_name, uniqueness FROM all_indexes WHERE owner = 'QLSHX10' AND table_name = 'EQUIPMENTS' AND uniqueness = 'UNIQUE';
