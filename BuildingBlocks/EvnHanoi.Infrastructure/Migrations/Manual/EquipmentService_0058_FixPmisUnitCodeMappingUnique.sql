-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0058_FixPmisUnitCodeMappingUnique.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0058_FixPmisUnitCodeMappingUnique.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0058_FixPmisUnitCodeMappingUnique.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: sửa lỗi tương tự Migration0054 (PMIS_EQUIPMENT_TYPE_MAPPING) nhưng cho
-- PMIS_UNIT_CODE_MAPPING: UQ_PMIS_UNIT_CODE_MAPPING_CODE (Migration0051) là UNIQUE constraint thường,
-- tính cả dòng đã xoá mềm — sau khi xoá 1 ánh xạ, không thêm lại được đúng mã đơn vị PMIS đó lần nữa
-- (báo trùng dù danh sách hiển thị trống). Thay bằng unique index hàm chỉ tính dòng IsDeleted = 0.
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.UX_PMIS_UNIT_CODE_MAPPING_ACTIVE;
--   ALTER TABLE QLSHX10.PMIS_UNIT_CODE_MAPPING ADD CONSTRAINT UQ_PMIS_UNIT_CODE_MAPPING_CODE UNIQUE (PmisUnitCode);
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0058_FixPmisUnitCodeMappingUnique%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM ALL_CONSTRAINTS
    WHERE OWNER = 'QLSHX10'
      AND CONSTRAINT_NAME = 'UQ_PMIS_UNIT_CODE_MAPPING_CODE'
      AND TABLE_NAME = 'PMIS_UNIT_CODE_MAPPING';

    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.PMIS_UNIT_CODE_MAPPING DROP CONSTRAINT UQ_PMIS_UNIT_CODE_MAPPING_CODE';
        DBMS_OUTPUT.PUT_LINE('Da bo constraint UQ_PMIS_UNIT_CODE_MAPPING_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Constraint UQ_PMIS_UNIT_CODE_MAPPING_CODE khong ton tai — bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM ALL_INDEXES WHERE OWNER = 'QLSHX10' AND INDEX_NAME = 'UX_PMIS_UNIT_CODE_MAPPING_ACTIVE';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE UNIQUE INDEX QLSHX10.UX_PMIS_UNIT_CODE_MAPPING_ACTIVE ON QLSHX10.PMIS_UNIT_CODE_MAPPING (
                CASE WHEN IsDeleted = 0 THEN PmisUnitCode END
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao unique index UX_PMIS_UNIT_CODE_MAPPING_ACTIVE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index UX_PMIS_UNIT_CODE_MAPPING_ACTIVE da ton tai — bo qua.');
    END IF;
END;
/

-- KIỂM TRA:
-- SELECT constraint_name, constraint_type FROM all_constraints WHERE owner = 'QLSHX10' AND table_name = 'PMIS_UNIT_CODE_MAPPING';
-- SELECT index_name, uniqueness FROM all_indexes WHERE owner = 'QLSHX10' AND table_name = 'PMIS_UNIT_CODE_MAPPING';
