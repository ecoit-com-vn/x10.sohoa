-- ============================================================================
-- SCRIPT SỬA DỮ LIỆU THỦ CÔNG (KHÔNG phải migration DbUp — không tự chạy khi deploy; cố ý KHÔNG đánh số 00xx để khỏi va số migration).
-- Hoàn tác việc đồng bộ PMIS tự điền EQUIPMENTS.FORM_VALUES từ thông số kỹ thuật PMIS.
-- ============================================================================
-- BỐI CẢNH: trước đây InternalPmisSyncController.UpsertEquipmentFromPmis tự điền FORM_VALUES (dữ liệu "Hệ thống số hóa") từ PMIS khi còn rỗng,
-- khiến cột này luôn trùng PMIS. Code đã bỏ việc điền (EquipmentService bản mới). Script này đưa dữ liệu đã bị điền về trạng thái "chưa nhập".
--
-- QUY TẮC XOÁ (FORM_VALUES = NULL) — phải thoả TẤT CẢ:
--   1. PMIS_CODE IS NOT NULL, FORM_VALUES IS NOT NULL, MODIFIEDBY = 'PMIS_SYNC', có dòng EQUIPMENT_PMIS_SPEC;
--   2. MỌI giá trị trong FORM_VALUES đều nằm trong tập giá trị của thông số PMIS hiện tại của thiết bị (kiểm tra theo GIÁ TRỊ, không cần ánh xạ khoá).
--      MODIFIEDBY chưa đủ tin cậy: đồng bộ ghi đè MODIFIEDBY='PMIS_SYNC' mỗi khi cập nhật Name/QR/đơn vị/loại — dòng người dùng đã nhập rồi bị
--      đồng bộ chạm vào cũng mang nhãn này. Điều kiện 2 giữ lại các dòng có giá trị KHÁC PMIS (người dùng đã nhập/sửa) và in ra số lượng.
--   KHÔNG sửa MODIFIEDBY/MODIFIEDDATE khi xoá. Thiết bị nhập tay (không PMIS_CODE) và dòng MODIFIEDBY khác giữ nguyên.
--   Giới hạn đã biết: người dùng nhập đúng bằng giá trị PMIS thì không phân biệt được với tự điền → bị xoá (có sao lưu).
--
-- ĐIỀU KIỆN CHẠY: PHẢI deploy EquipmentService bản mới (hết tự điền) TRƯỚC. Script tự chặn nếu thấy thiết bị MỚI TẠO trong 15 phút qua vẫn bị điền
--   (c_force = TRUE để bỏ qua kiểm tra — chỉ khi chắc chắn không còn pod EquipmentService cũ).
--
-- CẤU HÌNH Ở ĐẦU KHỐI 2 (sửa trước khi chạy):
--   c_dry_run  TRUE = chỉ đếm, KHÔNG ghi (kể cả sao lưu). Mặc định FALSE.
--   c_after_id  khoá duyệt tiếp: lần chạy trước in "ID cuối: <x>" — dán vào đây để chạy tiếp (mặc định rỗng = từ đầu).
--
-- SAO LƯU: dòng bị xoá FORM_VALUES được chép vào QLSHX10.EQUIPMENT_FORM_VALUES_BACKUP_20261013 (khoá chính EQUIPMENT_ID; chạy lại cùng dòng thì
--   bản sao lưu được GHI ĐÈ bằng giá trị mới nhất vừa xoá). Bảng này giữ CLOB dữ liệu — DROP sau khi xác nhận xong (đề xuất sau 30 ngày):
--   DROP TABLE QLSHX10.EQUIPMENT_FORM_VALUES_BACKUP_20261013 PURGE;
--
-- CÁCH CHẠY (qua DebugSql /execute — gateway cắt ~100 s, script tự thoát sau 40 lô × 500 dòng; lặp tới khi "Lô duyệt cuối: 0 dòng"):
--   Tiền tố "QLSHX10." tường minh, dùng ALL_* thay USER_* (xem feedback_manual_sql_migrations_need_qlshx10_prefix).
--
-- ROLLBACK (khôi phục từ bản sao lưu, chỉ dòng đang NULL — không đè dữ liệu người dùng nhập sau đó):
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

