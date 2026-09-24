-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0065_AddInfrastructureListIndexes.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0065_AddInfrastructureListIndexes.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0065_AddInfrastructureListIndexes.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm 4 index (không UNIQUE) phục vụ các câu tra cứu nóng của màn Danh mục Trạm/Đường dây và
-- các tra cứu liên quan (audit PMIS 2026-09-24 — full table scan ~38k dòng INFRASTRUCTURE mỗi lần tải
-- trang, xem comment đầy đủ trong file .cs):
--   1) IX_INFRASTRUCTURE_TYPE_DELETED ON INFRASTRUCTURE (INFRA_TYPE_ID, IsDeleted)
--   2) IX_INFRASTRUCTURE_PARENT_ID    ON INFRASTRUCTURE (PARENT_ID)
--   3) IX_INFRASTRUCTURE_CODE_NORM    ON INFRASTRUCTURE (UPPER(TRIM(CODE)))
--   4) IX_PMIS_EQTYPE_MAPPING_LOOKUP  ON PMIS_EQUIPMENT_TYPE_MAPPING (PmisMaLoaiTB, GridTypeId, IsDeleted)
-- Chỉ CỘNG THÊM, không đụng dữ liệu/constraint hiện có, an toàn chạy trên môi trường đang có dữ liệu.
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng/index đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or
-- view does not exist" (unqualified name sẽ tìm nhầm trong schema của user đang kết nối, không phải
-- QLSHX10), và kiểm tra tồn tại index dùng ALL_INDEXES lọc theo OWNER thay vì USER_INDEXES
-- (USER_INDEXES chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối THẲNG bằng user
-- QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IX_INFRASTRUCTURE_TYPE_DELETED;
--   DROP INDEX QLSHX10.IX_INFRASTRUCTURE_PARENT_ID;
--   DROP INDEX QLSHX10.IX_INFRASTRUCTURE_CODE_NORM;
--   DROP INDEX QLSHX10.IX_PMIS_EQTYPE_MAPPING_LOOKUP;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0065_AddInfrastructureListIndexes%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_INFRASTRUCTURE_TYPE_DELETED';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_INFRASTRUCTURE_TYPE_DELETED ON QLSHX10.INFRASTRUCTURE (INFRA_TYPE_ID, IsDeleted)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_INFRASTRUCTURE_TYPE_DELETED.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_INFRASTRUCTURE_TYPE_DELETED da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_INFRASTRUCTURE_PARENT_ID';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_INFRASTRUCTURE_PARENT_ID ON QLSHX10.INFRASTRUCTURE (PARENT_ID)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_INFRASTRUCTURE_PARENT_ID.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_INFRASTRUCTURE_PARENT_ID da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_INFRASTRUCTURE_CODE_NORM';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_INFRASTRUCTURE_CODE_NORM ON QLSHX10.INFRASTRUCTURE (UPPER(TRIM(CODE)))';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_INFRASTRUCTURE_CODE_NORM.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_INFRASTRUCTURE_CODE_NORM da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_PMIS_EQTYPE_MAPPING_LOOKUP';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_PMIS_EQTYPE_MAPPING_LOOKUP ON QLSHX10.PMIS_EQUIPMENT_TYPE_MAPPING (PmisMaLoaiTB, GridTypeId, IsDeleted)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_PMIS_EQTYPE_MAPPING_LOOKUP.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_PMIS_EQTYPE_MAPPING_LOOKUP da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT index_name, table_name FROM all_indexes WHERE owner = 'QLSHX10'
-- AND index_name IN ('IX_INFRASTRUCTURE_TYPE_DELETED','IX_INFRASTRUCTURE_PARENT_ID','IX_INFRASTRUCTURE_CODE_NORM','IX_PMIS_EQTYPE_MAPPING_LOOKUP');
