-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0070_AddInfrastructureFilterIndexes.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0070_AddInfrastructureFilterIndexes.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0070_AddInfrastructureFilterIndexes.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm 3 index (không UNIQUE) phục vụ InfrastructureRepository.GetPagedAsync (màn "Quản lý
-- trạm biến áp" /catalog/substation và "Quản lý đường dây" /catalog/transmission-line) — Migration0065
-- đã thêm index cho (INFRA_TYPE_ID, IsDeleted) và PARENT_ID, nhưng UNIT_ID/IS_ACTIVE/GRIDTYPEID (lọc
-- theo đơn vị, trạng thái, cấp điện áp — đều là filter phổ biến trên 2 màn này) vẫn chưa có index:
--   1) IX_INFRASTRUCTURE_UNITID    ON INFRASTRUCTURE (UNIT_ID)
--   2) IX_INFRASTRUCTURE_ISACTIVE  ON INFRASTRUCTURE (IS_ACTIVE)
--   3) IX_INFRASTRUCTURE_GRIDTYPEID ON INFRASTRUCTURE (GRIDTYPEID)
-- Chỉ CỘNG THÊM, không đụng dữ liệu/constraint hiện có, an toàn chạy trên môi trường đang có dữ liệu.
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng/index đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or
-- view does not exist", và kiểm tra tồn tại index dùng ALL_INDEXES lọc theo OWNER thay vì USER_INDEXES
-- (USER_INDEXES chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối THẲNG bằng user
-- QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IX_INFRASTRUCTURE_UNITID;
--   DROP INDEX QLSHX10.IX_INFRASTRUCTURE_ISACTIVE;
--   DROP INDEX QLSHX10.IX_INFRASTRUCTURE_GRIDTYPEID;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0070_AddInfrastructureFilterIndexes%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_INFRASTRUCTURE_UNITID';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_INFRASTRUCTURE_UNITID ON QLSHX10.INFRASTRUCTURE (UNIT_ID)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_INFRASTRUCTURE_UNITID.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_INFRASTRUCTURE_UNITID da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_INFRASTRUCTURE_ISACTIVE';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_INFRASTRUCTURE_ISACTIVE ON QLSHX10.INFRASTRUCTURE (IS_ACTIVE)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_INFRASTRUCTURE_ISACTIVE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_INFRASTRUCTURE_ISACTIVE da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_INFRASTRUCTURE_GRIDTYPEID';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_INFRASTRUCTURE_GRIDTYPEID ON QLSHX10.INFRASTRUCTURE (GRIDTYPEID)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_INFRASTRUCTURE_GRIDTYPEID.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_INFRASTRUCTURE_GRIDTYPEID da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT index_name, table_name FROM all_indexes WHERE owner = 'QLSHX10'
-- AND index_name IN ('IX_INFRASTRUCTURE_UNITID','IX_INFRASTRUCTURE_ISACTIVE','IX_INFRASTRUCTURE_GRIDTYPEID');
