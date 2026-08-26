-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0054_FixPmisEquipmentTypeMappingUnique.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0054_FixPmisEquipmentTypeMappingUnique.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0054_FixPmisEquipmentTypeMappingUnique.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- PHỤ THUỘC: chạy SAU EquipmentService_0052_CreatePmisEquipmentTypeMapping.sql.
--
-- NỘI DUNG: sửa lỗi UQ_PMIS_EQUIPMENT_TYPE_MAPPING (tạo ở 0052) tính cả dòng đã xoá mềm, khiến sau khi
-- Admin xoá 1 ánh xạ thì KHÔNG thêm lại được đúng cặp (mã loại thiết bị PMIS + cấp điện áp) đó — danh
-- sách trống mà hệ thống vẫn báo "đã được ánh xạ". Thay bằng unique index hàm chỉ tính dòng
-- IsDeleted = 0: khi IsDeleted = 1 cả 2 biểu thức đều NULL, Oracle bỏ qua khoá toàn NULL trong unique
-- index nên các dòng đã xoá không chặn nhau nữa, còn trùng thật vẫn bị chặn.
--
-- Ghi chú: PMIS_UNIT_CODE_MAPPING (0051) có cùng dạng ràng buộc nhưng chưa có API xoá nên chưa gặp lỗi
-- này — nếu sau này bổ sung màn quản lý ánh xạ đơn vị thì xử lý y hệt.
--
-- ROLLBACK thủ công:
--   DROP INDEX UX_PMIS_EQTYPE_MAPPING_ACTIVE;
--   ALTER TABLE PMIS_EQUIPMENT_TYPE_MAPPING
--     ADD CONSTRAINT UQ_PMIS_EQUIPMENT_TYPE_MAPPING UNIQUE (PmisMaLoaiTB, GridTypeId);
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0054_FixPmisEquipmentTypeMappingUnique%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM user_constraints
    WHERE constraint_name = 'UQ_PMIS_EQUIPMENT_TYPE_MAPPING'
      AND table_name = 'PMIS_EQUIPMENT_TYPE_MAPPING';

    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE PMIS_EQUIPMENT_TYPE_MAPPING DROP CONSTRAINT UQ_PMIS_EQUIPMENT_TYPE_MAPPING';
        DBMS_OUTPUT.PUT_LINE('Da bo constraint UQ_PMIS_EQUIPMENT_TYPE_MAPPING.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Constraint UQ_PMIS_EQUIPMENT_TYPE_MAPPING khong ton tai — bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM user_indexes WHERE index_name = 'UX_PMIS_EQTYPE_MAPPING_ACTIVE';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE UNIQUE INDEX UX_PMIS_EQTYPE_MAPPING_ACTIVE ON PMIS_EQUIPMENT_TYPE_MAPPING (
                CASE WHEN IsDeleted = 0 THEN PmisMaLoaiTB END,
                CASE WHEN IsDeleted = 0 THEN GridTypeId END
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao unique index UX_PMIS_EQTYPE_MAPPING_ACTIVE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index UX_PMIS_EQTYPE_MAPPING_ACTIVE da ton tai — bo qua.');
    END IF;
END;
/

-- KIỂM TRA: chỉ còn PK + unique index mới, không còn UQ_PMIS_EQUIPMENT_TYPE_MAPPING.
-- SELECT constraint_name, constraint_type FROM user_constraints
-- WHERE table_name = 'PMIS_EQUIPMENT_TYPE_MAPPING';
-- SELECT index_name, uniqueness FROM user_indexes WHERE table_name = 'PMIS_EQUIPMENT_TYPE_MAPPING';
