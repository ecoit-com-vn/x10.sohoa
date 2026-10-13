-- ============================================================================
-- SCRIPT DỮ LIỆU THỦ CÔNG (KHÔNG phải migration DbUp — không tự chạy khi deploy).
-- Hoàn tác việc đồng bộ PMIS tự điền EQUIPMENTS.FORM_VALUES từ thông số kỹ thuật PMIS.
-- ============================================================================
-- BỐI CẢNH: trước đây InternalPmisSyncController.UpsertEquipmentFromPmis tự điền FORM_VALUES (dữ liệu "Hệ thống số hóa") từ PMIS khi còn rỗng,
-- khiến cột này luôn trùng PMIS. Code đã bỏ việc điền (EquipmentService bản mới). Script này đưa dữ liệu đã bị điền về trạng thái "chưa nhập".
--
-- QUY TẮC (đã chốt): PMIS_CODE IS NOT NULL AND FORM_VALUES IS NOT NULL AND MODIFIEDBY = 'PMIS_SYNC' AND có dòng EQUIPMENT_PMIS_SPEC
--   → đặt FORM_VALUES = NULL. KHÔNG sửa MODIFIEDBY/MODIFIEDDATE (tránh đánh dấu như người dùng vừa sửa).
--   Dòng do người dùng sửa (MODIFIEDBY khác) và thiết bị nhập tay (không PMIS_CODE) giữ nguyên.
--
-- ĐIỀU KIỆN CHẠY: PHẢI deploy EquipmentService bản mới (hết tự điền) TRƯỚC, nếu không đồng bộ sẽ điền lại.
--
-- SAO LƯU: mọi dòng bị xoá FORM_VALUES được chép vào QLSHX10.EQUIPMENT_FORM_VALUES_BACKUP_20261013 (EQUIPMENT_ID là khoá chính — nếu chạy
--   lại và dòng đã có bản sao lưu thì GIỮ bản sao lưu đầu tiên, không ghi đè).
--
-- CÁCH CHẠY (qua DebugSql /execute — gateway cắt ~100 s nên script tự thoát sau ~60 s; lặp lại tới khi in "Còn lại: 0"):
--   Chế độ đếm (không ghi): chạy riêng câu SELECT COUNT(*) ở mục DRY-RUN bên dưới qua /select.
-- Tiền tố "QLSHX10." tường minh, dùng ALL_* thay USER_* (xem feedback_manual_sql_migrations_need_qlshx10_prefix).
--
-- DRY-RUN (qua /select):
--   SELECT COUNT(*) FROM QLSHX10.EQUIPMENTS e WHERE e.PMIS_CODE IS NOT NULL AND e.FORM_VALUES IS NOT NULL AND e.MODIFIEDBY = 'PMIS_SYNC'
--      AND EXISTS (SELECT 1 FROM QLSHX10.EQUIPMENT_PMIS_SPEC s WHERE s.EQUIPMENTID = e.ID);
--
-- ROLLBACK (khôi phục FORM_VALUES từ bản sao lưu, chỉ cho dòng hiện đang NULL — không đè dữ liệu người dùng nhập sau đó):
--   UPDATE QLSHX10.EQUIPMENTS e SET e.FORM_VALUES = (SELECT b.FORM_VALUES FROM QLSHX10.EQUIPMENT_FORM_VALUES_BACKUP_20261013 b WHERE b.EQUIPMENT_ID = e.ID)
--    WHERE e.FORM_VALUES IS NULL AND EXISTS (SELECT 1 FROM QLSHX10.EQUIPMENT_FORM_VALUES_BACKUP_20261013 b WHERE b.EQUIPMENT_ID = e.ID);
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

