-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0069_AddEquipmentListIndexes.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0069_AddEquipmentListIndexes.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0069_AddEquipmentListIndexes.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm 4 index (không UNIQUE) phục vụ EquipmentRepository.GetPagedAsync (màn "Quản lý thiết
-- bị", /equipment/device-list) — trước đây không index nào khớp UnitId/EquipmentTypeId/IsActive, và
-- không index nào khớp thứ tự ORDER BY mặc định, nên mỗi lần tải danh sách (kể cả không lọc gì) đều
-- full table scan + sort toàn bộ EQUIPMENTS, chi phí tăng thẳng theo số dòng của bảng:
--   1) IX_EQUIPMENTS_UNITID          ON EQUIPMENTS (UnitId)
--   2) IX_EQUIPMENTS_EQUIPMENTTYPEID ON EQUIPMENTS (EquipmentTypeId)
--   3) IX_EQUIPMENTS_ISACTIVE        ON EQUIPMENTS (IS_ACTIVE)
--   4) IX_EQUIPMENTS_DELETED_CREATED ON EQUIPMENTS (IsDeleted, CreatedAt) — IsDeleted=0 có mặt ở MỌI
--      câu truy vấn danh sách; ghép thêm CreatedAt cho phép Oracle đọc index theo đúng thứ tự
--      ORDER BY e.CreatedAt DESC thay vì sort riêng toàn bộ tập kết quả.
-- Chỉ CỘNG THÊM, không đụng dữ liệu/constraint hiện có, an toàn chạy trên môi trường đang có dữ liệu.
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng/index đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or
-- view does not exist", và kiểm tra tồn tại index dùng ALL_INDEXES lọc theo OWNER thay vì USER_INDEXES
-- (USER_INDEXES chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối THẲNG bằng user
-- QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IX_EQUIPMENTS_UNITID;
--   DROP INDEX QLSHX10.IX_EQUIPMENTS_EQUIPMENTTYPEID;
--   DROP INDEX QLSHX10.IX_EQUIPMENTS_ISACTIVE;
--   DROP INDEX QLSHX10.IX_EQUIPMENTS_DELETED_CREATED;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0069_AddEquipmentListIndexes%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_EQUIPMENTS_UNITID';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_EQUIPMENTS_UNITID ON QLSHX10.EQUIPMENTS (UnitId)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_EQUIPMENTS_UNITID.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_EQUIPMENTS_UNITID da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_EQUIPMENTS_EQUIPMENTTYPEID';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_EQUIPMENTS_EQUIPMENTTYPEID ON QLSHX10.EQUIPMENTS (EquipmentTypeId)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_EQUIPMENTS_EQUIPMENTTYPEID.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_EQUIPMENTS_EQUIPMENTTYPEID da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_EQUIPMENTS_ISACTIVE';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_EQUIPMENTS_ISACTIVE ON QLSHX10.EQUIPMENTS (IS_ACTIVE)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_EQUIPMENTS_ISACTIVE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_EQUIPMENTS_ISACTIVE da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_EQUIPMENTS_DELETED_CREATED';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_EQUIPMENTS_DELETED_CREATED ON QLSHX10.EQUIPMENTS (IsDeleted, CreatedAt)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_EQUIPMENTS_DELETED_CREATED.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_EQUIPMENTS_DELETED_CREATED da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT index_name, table_name FROM all_indexes WHERE owner = 'QLSHX10'
-- AND index_name IN ('IX_EQUIPMENTS_UNITID','IX_EQUIPMENTS_EQUIPMENTTYPEID','IX_EQUIPMENTS_ISACTIVE','IX_EQUIPMENTS_DELETED_CREATED');
