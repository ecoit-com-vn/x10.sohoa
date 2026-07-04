-- Menu Nhập liệu hồ sơ số hóa + Kiểm tra nhập liệu
-- Quyền DOSSIER_DIGITIZATION_* được EquipmentService/WorkflowService tự sinh khi khởi động.

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 56,
       N'Nhập liệu hồ sơ số hóa',
       '/dossier-management/digitization/my-dossiers',
       'pi pi-file-edit',
       (SELECT Id FROM APP_MENU WHERE Name = N'Số hóa hồ sơ' AND ROWNUM = 1),
       5,
       1,
       'DOSSIER_DIGITIZATION_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/dossier-management/digitization/my-dossiers'
);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 57,
       N'Kiểm tra nhập liệu',
       '/dossier-management/digitization/approve',
       'pi pi-check-square',
       (SELECT Id FROM APP_MENU WHERE Name = N'Số hóa hồ sơ' AND ROWNUM = 1),
       6,
       1,
       'DOSSIER_DIGITIZATION_MANAGE'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/dossier-management/digitization/approve'
);

COMMIT;
