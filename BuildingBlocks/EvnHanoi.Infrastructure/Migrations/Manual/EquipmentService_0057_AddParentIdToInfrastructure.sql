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
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   ALTER TABLE QLSHX10.INFRASTRUCTURE DROP CONSTRAINT FK_INFRA_PARENT;
--   ALTER TABLE QLSHX10.INFRASTRUCTURE DROP COLUMN PARENT_ID;
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
      FROM ALL_TAB_COLS
     WHERE OWNER = 'QLSHX10'
       AND TABLE_NAME = 'INFRASTRUCTURE'
       AND COLUMN_NAME = 'PARENT_ID';

    IF v_col_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.INFRASTRUCTURE ADD PARENT_ID VARCHAR2(36) NULL';
        DBMS_OUTPUT.PUT_LINE('Đã thêm cột INFRASTRUCTURE.PARENT_ID');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cột INFRASTRUCTURE.PARENT_ID đã tồn tại, bỏ qua');
    END IF;

    -- 2. Thêm ràng buộc khóa ngoại FK_INFRA_PARENT nếu chưa có
    SELECT COUNT(*) INTO v_fk_exists
      FROM ALL_CONSTRAINTS
     WHERE OWNER = 'QLSHX10'
       AND TABLE_NAME = 'INFRASTRUCTURE'
       AND CONSTRAINT_NAME = 'FK_INFRA_PARENT';

    IF v_fk_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.INFRASTRUCTURE ADD CONSTRAINT FK_INFRA_PARENT FOREIGN KEY (PARENT_ID) REFERENCES QLSHX10.INFRASTRUCTURE(ID) ON DELETE SET NULL';
        DBMS_OUTPUT.PUT_LINE('Đã thêm khóa ngoại FK_INFRA_PARENT');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Khóa ngoại FK_INFRA_PARENT đã tồn tại, bỏ qua');
    END IF;
END;
/
