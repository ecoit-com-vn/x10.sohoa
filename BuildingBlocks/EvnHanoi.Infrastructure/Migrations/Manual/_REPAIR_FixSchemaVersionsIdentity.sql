-- ============================================================================
-- SCRIPT SỬA LỖI HẠ TẦNG — KHÔNG PHẢI 1 MIGRATION NGHIỆP VỤ, KHÔNG GHI JOURNAL
-- ============================================================================
-- BỐI CẢNH (phát hiện 2026-09-29 trên production 210.245.84.38:8084):
-- Bảng SCHEMAVERSIONS (do DbUp dùng để đánh dấu migration nào đã chạy) có cột SCHEMAVERSIONID
-- là khóa chính (PK_SCHEMAVERSIONS), NOT NULL, nhưng KHÔNG có DEFAULT/IDENTITY/trigger tự sinh giá
-- trị — trong khi CHÍNH DbUp (và mọi câu INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED) từng được
-- hướng dẫn chạy tay trong các file Migrations/Manual/*.sql) đều KHÔNG cung cấp giá trị cho cột này.
-- Hậu quả: MỌI lần ghi journal (tự động lẫn thủ công) đều báo:
--   ORA-01400: cannot insert NULL into ("QLSHX10"."SCHEMAVERSIONS"."SCHEMAVERSIONID")
-- => Không migration nào từng ghi journal thành công trên production, dù nhiều DDL đã chạy đúng —
-- DbUp cứ thử lại TỪ ĐẦU mỗi lần service khởi động, gây lỗi lặp lại + có thể góp phần vào tình trạng
-- pod không ổn định quan sát được cùng ngày.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @_REPAIR_FixSchemaVersionsIdentity.sql
-- Chạy ĐÚNG 1 LẦN trên production. KHÔNG cần chạy trên UAT/dev (2 môi trường đó SCHEMAVERSIONID đã
-- tự sinh đúng từ trước — đã xác nhận qua SCHEMAVERSIONS đầy đủ, không lỗi).
--
-- SAU KHI CHẠY XONG: KHÔNG cần chạy tay từng file migration nữa — chỉ cần khởi động lại
-- EquipmentService và SyncService, DbUp sẽ TỰ áp toàn bộ migration còn thiếu (0057-0068 +
-- 0058_Fix/0060_Create/0063 vừa soạn) VÀ tự ghi journal đúng, vì cột SCHEMAVERSIONID giờ đã tự sinh.
--
-- ROLLBACK thủ công (không khuyến khích — sequence đã tăng không lùi lại được, nhưng bỏ DEFAULT thì an toàn):
--   ALTER TABLE QLSHX10.SCHEMAVERSIONS MODIFY (SCHEMAVERSIONID DEFAULT NULL);
-- ============================================================================
SET SERVEROUTPUT ON

-- BƯỚC 1: đẩy SCHEMAVERSIONS_SEQUENCE vượt qua SCHEMAVERSIONID lớn nhất đang có (an toàn: +margin),
-- tránh sinh trùng khóa chính khi bước 2 bắt đầu dùng sequence làm DEFAULT.
DECLARE
    v_current NUMBER;
    v_max_id  NUMBER;
    v_target  NUMBER;
    v_diff    NUMBER;
BEGIN
    SELECT NVL(MAX(SCHEMAVERSIONID), 0) INTO v_max_id FROM QLSHX10.SCHEMAVERSIONS;
    v_target := v_max_id + 100; -- margin dư, tránh chạy migration song song lúc sửa

    SELECT QLSHX10.SCHEMAVERSIONS_SEQUENCE.NEXTVAL INTO v_current FROM DUAL;

    IF v_current < v_target THEN
        v_diff := v_target - v_current;
        EXECUTE IMMEDIATE 'ALTER SEQUENCE QLSHX10.SCHEMAVERSIONS_SEQUENCE INCREMENT BY ' || v_diff;
        SELECT QLSHX10.SCHEMAVERSIONS_SEQUENCE.NEXTVAL INTO v_current FROM DUAL;
        EXECUTE IMMEDIATE 'ALTER SEQUENCE QLSHX10.SCHEMAVERSIONS_SEQUENCE INCREMENT BY 1';
        DBMS_OUTPUT.PUT_LINE('Da day SCHEMAVERSIONS_SEQUENCE toi ' || v_current || ' (max id hien tai: ' || v_max_id || ').');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Sequence da vuot qua max id (' || v_current || ' >= ' || v_target || '), khong can day.');
    END IF;
END;
/

-- BƯỚC 2: gắn DEFAULT cho SCHEMAVERSIONID trỏ vào sequence — mọi INSERT sau này (của DbUp lẫn tay)
-- không cần chỉ định cột này nữa vẫn tự có giá trị đúng, không còn ORA-01400.
ALTER TABLE QLSHX10.SCHEMAVERSIONS MODIFY (SCHEMAVERSIONID DEFAULT QLSHX10.SCHEMAVERSIONS_SEQUENCE.NEXTVAL);

-- KIỂM TRA:
-- SELECT COLUMN_NAME, DATA_DEFAULT FROM ALL_TAB_COLUMNS WHERE OWNER='QLSHX10' AND TABLE_NAME='SCHEMAVERSIONS' AND COLUMN_NAME='SCHEMAVERSIONID';
-- -- Thử insert thật 1 dòng test rồi xoá ngay để xác nhận:
-- INSERT INTO QLSHX10.SCHEMAVERSIONS (SCRIPTNAME, APPLIED) VALUES ('TEST_REPAIR_CHECK', SYSTIMESTAMP);
-- SELECT * FROM QLSHX10.SCHEMAVERSIONS WHERE SCRIPTNAME = 'TEST_REPAIR_CHECK';
-- DELETE FROM QLSHX10.SCHEMAVERSIONS WHERE SCRIPTNAME = 'TEST_REPAIR_CHECK';
-- COMMIT;
