-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0053_AddLowVoltageGridType.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0053_AddLowVoltageGridType.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0053_AddLowVoltageGridType.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cấp lưới điện "Hạ áp" (Id = 3) vào GridTypes — trước đây bảng chỉ có 2 dòng
-- (1 Cao áp, 2 Trung áp, seed từ 0006_ModifyEquipmentTypesSchema.sql) nên thiết bị PMIS dưới 1kV
-- (dữ liệu thật trả "0,4kV", "0,22kV") bị dồn nhầm vào Trung áp. Ngưỡng suy luận trong code:
-- ≥ 66kV = Cao áp (1), 1kV–dưới 66kV = Trung áp (2), dưới 1kV = Hạ áp (3).
--
-- ⚠ Chỉ thêm DANH MỤC. Các loại thiết bị hạ áp trong EquipmentTypes do Admin tự tạo (danh mục
-- "Loại thiết bị"), nếu chưa có thì thiết bị 0,4kV đồng bộ về sẽ báo thiếu ánh xạ loại thiết bị.
--
-- ROLLBACK thủ công (chỉ khi chắc chắn không dòng nào đang tham chiếu GridTypeId = 3):
--   DELETE FROM GridTypes WHERE Id = 3;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0053_AddLowVoltageGridType%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM GridTypes WHERE Id = 3;

    IF v_exists = 0 THEN
        INSERT INTO GridTypes (Id, Name) VALUES (3, 'Hạ áp');
        COMMIT;
        DBMS_OUTPUT.PUT_LINE('Da them cap luoi dien Ha ap (Id = 3).');
    ELSE
        DBMS_OUTPUT.PUT_LINE('GridTypes da co dong Id = 3 — bo qua.');
    END IF;
END;
/

-- KIỂM TRA: phải ra đúng 3 dòng 1/2/3.
-- SELECT Id, Name FROM GridTypes ORDER BY Id;
