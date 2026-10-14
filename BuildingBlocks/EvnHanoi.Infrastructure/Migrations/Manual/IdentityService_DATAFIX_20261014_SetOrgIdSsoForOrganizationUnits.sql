-- ============================================================================
-- SCRIPT SỬA DỮ LIỆU THỦ CÔNG (không phải migration DbUp): đặt ORGANIZATION_UNIT.ORGIDSSO theo mã tổ chức (orgId) của SSO EVNHANOI.
-- Nguồn orgId: "Tài liệu tích hợp SSO.md" (danh sách người dùng cung cấp 2026-10-14). Đợt 1 — các đơn vị ĐÃ CÓ trong hệ thống.
-- Lý do: đăng nhập SSO tra đơn vị theo ORGIDSSO (hoặc Id); không khớp → lỗi SSO-ORG-MAPPING (HTTP 500).
-- Mỗi UPDATE có điều kiện ID + CODE + ISDELETED=0 để không ghi nhầm đơn vị khác. Có sao lưu bảng trước khi sửa.
--
-- GHI CHÚ ĐĂNG NHẬP: bộ tra đơn vị còn khớp theo Id (TO_CHAR(Id) = orgId) nên orgId '1' vẫn về EVNHANOI (Id 1) sau khi ORGIDSSO đổi 1 → 276.
--
-- ROLLBACK (khôi phục cả ORGIDSSO lẫn UPDATEDAT/UPDATEDBY; chỉ dòng CHƯA bị sửa tiếp sau script — UPDATEDBY vẫn là 'SSO_ORG_SYNC'):
--   UPDATE QLSHX10.ORGANIZATION_UNIT u
--      SET u.ORGIDSSO  = (SELECT b.ORGIDSSO  FROM QLSHX10.ORGANIZATION_UNIT_BAK_20261014 b WHERE b.ID = u.ID),
--          u.UPDATEDAT = (SELECT b.UPDATEDAT FROM QLSHX10.ORGANIZATION_UNIT_BAK_20261014 b WHERE b.ID = u.ID),
--          u.UPDATEDBY = (SELECT b.UPDATEDBY FROM QLSHX10.ORGANIZATION_UNIT_BAK_20261014 b WHERE b.ID = u.ID)
--    WHERE u.ID IN (1,3,23,63,64,65,67) AND u.UPDATEDBY = 'SSO_ORG_SYNC';
--   COMMIT;
--   (xoá bảng sao lưu khi đã xác nhận: DROP TABLE QLSHX10.ORGANIZATION_UNIT_BAK_20261014 PURGE;)
-- ============================================================================
SET SERVEROUTPUT ON

-- Khối 1: sao lưu (chỉ tạo nếu chưa có).
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_TABLES WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'ORGANIZATION_UNIT_BAK_20261014';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TABLE QLSHX10.ORGANIZATION_UNIT_BAK_20261014 AS SELECT * FROM QLSHX10.ORGANIZATION_UNIT';
        DBMS_OUTPUT.PUT_LINE('Đã sao lưu ORGANIZATION_UNIT.');
    END IF;
END;
/

-- Khối 2: cập nhật ORGIDSSO.
DECLARE
    v_total NUMBER := 0;
    PROCEDURE set_org(p_id NUMBER, p_code VARCHAR2, p_orgid NUMBER) IS
    BEGIN
        UPDATE QLSHX10.ORGANIZATION_UNIT
           SET ORGIDSSO = p_orgid, UPDATEDAT = SYSTIMESTAMP, UPDATEDBY = 'SSO_ORG_SYNC'
         WHERE ID = p_id AND CODE = p_code AND ISDELETED = 0;
        IF SQL%ROWCOUNT <> 1 THEN
            RAISE_APPLICATION_ERROR(-20001, 'Không tìm thấy đơn vị Id=' || p_id || ' Code=' || p_code || ' (chưa ghi gì thêm).');
        END IF;
        v_total := v_total + 1;
    END;
BEGIN
    set_org(1,  'EVNHANOI', 276);  -- Tổng Công ty Điện lực TP Hà Nội (F04), trước đây 1
    set_org(23, 'HN06',     292);  -- Công ty Điện lực Gia Lâm
    set_org(63, 'HN08',     294);  -- Sóc Sơn
    set_org(65, 'HN05',     295);  -- Thanh Trì
    set_org(64, 'HN09',     300);  -- Sơn Tây
    set_org(67, 'HN10',     302);  -- Thạch Thất
    set_org(3,  'X6',       318);  -- Công ty Lưới điện cao thế TP Hà Nội
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Đã cập nhật ORGIDSSO cho ' || v_total || ' đơn vị.');
END;
/