-- Khối 2: duyệt theo khoá ID, sao lưu + xoá FORM_VALUES của các dòng thoả quy tắc.
DECLARE
    c_dry_run   CONSTANT BOOLEAN      := FALSE;
    c_force     CONSTANT BOOLEAN      := FALSE;
    c_after_id  CONSTANT VARCHAR2(36) := '';
    c_batch     CONSTANT PLS_INTEGER  := 500;
    c_max_batches CONSTANT PLS_INTEGER := 40;   -- giới hạn số lô mỗi lần chạy (không dùng đồng hồ: kiểm tra SYSTIMESTAMP mỗi vòng lặp làm chậm bất thường)

    v_last_id   VARCHAR2(36) := NVL(c_after_id, ' ');   -- ' ' < mọi UUID; chuỗi rỗng trong Oracle là NULL nên không dùng
    v_cleared   NUMBER := 0;
    v_kept      NUMBER := 0;
    v_last_n    NUMBER := 0;
    v_recent    NUMBER;
    v_fv        CLOB;
    v_sp        CLOB;
    TYPE t_ids IS TABLE OF VARCHAR2(36);
    v_clear_ids t_ids;
    TYPE t_set IS TABLE OF NUMBER INDEX BY VARCHAR2(4000);

    -- TRUE nếu mọi giá trị trong FORM_VALUES đều có mặt trong tập giá trị thông số PMIS (=> chỉ là bản sao tự điền). Lỗi parse/kiểu lạ → FALSE (giữ lại).
    FUNCTION is_auto_filled(p_form_values CLOB, p_pmis_spec CLOB) RETURN BOOLEAN IS
        v_fv   JSON_OBJECT_T;
        v_sp   JSON_OBJECT_T;
        v_keys JSON_KEY_LIST;
        v_set  t_set;
        v_val  VARCHAR2(32767);
    BEGIN
        v_fv := JSON_OBJECT_T.parse(p_form_values);
        v_sp := JSON_OBJECT_T.parse(p_pmis_spec);

        v_keys := v_sp.get_keys;
        FOR i IN 1 .. v_keys.COUNT LOOP
            v_val := v_sp.get_string(v_keys(i));
            IF v_val IS NOT NULL THEN v_set(SUBSTR(v_val, 1, 4000)) := 1; END IF;
        END LOOP;

        v_keys := v_fv.get_keys;
        FOR i IN 1 .. v_keys.COUNT LOOP
            v_val := v_fv.get_string(v_keys(i));
            IF v_val IS NOT NULL AND TRIM(v_val) IS NOT NULL AND NOT v_set.EXISTS(SUBSTR(v_val, 1, 4000)) THEN
                RETURN FALSE;
            END IF;
        END LOOP;
        RETURN TRUE;
    EXCEPTION WHEN OTHERS THEN
        RETURN FALSE;
    END;
