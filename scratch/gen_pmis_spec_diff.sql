-- ============================================================================
-- SCRIPT DÙNG 1 LẦN (không phải migration): tạo dữ liệu "đã đồng bộ từ PMIS" (EQUIPMENT_PMIS_SPEC)
-- cho 1 thiết bị cụ thể, với 1 vài thông số kỹ thuật được đổi khác đi so với dữ liệu nội bộ đang lưu
-- (EQUIPMENTS.FORM_VALUES) — để test tính năng "So sánh với PMIS" trên trang chi tiết thiết bị (các
-- dòng lệch sẽ hiện màu đỏ).
--
-- Tự đọc đúng FormSchema (biểu mẫu EAV đang active) của loại thiết bị này để biết field nào ứng với
-- khoá nào trong FORM_VALUES/pmisFieldName — KHÔNG hard-code tên khoá, vì các khoá này chỉ có trong
-- dữ liệu thật, không nhìn thấy được từ giao diện.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @gen_pmis_spec_diff.sql
-- Sửa v_equipment_id / v_fields_to_change bên dưới trước khi chạy nếu cần đổi thiết bị khác.
-- ============================================================================
SET SERVEROUTPUT ON SIZE UNLIMITED

DECLARE
    v_equipment_id     VARCHAR2(36) := '02d130ea-c1f6-4ba1-b800-a3fbf67c6310'; -- Máy biến áp 01 (E2.MBA01)
    v_fields_to_change PLS_INTEGER := 3; -- số trường muốn tạo khác biệt với PMIS

    v_equipment_type_id VARCHAR2(36);
    v_equipment_name    VARCHAR2(500);
    v_form_values_clob  CLOB;
    v_form_schema_clob  CLOB;

    v_local  JSON_OBJECT_T;
    v_schema JSON_ARRAY_T;
    v_field  JSON_OBJECT_T;
    v_pmis   JSON_OBJECT_T := JSON_OBJECT_T();

    v_local_key     VARCHAR2(200);
    v_pmis_key      VARCHAR2(200);
    v_label         VARCHAR2(500);
    v_old_value     VARCHAR2(4000);
    v_new_value     VARCHAR2(4000);
    v_changed_count PLS_INTEGER := 0;
    v_exists_count  NUMBER;
    v_new_id        VARCHAR2(36);
    v_raw           VARCHAR2(32);

    -- Ưu tiên name -> key -> id -> fieldName, đúng logic ResolveSchemaFieldName phía backend
    -- (EavSchemaHelper) — field tự sinh (auto-form) có name rỗng nên khoá thật là "id" ngẫu nhiên.
    FUNCTION resolve_key(f JSON_OBJECT_T) RETURN VARCHAR2 IS
    BEGIN
        IF f.has('name') AND LENGTH(TRIM(f.get_String('name'))) > 0 THEN RETURN f.get_String('name'); END IF;
        IF f.has('key') AND LENGTH(TRIM(f.get_String('key'))) > 0 THEN RETURN f.get_String('key'); END IF;
        IF f.has('id') AND LENGTH(TRIM(f.get_String('id'))) > 0 THEN RETURN f.get_String('id'); END IF;
        IF f.has('fieldName') AND LENGTH(TRIM(f.get_String('fieldName'))) > 0 THEN RETURN f.get_String('fieldName'); END IF;
        RETURN NULL;
    END;

    -- Sinh giá trị "khác đi nhưng vẫn hợp lý": có số thì tăng số đầu tiên tìm được (giữ nguyên chữ/đơn
    -- vị xung quanh), không có số thì thêm hậu tố để phân biệt rõ.
    FUNCTION make_different(v_original VARCHAR2) RETURN VARCHAR2 IS
        v_match VARCHAR2(100);
        v_pos   PLS_INTEGER;
        v_num   NUMBER;
        v_new_num_text VARCHAR2(50);
    BEGIN
        v_pos := REGEXP_INSTR(v_original, '[0-9]+([.,][0-9]+)?');
        IF v_pos = 0 THEN
            RETURN v_original || ' (khac PMIS)';
        END IF;

        v_match := REGEXP_SUBSTR(v_original, '[0-9]+([.,][0-9]+)?');
        v_num := TO_NUMBER(REPLACE(v_match, ',', '.'), '9999999999D9999', 'NLS_NUMERIC_CHARACTERS=''.,''');

        IF v_num >= 10 THEN
            v_num := v_num + ROUND(v_num * 0.1, 2);
        ELSE
            v_num := v_num + 1;
        END IF;

        IF v_num = TRUNC(v_num) THEN
            v_new_num_text := TO_CHAR(TRUNC(v_num));
        ELSE
            v_new_num_text := TRIM(TO_CHAR(v_num, 'FM999999999.99', 'NLS_NUMERIC_CHARACTERS=''.,'''));
        END IF;

        RETURN SUBSTR(v_original, 1, v_pos - 1) || v_new_num_text || SUBSTR(v_original, v_pos + LENGTH(v_match));
    END;