-- Khối 1: tạo bảng sao lưu (tách riêng — khối 2 dùng SQL tĩnh nên bảng phải tồn tại lúc biên dịch).
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_TABLES WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'EQUIPMENT_FORM_VALUES_BACKUP_20261013';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TABLE QLSHX10.EQUIPMENT_FORM_VALUES_BACKUP_20261013 (
            EQUIPMENT_ID  VARCHAR2(36) NOT NULL,
            FORM_VALUES   CLOB NULL,
            MODIFIED_BY   VARCHAR2(100) NULL,
            MODIFIED_DATE TIMESTAMP NULL,
            BACKED_UP_AT  TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL,
            CONSTRAINT PK_EQ_FORM_VALUES_BAK_20261013 PRIMARY KEY (EQUIPMENT_ID)
        )';
        DBMS_OUTPUT.PUT_LINE('Đã tạo bảng sao lưu EQUIPMENT_FORM_VALUES_BACKUP_20261013.');
    END IF;
END;
/

-- Khối 2: sao lưu + xoá FORM_VALUES theo lô, tự thoát sau ~60 s.
DECLARE
    c_batch   CONSTANT PLS_INTEGER := 2000;
    c_max_sec CONSTANT NUMBER := 60;
    v_start   TIMESTAMP := SYSTIMESTAMP;
    v_cleared NUMBER := 0;
    v_remaining NUMBER;
    TYPE t_ids IS TABLE OF VARCHAR2(36);
    v_ids t_ids;

    FUNCTION elapsed_sec RETURN NUMBER IS
        v_diff INTERVAL DAY TO SECOND := SYSTIMESTAMP - v_start;
    BEGIN
        RETURN EXTRACT(DAY FROM v_diff) * 86400 + EXTRACT(HOUR FROM v_diff) * 3600
             + EXTRACT(MINUTE FROM v_diff) * 60 + EXTRACT(SECOND FROM v_diff);
    END;
BEGIN
    LOOP
        EXIT WHEN elapsed_sec >= c_max_sec;

        SELECT e.ID BULK COLLECT INTO v_ids
          FROM QLSHX10.EQUIPMENTS e
         WHERE e.PMIS_CODE IS NOT NULL
           AND e.FORM_VALUES IS NOT NULL
           AND e.MODIFIEDBY = 'PMIS_SYNC'
           AND EXISTS (SELECT 1 FROM QLSHX10.EQUIPMENT_PMIS_SPEC s WHERE s.EQUIPMENTID = e.ID)
           AND ROWNUM <= c_batch;
        EXIT WHEN v_ids.COUNT = 0;

        -- Sao lưu TRƯỚC (giữ bản đầu tiên nếu đã có), rồi mới xoá.
        FORALL i IN 1 .. v_ids.COUNT
            MERGE INTO QLSHX10.EQUIPMENT_FORM_VALUES_BACKUP_20261013 b
            USING (SELECT e.ID, e.FORM_VALUES, e.MODIFIEDBY, e.MODIFIEDDATE FROM QLSHX10.EQUIPMENTS e WHERE e.ID = v_ids(i)) src
               ON (b.EQUIPMENT_ID = src.ID)
             WHEN NOT MATCHED THEN
                 INSERT (EQUIPMENT_ID, FORM_VALUES, MODIFIED_BY, MODIFIED_DATE) VALUES (src.ID, src.FORM_VALUES, src.MODIFIEDBY, src.MODIFIEDDATE);

        FORALL i IN 1 .. v_ids.COUNT
            UPDATE QLSHX10.EQUIPMENTS
               SET FORM_VALUES = NULL
             WHERE ID = v_ids(i) AND FORM_VALUES IS NOT NULL AND MODIFIEDBY = 'PMIS_SYNC';

        v_cleared := v_cleared + v_ids.COUNT;
        COMMIT;
    END LOOP;

    SELECT COUNT(*) INTO v_remaining
      FROM QLSHX10.EQUIPMENTS e
     WHERE e.PMIS_CODE IS NOT NULL AND e.FORM_VALUES IS NOT NULL AND e.MODIFIEDBY = 'PMIS_SYNC'
       AND EXISTS (SELECT 1 FROM QLSHX10.EQUIPMENT_PMIS_SPEC s WHERE s.EQUIPMENTID = e.ID);

    DBMS_OUTPUT.PUT_LINE('Đã xoá FORM_VALUES lượt này: ' || v_cleared || '. Còn lại: ' || v_remaining);
END;
/
