-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0057_AddParentIdToInfrastructure.cs
-- ============================================================================
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0057_AddParentIdToInfrastructure.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0057_AddParentIdToInfrastructure.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG:
--   Bổ sung cột PARENT_ID và khóa ngoại FK_INFRA_PARENT (tự tham chiếu) vào bảng INFRASTRUCTURE.
--   Phục vụ cấu trúc phân cấp cây cha - con (cấp trạm / tuyến đường dây và các nhánh con)
--   trên màn hình Quản lý đường dây (/catalog/transmission-line).
--
-- ROLLBACK thủ công:
--   ALTER TABLE INFRASTRUCTURE DROP CONSTRAINT FK_INFRA_PARENT;
--   ALTER TABLE INFRASTRUCTURE DROP COLUMN PARENT_ID;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0057_AddParentIdToInfrastructure%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_col_exists NUMBER;
    v_fk_exists  NUMBER;
BEGIN
    -- 1. Thêm cột PARENT_ID nếu chưa có
    SELECT COUNT(*) INTO v_col_exists 
      FROM USER_TAB_COLS
     WHERE TABLE_NAME = 'INFRASTRUCTURE' 
       AND COLUMN_NAME = 'PARENT_ID';

    IF v_col_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INFRASTRUCTURE ADD PARENT_ID VARCHAR2(36) NULL';
        DBMS_OUTPUT.PUT_LINE('Đã thêm cột INFRASTRUCTURE.PARENT_ID');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cột INFRASTRUCTURE.PARENT_ID đã tồn tại, bỏ qua');
    END IF;

    -- 2. Thêm ràng buộc khóa ngoại FK_INFRA_PARENT nếu chưa có
    SELECT COUNT(*) INTO v_fk_exists
      FROM USER_CONSTRAINTS
     WHERE TABLE_NAME = 'INFRASTRUCTURE'
       AND CONSTRAINT_NAME = 'FK_INFRA_PARENT';

    IF v_fk_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE INFRASTRUCTURE ADD CONSTRAINT FK_INFRA_PARENT FOREIGN KEY (PARENT_ID) REFERENCES INFRASTRUCTURE(ID) ON DELETE SET NULL';
        DBMS_OUTPUT.PUT_LINE('Đã thêm khóa ngoại FK_INFRA_PARENT');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Khóa ngoại FK_INFRA_PARENT đã tồn tại, bỏ qua');
    END IF;
END;
/
