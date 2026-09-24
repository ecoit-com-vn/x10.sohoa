-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0067_AddNormalizedSearchColumnsToInfrastructure.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0067_AddNormalizedSearchColumnsToInfrastructure.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0067_AddNormalizedSearchColumnsToInfrastructure.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm 2 cột NORMALIZED_CODE/NORMALIZED_NAME vào INFRASTRUCTURE — ghi sẵn kết quả bỏ dấu tiếng
-- Việt (chữ thường) lúc upsert/tạo/sửa, thay vì tính 268 hàm REPLACE() lồng nhau mỗi lần có keyword tìm
-- kiếm trên màn Danh mục Trạm/Đường dây (audit hiệu năng PMIS 2026-09-24). LƯU Ý: KHÔNG giải quyết gốc
-- full-table-scan (vẫn LIKE '%...%' wildcard đầu), chỉ giảm chi phí CPU/dòng.
--
-- ⚠️ CẢNH BÁO QUAN TRỌNG VỀ CÁCH BACKFILL — ĐÃ TỰ THỬ VÀ THẤT BẠI 1 CÁCH TRƯỚC KHI CHỌN CÁCH NÀY:
-- Bản đầu tiên của migration này dùng 1 câu UPDATE SET-BASED với REPLACE() lồng 61 tầng ngay trong SQL
-- (Oracle tự tính server-side, tưởng sẽ nhanh nhất vì chỉ 1 round-trip/lô) — khi test THẬT trên Oracle dev
-- (192.168.1.199), câu này TREO >14 PHÚT không tiến triển (dù cùng 1 pattern phân trang ROWID/FETCH FIRST
-- vốn chỉ mất ~360ms/500 dòng với 1 hàm LOWER() đơn giản — nghi vấn cao là Oracle SQL parser/optimizer xử
-- lý cực chậm với expression lồng quá sâu, KHÔNG PHẢI do tải server hay pattern phân trang). Bản dưới đây
-- dùng PL/SQL với BULK COLLECT + FORALL và HÀM RIÊNG gán REPLACE TUẦN TỰ (không lồng vào 1 biểu thức) —
-- tránh hoàn toàn vấn đề trên, đã kiểm chứng hướng tương đương (tính ở tầng ứng dụng + ghi theo lô) chỉ
-- mất ~4.3 giây cho toàn bộ ~38.815 dòng khi chạy qua .NET (xem Migration0067.cs).
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IX_INFRASTRUCTURE_NORM_CODE;
--   ALTER TABLE QLSHX10.INFRASTRUCTURE DROP COLUMN NORMALIZED_CODE;
--   ALTER TABLE QLSHX10.INFRASTRUCTURE DROP COLUMN NORMALIZED_NAME;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0067_AddNormalizedSearchColumnsToInfrastructure%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_tab_columns
    WHERE owner = 'QLSHX10' AND table_name = 'INFRASTRUCTURE' AND column_name = 'NORMALIZED_CODE';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.INFRASTRUCTURE ADD NORMALIZED_CODE VARCHAR2(100) NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot NORMALIZED_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot NORMALIZED_CODE da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_tab_columns
    WHERE owner = 'QLSHX10' AND table_name = 'INFRASTRUCTURE' AND column_name = 'NORMALIZED_NAME';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.INFRASTRUCTURE ADD NORMALIZED_NAME VARCHAR2(500) NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot NORMALIZED_NAME.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot NORMALIZED_NAME da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_INFRASTRUCTURE_NORM_CODE';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_INFRASTRUCTURE_NORM_CODE ON QLSHX10.INFRASTRUCTURE (NORMALIZED_CODE)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_INFRASTRUCTURE_NORM_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_INFRASTRUCTURE_NORM_CODE da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- BACKFILL — hàm bỏ dấu gán REPLACE TUẦN TỰ (không lồng vào 1 biểu thức, tránh vấn đề nêu trên) + BULK
