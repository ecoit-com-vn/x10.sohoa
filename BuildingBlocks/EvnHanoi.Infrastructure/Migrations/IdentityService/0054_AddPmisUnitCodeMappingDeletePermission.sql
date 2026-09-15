-- Migration 0054: Thêm quyền xoá cho màn "Ánh xạ mã đơn vị PMIS" (bổ sung sau 0053 — KHÔNG sửa lại
-- 0053 vì migration đó đã chạy trên các môi trường có sẵn; DbUp chỉ theo dõi script đã chạy theo TÊN,
-- không theo nội dung, nên sửa nội dung 0053 sẽ không bao giờ được áp dụng ở môi trường đã chạy 0053).

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'pmis_unit_code_mapping_delete_id',
       'PMIS_UNIT_CODE_MAPPING_DELETE',
       N'Xoá ánh xạ mã đơn vị PMIS',
       N'Xoá ánh xạ mã đơn vị PMIS đã thêm nhầm',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'PMIS_UNIT_CODE_MAPPING_DELETE'
);

INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
SELECT SYS_GUID(),
       (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1),
       p.Id
FROM PERMISSION p
WHERE p.Code = 'PMIS_UNIT_CODE_MAPPING_DELETE'
AND NOT EXISTS (
    SELECT 1
    FROM PERMISSION_GROUP_PERMISSION pgp
    WHERE pgp.PermissionGroupId = (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND pgp.PermissionId = p.Id
)
AND (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL;

COMMIT;
