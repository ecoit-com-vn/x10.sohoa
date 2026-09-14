-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/IdentityService/Migration0051_AddOrgIdSsoToOrganizationUnit.cs
-- ============================================================================
-- KHÔNG chạy tự động — chỉ dùng khi cần áp dụng tay 1 lần trên môi trường không tự chạy migration lúc
-- khởi động dịch vụ (vd. CSDL chia sẻ nhiều môi trường, hoặc cần soát trước khi deploy).
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @IdentityService_0051_AddOrgIdSsoToOrganizationUnit.sql
-- Sau khi chạy tay, ghi journal để migration runner biết đã áp dụng, tránh chạy trùng lúc service khởi động:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.IdentityService.Migration0051_AddOrgIdSsoToOrganizationUnit.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm cột ORGIDSSO (VARCHAR2(100) NULL) vào ORGANIZATION_UNIT — OrganizationUnitRepository
-- (CreateAsync/UpdateAsync/GetAllAsync/GetByIdAsync/GetOrganizationUnitsHierarchicalAsync) và
-- SsoAccountService đã tham chiếu cột này từ trước, nhưng chưa từng có migration chính thức nào tạo nó
-- — cột chỉ được ALTER TABLE thủ công 1 lần trên 1 máy Oracle dùng chung (scratch/temp_oracle_test/
-- Program.cs, không nằm trong pipeline migration), nên môi trường nào chỉ chạy đúng migration chính
-- thức sẽ thiếu cột này, gây ORA-00904 (invalid identifier "ORGIDSSO") mỗi khi tạo/sửa/xem đơn vị.
--
-- ROLLBACK thủ công:
--   ALTER TABLE ORGANIZATION_UNIT DROP COLUMN ORGIDSSO;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0051_AddOrgIdSsoToOrganizationUnit%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tab_columns
    WHERE table_name = 'ORGANIZATION_UNIT' AND column_name = 'ORGIDSSO';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE ORGANIZATION_UNIT ADD ORGIDSSO VARCHAR2(100) NULL';
        DBMS_OUTPUT.PUT_LINE('Da them cot ORGIDSSO vao ORGANIZATION_UNIT.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Cot ORGIDSSO da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT column_name, data_type, data_length, nullable FROM user_tab_columns
-- WHERE table_name = 'ORGANIZATION_UNIT' AND column_name = 'ORGIDSSO';