BEGIN
    -- 1) Lấy thiết bị + thông số nội bộ đang lưu
    SELECT EquipmentTypeId, Name, FORM_VALUES
      INTO v_equipment_type_id, v_equipment_name, v_form_values_clob
      FROM EQUIPMENTS
     WHERE Id = v_equipment_id AND IsDeleted = 0;

    DBMS_OUTPUT.PUT_LINE('Thiet bi: ' || v_equipment_name);

    IF v_form_values_clob IS NULL THEN
        DBMS_OUTPUT.PUT_LINE('Thiet bi nay chua co FORM_VALUES (chua nhap thong so ky thuat nao) -- khong co gi de tao khac biet.');
        RETURN;
    END IF;

    -- 2) Lấy FormSchema của biểu mẫu EAV đang active cho loại thiết bị này
    BEGIN
        SELECT v.FormSchema
          INTO v_form_schema_clob
          FROM EavFormTemplates t
          JOIN EavFormTemplateVersions v ON t.Id = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0
         WHERE t.EquipmentTypeId = v_equipment_type_id AND t.IsDeleted = 0 AND t.IsActive = 1 AND t.FormType = 'TEMPLATE'
         ORDER BY CASE WHEN v.Status = 'Hoàn thành' THEN 0 ELSE 1 END, v.Version DESC
         FETCH FIRST 1 ROW ONLY;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            DBMS_OUTPUT.PUT_LINE('Khong tim thay bieu mau thong so ky thuat (EavFormTemplate) dang active cho loai thiet bi nay.');
            RETURN;
    END;

    v_local  := JSON_OBJECT_T.parse(v_form_values_clob);
    v_schema := JSON_ARRAY_T.parse(v_form_schema_clob);

    -- 3) Duyệt từng field trong schema, đối chiếu với giá trị nội bộ, chọn vài field đầu tiên có giá
    -- trị để tạo khác biệt, các field còn lại giữ nguyên giá trị (để bảng so sánh có cả dòng khớp lẫn lệch).
    FOR i IN 0 .. v_schema.get_size - 1 LOOP
        v_field := TREAT(v_schema.get(i) AS JSON_OBJECT_T);
        v_local_key := resolve_key(v_field);

        IF v_local_key IS NOT NULL AND v_local.has(v_local_key) THEN
            v_old_value := v_local.get_String(v_local_key);

            IF v_old_value IS NOT NULL AND LENGTH(TRIM(v_old_value)) > 0 THEN
                v_pmis_key := CASE WHEN v_field.has('pmisFieldName') AND LENGTH(TRIM(v_field.get_String('pmisFieldName'))) > 0
                                   THEN v_field.get_String('pmisFieldName') ELSE v_local_key END;
                v_label := CASE WHEN v_field.has('label') THEN v_field.get_String('label') ELSE v_local_key END;

                IF v_changed_count < v_fields_to_change THEN
                    v_new_value := make_different(v_old_value);
                    IF v_new_value != v_old_value THEN
                        v_pmis.put(v_pmis_key, v_new_value);
                        v_changed_count := v_changed_count + 1;
                        DBMS_OUTPUT.PUT_LINE('  Doi "' || v_label || '": "' || v_old_value || '" -> "' || v_new_value || '"');
                    ELSE
                        v_pmis.put(v_pmis_key, v_old_value);
                    END IF;
                ELSE
                    v_pmis.put(v_pmis_key, v_old_value);
                END IF;
            END IF;
        END IF;
    END LOOP;

    IF v_changed_count = 0 THEN
        DBMS_OUTPUT.PUT_LINE('Khong tim duoc truong nao co san gia tri de tao khac biet.');
        RETURN;
    END IF;

    -- 4) Ghi vào EQUIPMENT_PMIS_SPEC (giống hệt cách PmisEquipmentSpecRepository.UpsertAsync ghi khi
    -- đồng bộ thật — 1 dòng/thiết bị, ghi đè mỗi lần).
    SELECT COUNT(*) INTO v_exists_count FROM EQUIPMENT_PMIS_SPEC WHERE EquipmentId = v_equipment_id;

    IF v_exists_count > 0 THEN
        UPDATE EQUIPMENT_PMIS_SPEC
           SET FormValues = v_pmis.to_clob(),
               SyncedAt = SYSTIMESTAMP,
               SyncHistoryId = NULL,
               RowVersion = RowVersion + 1,
               ModifiedBy = 'TEST_SCRIPT',
               ModifiedDate = SYSTIMESTAMP
         WHERE EquipmentId = v_equipment_id;
    ELSE
        v_raw := RAWTOHEX(SYS_GUID());
        v_new_id := LOWER(SUBSTR(v_raw,1,8)||'-'||SUBSTR(v_raw,9,4)||'-'||SUBSTR(v_raw,13,4)||'-'||SUBSTR(v_raw,17,4)||'-'||SUBSTR(v_raw,21,12));

        INSERT INTO EQUIPMENT_PMIS_SPEC (Id, EquipmentId, FormValues, SyncedAt, CreatedBy)
        VALUES (v_new_id, v_equipment_id, v_pmis.to_clob(), SYSTIMESTAMP, 'TEST_SCRIPT');
    END IF;

    COMMIT;

    DBMS_OUTPUT.PUT_LINE('Da ghi EQUIPMENT_PMIS_SPEC -- da doi ' || v_changed_count || ' truong.');
    DBMS_OUTPUT.PUT_LINE('Mo trang chi tiet thiet bi -> muc "So sanh voi PMIS" de xem cac dong lech mau do.');
END;
/

-- KIỂM TRA:
-- SELECT EquipmentId, FormValues, SyncedAt, ModifiedBy FROM EQUIPMENT_PMIS_SPEC
-- WHERE EquipmentId = '02d130ea-c1f6-4ba1-b800-a3fbf67c6310';
--
-- DỌN LẠI (xoá dữ liệu test để không lẫn với dữ liệu PMIS thật sau này):
-- DELETE FROM EQUIPMENT_PMIS_SPEC WHERE EquipmentId = '02d130ea-c1f6-4ba1-b800-a3fbf67c6310' AND CreatedBy = 'TEST_SCRIPT';
-- COMMIT;
