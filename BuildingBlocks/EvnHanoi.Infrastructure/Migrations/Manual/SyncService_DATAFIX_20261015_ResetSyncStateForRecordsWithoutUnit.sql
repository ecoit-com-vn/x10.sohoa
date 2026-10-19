-- ============================================================================
-- SCRIPT SỬA DỮ LIỆU THỦ CÔNG (không phải migration DbUp): xoá dòng PMIS_SYNC_STATE của Trạm / Đường dây / Thiết bị đang TRỐNG ĐƠN VỊ
-- (UNIT_ID/UNITID IS NULL) để lượt đồng bộ kế tiếp coi là bản ghi mới/đổi và đẩy lại — nhận đơn vị theo ánh xạ PMIS_UNIT_CODE_MAPPING vừa thêm
-- (HN1100 → Thường Tín, PD6800 → X6). Đồng bộ tăng dần bỏ qua bản ghi cùng mã băm nên nếu không xoá, đơn vị chỉ cập nhật sau đợt quét đầy đủ 7 ngày.
--  - SUBSTATION / TRANSMISSION_LINE: dòng của Trạm / Đường dây trống đơn vị.
--  - EQUIPMENT: dòng của Thiết bị trống đơn vị.
--  - PARENT_SCAN: dòng quét của các Trạm/Đường dây CHA có chứa thiết bị trống đơn vị (để cha được quét lại sớm — ưu tiên "chưa quét").
-- Chỉ xoá trạng thái đồng bộ (dữ liệu nghiệp vụ không đổi). Có sao lưu các dòng bị xoá vào QLSHX10.PMIS_SYNC_STATE_BAK_20261015.
--
-- ROLLBACK (khôi phục dòng trạng thái đã xoá — chỉ những dòng hiện không còn tồn tại):
--   INSERT INTO QLSHX10.PMIS_SYNC_STATE SELECT b.* FROM QLSHX10.PMIS_SYNC_STATE_BAK_20261015 b
--    WHERE NOT EXISTS (SELECT 1 FROM QLSHX10.PMIS_SYNC_STATE s WHERE s.OBJECT_TYPE = b.OBJECT_TYPE AND s.PMIS_CODE = b.PMIS_CODE);  COMMIT;
--   (xoá bảng sao lưu khi đã xác nhận: DROP TABLE QLSHX10.PMIS_SYNC_STATE_BAK_20261015 PURGE;)
-- ============================================================================
SET SERVEROUTPUT ON

-- Khối 1: sao lưu. Tạo bảng nếu chưa có, rồi LUÔN bổ sung các dòng đang thuộc phạm vi (trống đơn vị) mà chưa được sao lưu — chạy lại sau khi thêm ánh xạ
-- đơn vị mới vẫn xử lý được phần còn trống.
DECLARE
    v_count NUMBER;
    v_added NUMBER;
    c_criteria CONSTANT VARCHAR2(4000) := '
        (s.OBJECT_TYPE = ''SUBSTATION'' AND s.PMIS_CODE IN (SELECT PMIS_CODE FROM QLSHX10.INFRASTRUCTURE WHERE INFRA_TYPE_ID = 1 AND ISDELETED = 0 AND UNIT_ID IS NULL AND PMIS_CODE IS NOT NULL))
        OR (s.OBJECT_TYPE = ''TRANSMISSION_LINE'' AND s.PMIS_CODE IN (SELECT PMIS_CODE FROM QLSHX10.INFRASTRUCTURE WHERE INFRA_TYPE_ID = 2 AND ISDELETED = 0 AND UNIT_ID IS NULL AND PMIS_CODE IS NOT NULL))
        OR (s.OBJECT_TYPE = ''EQUIPMENT'' AND s.PMIS_CODE IN (SELECT PMIS_CODE FROM QLSHX10.EQUIPMENTS WHERE ISDELETED = 0 AND UNITID IS NULL AND PMIS_CODE IS NOT NULL))
        OR (s.OBJECT_TYPE = ''PARENT_SCAN'' AND s.PMIS_CODE IN (SELECT i.PMIS_CODE FROM QLSHX10.INFRASTRUCTURE i WHERE i.PMIS_CODE IS NOT NULL
               AND EXISTS (SELECT 1 FROM QLSHX10.EQUIPMENTS e WHERE e.INFRASTRUCTURE_ID = i.ID AND e.ISDELETED = 0 AND e.UNITID IS NULL)))';
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_TABLES WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'PMIS_SYNC_STATE_BAK_20261015';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TABLE QLSHX10.PMIS_SYNC_STATE_BAK_20261015 AS SELECT s.* FROM QLSHX10.PMIS_SYNC_STATE s WHERE 1 = 0';
        DBMS_OUTPUT.PUT_LINE('Đã tạo bảng sao lưu PMIS_SYNC_STATE_BAK_20261015.');
    END IF;
    EXECUTE IMMEDIATE 'INSERT INTO QLSHX10.PMIS_SYNC_STATE_BAK_20261015
        SELECT s.* FROM QLSHX10.PMIS_SYNC_STATE s
         WHERE (' || c_criteria || ')
           AND NOT EXISTS (SELECT 1 FROM QLSHX10.PMIS_SYNC_STATE_BAK_20261015 b WHERE b.OBJECT_TYPE = s.OBJECT_TYPE AND b.PMIS_CODE = s.PMIS_CODE)';
    v_added := SQL%ROWCOUNT;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Đã sao lưu thêm ' || v_added || ' dòng PMIS_SYNC_STATE.');
