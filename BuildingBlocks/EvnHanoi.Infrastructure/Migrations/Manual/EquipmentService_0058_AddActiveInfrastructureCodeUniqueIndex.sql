-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0058_AddActiveInfrastructureCodeUniqueIndex.cs
-- ============================================================================
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0058_AddActiveInfrastructureCodeUniqueIndex.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0058_AddActiveInfrastructureCodeUniqueIndex.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- MỤC ĐÍCH:
--   Sửa lỗi ORA-00001: unique constraint (QLSHX10.SYS_C0032443) violated khi tạo lại đường dây/trạm biến áp
--   với mã (CODE) đã bị xóa mềm (ISDELETED = 1).
--   Bảng INFRASTRUCTURE lúc tạo ban đầu (Migration0005) dùng "CODE VARCHAR2(100) NOT NULL UNIQUE"
--   khiến Oracle sinh constraint UNIQUE toàn cục (SYS_C0032443).
--   Script này xóa ràng buộc UNIQUE toàn cục và tạo UNIQUE INDEX dạng hàm chỉ áp dụng khi ISDELETED = 0.
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    duplicate_count NUMBER;
    v_index_exists  NUMBER;
BEGIN
    -- 1. Kiểm tra nếu có bản ghi trùng mã trong số các bản ghi đang active (ISDELETED = 0)
    SELECT COUNT(*)
      INTO duplicate_count
      FROM (
          SELECT UPPER(TRIM(CODE))
            FROM QLSHX10.INFRASTRUCTURE
           WHERE ISDELETED = 0
             AND TRIM(CODE) IS NOT NULL
           GROUP BY UPPER(TRIM(CODE))
          HAVING COUNT(*) > 1
      );

    IF duplicate_count > 0 THEN
        RAISE_APPLICATION_ERROR(
            -20001,
            'Cannot create active infrastructure code unique index because active normalized codes are duplicated.');
    END IF;

    -- 2. Tạo Unique Index cho các bản ghi chưa xóa mềm (ISDELETED = 0)
    SELECT COUNT(*)
      INTO v_index_exists
      FROM ALL_INDEXES
     WHERE OWNER = 'QLSHX10'
       AND INDEX_NAME = 'UQ_INFRASTRUCTURE_ACTIVE_CODE';

    IF v_index_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE UNIQUE INDEX QLSHX10.UQ_INFRASTRUCTURE_ACTIVE_CODE
                ON QLSHX10.INFRASTRUCTURE (
                    CASE
                        WHEN ISDELETED = 0 THEN UPPER(TRIM(CODE))
                    END
                )';
        DBMS_OUTPUT.PUT_LINE('Da tao unique index UQ_INFRASTRUCTURE_ACTIVE_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Unique index UQ_INFRASTRUCTURE_ACTIVE_CODE da ton tai - bo qua.');
    END IF;

    -- 3. Xóa các Unique Constraint cũ trên cột CODE (bao gồm SYS_C0032443)
    FOR unique_constraint IN (
        SELECT uc.CONSTRAINT_NAME
          FROM ALL_CONSTRAINTS uc
         WHERE uc.OWNER = 'QLSHX10'
           AND uc.TABLE_NAME = 'INFRASTRUCTURE'
           AND uc.CONSTRAINT_TYPE = 'U'
           AND EXISTS (
               SELECT 1
                 FROM ALL_CONS_COLUMNS ucc
                WHERE ucc.OWNER = uc.OWNER
                  AND ucc.CONSTRAINT_NAME = uc.CONSTRAINT_NAME
                  AND ucc.TABLE_NAME = uc.TABLE_NAME
                  AND ucc.COLUMN_NAME = 'CODE'
           )
           AND NOT EXISTS (
               SELECT 1
                 FROM ALL_CONS_COLUMNS ucc
                WHERE ucc.OWNER = uc.OWNER
                  AND ucc.CONSTRAINT_NAME = uc.CONSTRAINT_NAME
                  AND ucc.TABLE_NAME = uc.TABLE_NAME
                  AND ucc.COLUMN_NAME <> 'CODE'
           )
    ) LOOP
        EXECUTE IMMEDIATE
            'ALTER TABLE QLSHX10.INFRASTRUCTURE DROP CONSTRAINT ' ||
            unique_constraint.CONSTRAINT_NAME;
        DBMS_OUTPUT.PUT_LINE('Da drop constraint: ' || unique_constraint.CONSTRAINT_NAME);
    END LOOP;

    -- 4. Xóa các Unique Index cũ chỉ gồm cột CODE (trừ UQ_INFRASTRUCTURE_ACTIVE_CODE vừa tạo)
    FOR unique_index IN (
        SELECT ui.INDEX_NAME
          FROM ALL_INDEXES ui
         WHERE ui.OWNER = 'QLSHX10'
           AND ui.TABLE_NAME = 'INFRASTRUCTURE'
           AND ui.UNIQUENESS = 'UNIQUE'
           AND ui.INDEX_NAME <> 'UQ_INFRASTRUCTURE_ACTIVE_CODE'
           AND EXISTS (
               SELECT 1
                 FROM ALL_IND_COLUMNS uic
                WHERE uic.INDEX_OWNER = ui.OWNER
                  AND uic.INDEX_NAME = ui.INDEX_NAME
                  AND uic.TABLE_NAME = ui.TABLE_NAME
                  AND uic.COLUMN_NAME = 'CODE'
           )
           AND NOT EXISTS (
               SELECT 1
                 FROM ALL_IND_COLUMNS uic
                WHERE uic.INDEX_OWNER = ui.OWNER
                  AND uic.INDEX_NAME = ui.INDEX_NAME
                  AND uic.TABLE_NAME = ui.TABLE_NAME
                  AND uic.COLUMN_NAME <> 'CODE'
           )
    ) LOOP
        EXECUTE IMMEDIATE
            'DROP INDEX QLSHX10.' ||
            unique_index.INDEX_NAME;
        DBMS_OUTPUT.PUT_LINE('Da drop index: ' || unique_index.INDEX_NAME);
    END LOOP;
END;
/
