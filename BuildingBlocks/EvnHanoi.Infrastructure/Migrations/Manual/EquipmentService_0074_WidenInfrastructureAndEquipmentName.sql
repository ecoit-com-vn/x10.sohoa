-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0074_WidenInfrastructureAndEquipmentName.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0074_WidenInfrastructureAndEquipmentName.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0074_WidenInfrastructureAndEquipmentName.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: nới INFRASTRUCTURE.NAME và EQUIPMENTS.NAME từ VARCHAR2(255 BYTE) lên VARCHAR2(512 CHAR);
-- INFRASTRUCTURE.NORMALIZED_NAME lên VARCHAR2(1000 CHAR).
-- Lý do: đường dây PD-0022D00-DZ1926622 tên ~190 ký tự = 265 BYTE (dấu tiếng Việt 2–3 byte) → ORA-12899
-- lặp lại ở mọi lượt đồng bộ Đường dây (production 2026-10-05).
--
-- AN TOÀN: chỉ đổi metadata (không viết lại dữ liệu), giữ NOT NULL/DEFAULT; không có index/view phụ thuộc.
-- Chạy lại nhiều lần không lỗi. DDL_LOCK_TIMEOUT chờ tối đa 60 giây nếu có phiên đang ghi bảng (nếu vẫn
-- ORA-00054, chạy lại lúc ít tải).
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công (chỉ chạy được nếu KHÔNG có dòng nào dài hơn 255 byte — nếu không sẽ ORA-01441):
--   ALTER TABLE QLSHX10.INFRASTRUCTURE MODIFY (NAME VARCHAR2(255 BYTE));
--   ALTER TABLE QLSHX10.INFRASTRUCTURE MODIFY (NORMALIZED_NAME VARCHAR2(500 BYTE));
--   ALTER TABLE QLSHX10.EQUIPMENTS MODIFY (NAME VARCHAR2(255 BYTE));
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0074_WidenInfrastructureAndEquipmentName%';
--   COMMIT;
-- ============================================================================
ALTER SESSION SET DDL_LOCK_TIMEOUT = 60;

ALTER TABLE QLSHX10.INFRASTRUCTURE MODIFY (NAME VARCHAR2(512 CHAR));
ALTER TABLE QLSHX10.INFRASTRUCTURE MODIFY (NORMALIZED_NAME VARCHAR2(1000 CHAR));
ALTER TABLE QLSHX10.EQUIPMENTS MODIFY (NAME VARCHAR2(512 CHAR));

-- KIỂM TRA SAU KHI CHẠY: cả 3 dòng phải có CHAR_USED = 'C' và CHAR_LENGTH lần lượt 512 / 1000 / 512.
-- SELECT TABLE_NAME, COLUMN_NAME, CHAR_LENGTH, CHAR_USED, NULLABLE FROM ALL_TAB_COLUMNS
-- WHERE OWNER = 'QLSHX10' AND ((TABLE_NAME = 'INFRASTRUCTURE' AND COLUMN_NAME IN ('NAME', 'NORMALIZED_NAME'))
--    OR (TABLE_NAME = 'EQUIPMENTS' AND COLUMN_NAME = 'NAME'));