BEGIN
    -- Chặn nếu EquipmentService cũ vẫn đang tự điền: thiết bị mới tạo (PMIS_SYNC) trong 15 phút qua mà đã có FORM_VALUES.
    IF NOT c_force AND NOT c_dry_run THEN
        SELECT COUNT(*) INTO v_recent
          FROM QLSHX10.EQUIPMENTS e
         WHERE e.CREATEDBY = 'PMIS_SYNC' AND e.FORM_VALUES IS NOT NULL
           AND e.CREATEDAT > SYSTIMESTAMP - INTERVAL '15' MINUTE
           AND EXISTS (SELECT 1 FROM QLSHX10.EQUIPMENT_PMIS_SPEC s WHERE s.EQUIPMENTID = e.ID);
        IF v_recent > 0 THEN
            RAISE_APPLICATION_ERROR(-20001, 'Còn ' || v_recent || ' thiết bị mới tạo trong 15 phút qua vẫn bị tự điền FORM_VALUES — EquipmentService bản mới chưa chạy hết. Dừng.');
        END IF;
    END IF;

    FOR b IN 1 .. c_max_batches LOOP
        v_clear_ids := t_ids();
        v_last_n := 0;

        -- Con trỏ CHỈ chọn ID (duyệt theo khoá chính, đã lọc): chọn kèm CLOB rồi ORDER BY khiến Oracle quét toàn bảng + sắp xếp cả CLOB (rất chậm).
        FOR r IN (
            SELECT ID FROM (
                SELECT e.ID AS ID
                  FROM QLSHX10.EQUIPMENTS e
                 WHERE e.ID > v_last_id
                   AND e.PMIS_CODE IS NOT NULL AND e.FORM_VALUES IS NOT NULL AND e.MODIFIEDBY = 'PMIS_SYNC'
                 ORDER BY e.ID
            ) WHERE ROWNUM <= c_batch
        ) LOOP
            v_last_id := r.ID;
            v_last_n := v_last_n + 1;
            BEGIN
                SELECT e.FORM_VALUES, s.FORMVALUES INTO v_fv, v_sp
                  FROM QLSHX10.EQUIPMENTS e JOIN QLSHX10.EQUIPMENT_PMIS_SPEC s ON s.EQUIPMENTID = e.ID
                 WHERE e.ID = r.ID;
            EXCEPTION WHEN NO_DATA_FOUND THEN
                CONTINUE; -- không có dòng thông số PMIS → không thuộc phạm vi xoá
            END;
            IF is_auto_filled(v_fv, v_sp) THEN
                v_clear_ids.EXTEND;
                v_clear_ids(v_clear_ids.COUNT) := r.ID;
            ELSE
                v_kept := v_kept + 1;
            END IF;
        END LOOP;
        EXIT WHEN v_last_n = 0;

        IF NOT c_dry_run AND v_clear_ids.COUNT > 0 THEN
            -- Sao lưu TRƯỚC (ghi đè bản cũ nếu dòng từng được sao lưu), rồi mới xoá.
            FORALL i IN 1 .. v_clear_ids.COUNT
                MERGE INTO QLSHX10.EQUIPMENT_FORM_VALUES_BACKUP_20261013 b
                USING (SELECT e.ID, e.FORM_VALUES, e.MODIFIEDBY, e.MODIFIEDDATE FROM QLSHX10.EQUIPMENTS e WHERE e.ID = v_clear_ids(i)) src
                   ON (b.EQUIPMENT_ID = src.ID)
                 WHEN MATCHED THEN UPDATE SET b.FORM_VALUES = src.FORM_VALUES, b.MODIFIED_BY = src.MODIFIEDBY,
                                              b.MODIFIED_DATE = src.MODIFIEDDATE, b.BACKED_UP_AT = SYSTIMESTAMP
                 WHEN NOT MATCHED THEN INSERT (EQUIPMENT_ID, FORM_VALUES, MODIFIED_BY, MODIFIED_DATE)
                                       VALUES (src.ID, src.FORM_VALUES, src.MODIFIEDBY, src.MODIFIEDDATE);

            FORALL i IN 1 .. v_clear_ids.COUNT
                UPDATE QLSHX10.EQUIPMENTS
                   SET FORM_VALUES = NULL
                 WHERE ID = v_clear_ids(i) AND FORM_VALUES IS NOT NULL AND MODIFIEDBY = 'PMIS_SYNC';
            COMMIT;
        END IF;
        v_cleared := v_cleared + v_clear_ids.COUNT;
    END LOOP;

    DBMS_OUTPUT.PUT_LINE(CASE WHEN c_dry_run THEN '[DRY-RUN] ' ELSE '' END
        || 'Thoả quy tắc xoá: ' || v_cleared || ' | Giữ lại (giá trị khác PMIS): ' || v_kept
        || ' | Lô duyệt cuối: ' || v_last_n || ' dòng | ID cuối: ' || TRIM(v_last_id));
END;
/
