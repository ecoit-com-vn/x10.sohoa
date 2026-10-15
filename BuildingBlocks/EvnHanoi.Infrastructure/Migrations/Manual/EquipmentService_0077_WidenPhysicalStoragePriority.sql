-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0077_WidenPhysicalStoragePriority.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0077_WidenPhysicalStoragePriority.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0077_WidenPhysicalStoragePriority.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: nới cột PRIORITY trong PHYSICAL_SHELF, PHYSICAL_FLOOR, PHYSICAL_BOX từ NUMBER(3) lên NUMBER(9).
-- Lý do: cột PRIORITY cũ chỉ cho phép tối đa 999; khi import từ dòng 1000 trở đi sẽ gặp lỗi ORA-01438.
--
-- AN TOÀN: nới độ chính xác số (precision) trong Oracle chỉ thay đổi metadata, không ghi lại bảng.
-- Idempotent: chạy lại nhiều lần không lỗi.
-- DDL_LOCK_TIMEOUT chờ tối đa 60 giây nếu có phiên đang ghi bảng.
--
-- SCHEMA: tiền tố "QLSHX10." tường minh. Nếu kết nối bằng chính user QLSHX10 thì tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công (chỉ khi mọi giá trị PRIORITY <= 999):
--   ALTER TABLE QLSHX10.PHYSICAL_SHELF MODIFY (PRIORITY NUMBER(3));
--   ALTER TABLE QLSHX10.PHYSICAL_FLOOR MODIFY (PRIORITY NUMBER(3));
--   ALTER TABLE QLSHX10.PHYSICAL_BOX MODIFY (PRIORITY NUMBER(3));
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0077_WidenPhysicalStoragePriority%';
--   COMMIT;
-- ============================================================================
ALTER SESSION SET DDL_LOCK_TIMEOUT = 60;

ALTER TABLE QLSHX10.PHYSICAL_SHELF MODIFY (PRIORITY NUMBER(9));
ALTER TABLE QLSHX10.PHYSICAL_FLOOR MODIFY (PRIORITY NUMBER(9));
ALTER TABLE QLSHX10.PHYSICAL_BOX MODIFY (PRIORITY NUMBER(9));
