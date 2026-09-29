-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/IdentityService/Migration0057_AddOrganizationUnitParentIdIndex.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @IdentityService_0057_AddOrganizationUnitParentIdIndex.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.IdentityService.Migration0057_AddOrganizationUnitParentIdIndex.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm index IX_ORGANIZATION_UNIT_PARENTID (ParentId) — ORGANIZATION_UNIT.ParentId là
-- self-FK dùng cho câu truy vấn phân cấp "CONNECT BY PRIOR Id = ParentId"
-- (DossierSearchService.ApplyUnitScopeAsync, chạy trên MỌI lần tải danh sách hồ sơ có lọc theo đơn vị —
-- cả màn Xuất bản hồ sơ lẫn /my-dossiers), và GetOrganizationUnitsHierarchicalAsync ở nhiều nơi khác —
-- Oracle không tự tạo index cho self-FK, nên mỗi bước duyệt cây phải quét toàn bảng để tìm các con của
-- 1 node. Chỉ CỘNG THÊM, không đụng dữ liệu/constraint hiện có, an toàn chạy trên môi trường đang có dữ liệu.
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng/index đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or
-- view does not exist", và kiểm tra tồn tại index dùng ALL_INDEXES lọc theo OWNER thay vì USER_INDEXES
-- (USER_INDEXES chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối THẲNG bằng user
-- QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IX_ORGANIZATION_UNIT_PARENTID;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0057_AddOrganizationUnitParentIdIndex%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_ORGANIZATION_UNIT_PARENTID';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_ORGANIZATION_UNIT_PARENTID ON QLSHX10.ORGANIZATION_UNIT (ParentId)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_ORGANIZATION_UNIT_PARENTID.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_ORGANIZATION_UNIT_PARENTID da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT index_name, table_name FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_ORGANIZATION_UNIT_PARENTID';
