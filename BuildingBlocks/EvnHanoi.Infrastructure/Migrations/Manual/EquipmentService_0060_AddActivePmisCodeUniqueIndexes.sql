-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0060_AddActivePmisCodeUniqueIndexes.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0060_AddActivePmisCodeUniqueIndexes.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0060_AddActivePmisCodeUniqueIndexes.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: chặn trùng PMIS_CODE ở tầng DB cho INFRASTRUCTURE và EQUIPMENTS — trước đây chỉ có index
-- thường (Migration0047/0048), không UNIQUE. Kết hợp PMIS_CODE không chuẩn hoá khi ghi (khoảng trắng/
-- hoa-thường tuỳ lần PMIS trả về), 1 lượt sync có mã trạm lệch nhẹ có thể khiến hệ thống KHÔNG tìm thấy
-- dòng cũ và tự tạo thêm 1 dòng INFRASTRUCTURE trùng cho CÙNG 1 trạm thật — kéo theo TOÀN BỘ thiết bị của
-- trạm đó bị hiểu nhầm "đã chuyển sang trạm mới" (StatusTransition=0) ngay lượt kế tiếp. Đã gặp thật trên
-- production.
--
-- QUAN TRỌNG — CHẠY TRƯỚC KHI ÁP MIGRATION NÀY trên môi trường nghi có trùng (như PROD/staging đang có
-- 1 trạm hiện toàn bộ thiết bị "Đã chuyển TBA"): kiểm tra xem có ≥2 dòng "sống" trùng PMIS_CODE (đã
-- chuẩn hoá) không — NẾU CÓ, script bên dưới SẼ THẤT BẠI có chủ đích (RAISE_APPLICATION_ERROR), không tự
-- gộp/xoá gì. Cần xử lý tay: xác định dòng "chính", chuyển thiết bị của dòng "phụ" (UPDATE
-- EQUIPMENTS.INFRASTRUCTURE_ID) về dòng chính, xoá mềm (IsDeleted=1) dòng phụ, rồi chạy lại script này.
--
--   -- Tìm các PMIS_CODE (đã chuẩn hoá) đang trùng ở INFRASTRUCTURE:
--   SELECT UPPER(TRIM(PMIS_CODE)) AS NormalizedCode, COUNT(*) AS Cnt,
--          LISTAGG(ID, ',') WITHIN GROUP (ORDER BY ID) AS Ids
--   FROM QLSHX10.INFRASTRUCTURE WHERE ISDELETED = 0 AND PMIS_CODE IS NOT NULL
--   GROUP BY UPPER(TRIM(PMIS_CODE)) HAVING COUNT(*) > 1;
--
--   -- Tương tự cho EQUIPMENTS:
--   SELECT UPPER(TRIM(PMIS_CODE)) AS NormalizedCode, COUNT(*) AS Cnt,
--          LISTAGG(ID, ',') WITHIN GROUP (ORDER BY ID) AS Ids
--   FROM QLSHX10.EQUIPMENTS WHERE ISDELETED = 0 AND STATUSTRANSITION IS NULL AND PMIS_CODE IS NOT NULL
--   GROUP BY UPPER(TRIM(PMIS_CODE)) HAVING COUNT(*) > 1;
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.UX_INFRASTRUCTURE_ACTIVE_PMIS_CODE;
--   DROP INDEX QLSHX10.UX_EQUIPMENTS_ACTIVE_PMIS_CODE;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0060_AddActivePmisCodeUniqueIndexes%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

-- AND TRIM(PMIS_CODE) IS NOT NULL: Oracle TRIM('   ') = NULL, tranh GROUP BY gop nham cac dong PMIS_CODE
-- toan khoang trang thanh "trung" gia (unique index that su khong bao gio xung dot tren khoa toan NULL).
DECLARE
    duplicate_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO duplicate_count FROM (
        SELECT UPPER(TRIM(PMIS_CODE)) FROM QLSHX10.INFRASTRUCTURE
        WHERE ISDELETED = 0 AND PMIS_CODE IS NOT NULL AND TRIM(PMIS_CODE) IS NOT NULL
        GROUP BY UPPER(TRIM(PMIS_CODE)) HAVING COUNT(*) > 1
    );
    IF duplicate_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20001,
            'Cannot create active PMIS_CODE unique index on INFRASTRUCTURE (Tram/Duong day): active duplicates exist - merge/soft-delete the duplicate rows first.');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM ALL_INDEXES WHERE OWNER = 'QLSHX10' AND INDEX_NAME = 'UX_INFRASTRUCTURE_ACTIVE_PMIS_CODE';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE UNIQUE INDEX QLSHX10.UX_INFRASTRUCTURE_ACTIVE_PMIS_CODE ON QLSHX10.INFRASTRUCTURE (
                CASE WHEN ISDELETED = 0 AND PMIS_CODE IS NOT NULL THEN UPPER(TRIM(PMIS_CODE)) END
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao unique index UX_INFRASTRUCTURE_ACTIVE_PMIS_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index UX_INFRASTRUCTURE_ACTIVE_PMIS_CODE da ton tai — bo qua.');
    END IF;
END;
/

-- AND TRIM(PMIS_CODE) IS NOT NULL: xem giai thich o pre-check INFRASTRUCTURE ben tren.
DECLARE
    duplicate_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO duplicate_count FROM (
        SELECT UPPER(TRIM(PMIS_CODE)) FROM QLSHX10.EQUIPMENTS
        WHERE ISDELETED = 0 AND STATUSTRANSITION IS NULL AND PMIS_CODE IS NOT NULL AND TRIM(PMIS_CODE) IS NOT NULL
        GROUP BY UPPER(TRIM(PMIS_CODE)) HAVING COUNT(*) > 1
    );
    IF duplicate_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20001,
            'Cannot create active PMIS_CODE unique index on EQUIPMENTS (Thiet bi): active duplicates exist - merge/soft-delete the duplicate rows first.');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM ALL_INDEXES WHERE OWNER = 'QLSHX10' AND INDEX_NAME = 'UX_EQUIPMENTS_ACTIVE_PMIS_CODE';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE UNIQUE INDEX QLSHX10.UX_EQUIPMENTS_ACTIVE_PMIS_CODE ON QLSHX10.EQUIPMENTS (
                CASE WHEN ISDELETED = 0 AND STATUSTRANSITION IS NULL AND PMIS_CODE IS NOT NULL THEN UPPER(TRIM(PMIS_CODE)) END
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao unique index UX_EQUIPMENTS_ACTIVE_PMIS_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index UX_EQUIPMENTS_ACTIVE_PMIS_CODE da ton tai — bo qua.');
    END IF;
END;
/

-- KIỂM TRA:
-- SELECT index_name, uniqueness FROM all_indexes WHERE owner = 'QLSHX10' AND table_name IN ('INFRASTRUCTURE', 'EQUIPMENTS') AND uniqueness = 'UNIQUE';