END;
/

-- Khối 2: xoá các dòng ĐANG thuộc phạm vi (tiêu chí hiện tại) và đã có trong bản sao lưu (khoá OBJECT_TYPE + PMIS_CODE), theo từng loại.
DECLARE
    v_n NUMBER;
    v_total NUMBER := 0;
BEGIN
    FOR r IN (SELECT DISTINCT OBJECT_TYPE FROM QLSHX10.PMIS_SYNC_STATE_BAK_20261015 ORDER BY 1) LOOP
        DELETE FROM QLSHX10.PMIS_SYNC_STATE s
         WHERE s.OBJECT_TYPE = r.OBJECT_TYPE
           AND s.PMIS_CODE IN (SELECT b.PMIS_CODE FROM QLSHX10.PMIS_SYNC_STATE_BAK_20261015 b WHERE b.OBJECT_TYPE = r.OBJECT_TYPE)
           AND ((s.OBJECT_TYPE = 'SUBSTATION' AND s.PMIS_CODE IN (SELECT PMIS_CODE FROM QLSHX10.INFRASTRUCTURE WHERE INFRA_TYPE_ID = 1 AND ISDELETED = 0 AND UNIT_ID IS NULL AND PMIS_CODE IS NOT NULL))
             OR (s.OBJECT_TYPE = 'TRANSMISSION_LINE' AND s.PMIS_CODE IN (SELECT PMIS_CODE FROM QLSHX10.INFRASTRUCTURE WHERE INFRA_TYPE_ID = 2 AND ISDELETED = 0 AND UNIT_ID IS NULL AND PMIS_CODE IS NOT NULL))
             OR (s.OBJECT_TYPE = 'EQUIPMENT' AND s.PMIS_CODE IN (SELECT PMIS_CODE FROM QLSHX10.EQUIPMENTS WHERE ISDELETED = 0 AND UNITID IS NULL AND PMIS_CODE IS NOT NULL))
             OR (s.OBJECT_TYPE = 'PARENT_SCAN' AND s.PMIS_CODE IN (SELECT i.PMIS_CODE FROM QLSHX10.INFRASTRUCTURE i WHERE i.PMIS_CODE IS NOT NULL
                   AND EXISTS (SELECT 1 FROM QLSHX10.EQUIPMENTS e WHERE e.INFRASTRUCTURE_ID = i.ID AND e.ISDELETED = 0 AND e.UNITID IS NULL))));
        v_n := SQL%ROWCOUNT;
        v_total := v_total + v_n;
        DBMS_OUTPUT.PUT_LINE(r.OBJECT_TYPE || ': đã xoá ' || v_n || ' dòng.');
        COMMIT;
    END LOOP;
    DBMS_OUTPUT.PUT_LINE('Tổng đã xoá: ' || v_total || ' dòng PMIS_SYNC_STATE.');
END;
/