-- COLLECT/FORALL theo lô 2000 dòng, idempotent (chỉ nhắm dòng NORMALIZED_CODE còn NULL). An toàn chạy lại
-- nếu bị ngắt giữa chừng.
DECLARE
    FUNCTION remove_diacritics(p_text IN VARCHAR2) RETURN VARCHAR2 IS
        v_result VARCHAR2(4000) := NVL(p_text, ' ');
    BEGIN
        v_result := REPLACE(v_result, 'đ', 'd'); v_result := REPLACE(v_result, 'Đ', 'd');
        v_result := REPLACE(v_result, 'à', 'a'); v_result := REPLACE(v_result, 'á', 'a'); v_result := REPLACE(v_result, 'ả', 'a'); v_result := REPLACE(v_result, 'ã', 'a'); v_result := REPLACE(v_result, 'ạ', 'a');
        v_result := REPLACE(v_result, 'ă', 'a'); v_result := REPLACE(v_result, 'ắ', 'a'); v_result := REPLACE(v_result, 'ằ', 'a'); v_result := REPLACE(v_result, 'ẳ', 'a'); v_result := REPLACE(v_result, 'ẵ', 'a'); v_result := REPLACE(v_result, 'ặ', 'a');
        v_result := REPLACE(v_result, 'â', 'a'); v_result := REPLACE(v_result, 'ấ', 'a'); v_result := REPLACE(v_result, 'ầ', 'a'); v_result := REPLACE(v_result, 'ẩ', 'a'); v_result := REPLACE(v_result, 'ẫ', 'a'); v_result := REPLACE(v_result, 'ậ', 'a');
        v_result := REPLACE(v_result, 'è', 'e'); v_result := REPLACE(v_result, 'é', 'e'); v_result := REPLACE(v_result, 'ẻ', 'e'); v_result := REPLACE(v_result, 'ẽ', 'e'); v_result := REPLACE(v_result, 'ẹ', 'e');
        v_result := REPLACE(v_result, 'ê', 'e'); v_result := REPLACE(v_result, 'ế', 'e'); v_result := REPLACE(v_result, 'ề', 'e'); v_result := REPLACE(v_result, 'ể', 'e'); v_result := REPLACE(v_result, 'ễ', 'e'); v_result := REPLACE(v_result, 'ệ', 'e');
        v_result := REPLACE(v_result, 'ì', 'i'); v_result := REPLACE(v_result, 'í', 'i'); v_result := REPLACE(v_result, 'ỉ', 'i'); v_result := REPLACE(v_result, 'ĩ', 'i'); v_result := REPLACE(v_result, 'ị', 'i');
        v_result := REPLACE(v_result, 'ò', 'o'); v_result := REPLACE(v_result, 'ó', 'o'); v_result := REPLACE(v_result, 'ỏ', 'o'); v_result := REPLACE(v_result, 'õ', 'o'); v_result := REPLACE(v_result, 'ọ', 'o');
        v_result := REPLACE(v_result, 'ô', 'o'); v_result := REPLACE(v_result, 'ố', 'o'); v_result := REPLACE(v_result, 'ồ', 'o'); v_result := REPLACE(v_result, 'ổ', 'o'); v_result := REPLACE(v_result, 'ỗ', 'o'); v_result := REPLACE(v_result, 'ộ', 'o');
        v_result := REPLACE(v_result, 'ơ', 'o'); v_result := REPLACE(v_result, 'ớ', 'o'); v_result := REPLACE(v_result, 'ờ', 'o'); v_result := REPLACE(v_result, 'ở', 'o'); v_result := REPLACE(v_result, 'ỡ', 'o'); v_result := REPLACE(v_result, 'ợ', 'o');
        v_result := REPLACE(v_result, 'ù', 'u'); v_result := REPLACE(v_result, 'ú', 'u'); v_result := REPLACE(v_result, 'ủ', 'u'); v_result := REPLACE(v_result, 'ũ', 'u'); v_result := REPLACE(v_result, 'ụ', 'u');
        v_result := REPLACE(v_result, 'ư', 'u'); v_result := REPLACE(v_result, 'ứ', 'u'); v_result := REPLACE(v_result, 'ừ', 'u'); v_result := REPLACE(v_result, 'ử', 'u'); v_result := REPLACE(v_result, 'ữ', 'u'); v_result := REPLACE(v_result, 'ự', 'u');
        v_result := REPLACE(v_result, 'ỳ', 'y'); v_result := REPLACE(v_result, 'ý', 'y'); v_result := REPLACE(v_result, 'ỷ', 'y'); v_result := REPLACE(v_result, 'ỹ', 'y'); v_result := REPLACE(v_result, 'ỵ', 'y');
        RETURN LOWER(v_result);
    END;

    TYPE t_id_tab IS TABLE OF INFRASTRUCTURE.ID%TYPE;
    TYPE t_code_tab IS TABLE OF INFRASTRUCTURE.CODE%TYPE;
    TYPE t_name_tab IS TABLE OF INFRASTRUCTURE.NAME%TYPE;

    v_ids t_id_tab;
    v_codes t_code_tab;
    v_names t_name_tab;
    v_norm_codes t_code_tab;
    v_norm_names t_name_tab;

    CURSOR c_pending IS SELECT ID, CODE, NAME FROM INFRASTRUCTURE WHERE NORMALIZED_CODE IS NULL;
BEGIN
    OPEN c_pending;
    LOOP
        FETCH c_pending BULK COLLECT INTO v_ids, v_codes, v_names LIMIT 2000;
        EXIT WHEN v_ids.COUNT = 0;

        v_norm_codes := v_codes;
        v_norm_names := v_names;
        FOR i IN 1 .. v_ids.COUNT LOOP
            v_norm_codes(i) := remove_diacritics(v_codes(i));
            v_norm_names(i) := remove_diacritics(v_names(i));
        END LOOP;

        FORALL i IN 1 .. v_ids.COUNT
            UPDATE INFRASTRUCTURE SET NORMALIZED_CODE = v_norm_codes(i), NORMALIZED_NAME = v_norm_names(i) WHERE ID = v_ids(i);

        COMMIT;
        DBMS_OUTPUT.PUT_LINE('Backfill: da cap nhat ' || v_ids.COUNT || ' dong.');

        EXIT WHEN c_pending%NOTFOUND;
    END LOOP;
    CLOSE c_pending;
END;
/

-- KIỂM TRA:
-- SELECT COUNT(*) AS TongSo, COUNT(NORMALIZED_CODE) AS DaBackfill FROM QLSHX10.INFRASTRUCTURE;
-- -- 2 số phải bằng nhau (mọi dòng đều có NORMALIZED_CODE, kể cả rỗng nếu CODE gốc null).
