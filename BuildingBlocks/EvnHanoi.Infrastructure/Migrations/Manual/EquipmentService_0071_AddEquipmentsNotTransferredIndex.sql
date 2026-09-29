-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0071_AddEquipmentsNotTransferredIndex.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0071_AddEquipmentsNotTransferredIndex.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0071_AddEquipmentsNotTransferredIndex.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: index hàm IX_EQUIPMENTS_NOT_TRANSFERRED — khớp CHÍNH XÁC biểu thức
-- EquipmentSqlFilters.NotTransferredAway() dùng để loại thiết bị "hồn ma" (StatusTransition=0, "Đã
-- chuyển TBA") khỏi hầu hết mọi câu tra cứu EQUIPMENTS.
--
-- PHÁT HIỆN QUA EXPLAIN PLAN THẬT (audit hiệu năng /equipment/device-list 2026-09-29): điều kiện
-- "(StatusTransition IS NULL OR StatusTransition <> 0)" thực ra RẤT chọn lọc — chỉ ~7.015/160.781 dòng
-- khớp (~4.4%) — NHƯNG Oracle vẫn full table scan vì B-tree index thường không lưu giá trị NULL, nên
-- nhánh "IS NULL" luôn buộc quét toàn bảng. Migration0069 (index UnitId/EquipmentTypeId/IsActive/
-- (IsDeleted,CreatedAt)) không giải quyết được vì không đụng tới StatusTransition.
--
-- GIẢI PHÁP: index hàm trên "CASE WHEN (StatusTransition IS NULL OR StatusTransition <> 0) THEN 1 END"
-- — 95.6% dòng "hồn ma" có CASE trả về NULL nên KHÔNG được lưu vào index (cùng kiểu index hàm CASE WHEN
-- đã dùng ở UX_EQUIPMENTS_ACTIVE_INFRA_CODE) — index chỉ còn ~4.4% dòng "sống". Code đã sửa
-- EquipmentSqlFilters.NotTransferredAway() để sinh ĐÚNG biểu thức "(CASE WHEN (...) THEN 1 END) = 1"
-- khớp index này (Oracle chỉ dùng được function-based index khi WHERE khớp CHÍNH XÁC biểu thức lúc tạo
-- index — PHẢI deploy code mới CÙNG LÚC với chạy migration này, chạy 1 trong 2 riêng lẻ sẽ không có
-- tác dụng: chỉ tạo index mà chưa deploy code thì code cũ vẫn sinh WHERE dạng cũ không khớp index; chỉ
-- deploy code mà chưa tạo index thì chưa có index để dùng).
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng/index đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or
-- view does not exist", và kiểm tra tồn tại index dùng ALL_INDEXES lọc theo OWNER thay vì USER_INDEXES
-- (USER_INDEXES chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối THẲNG bằng user
-- QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IX_EQUIPMENTS_NOT_TRANSFERRED;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0071_AddEquipmentsNotTransferredIndex%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_EQUIPMENTS_NOT_TRANSFERRED';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE INDEX QLSHX10.IX_EQUIPMENTS_NOT_TRANSFERRED ON QLSHX10.EQUIPMENTS (
                CASE WHEN (StatusTransition IS NULL OR StatusTransition <> 0) THEN 1 END
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_EQUIPMENTS_NOT_TRANSFERRED.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_EQUIPMENTS_NOT_TRANSFERRED da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT index_name, status FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_EQUIPMENTS_NOT_TRANSFERRED';
