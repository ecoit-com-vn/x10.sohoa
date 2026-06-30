-- Menu Xuất bản hồ sơ + gán PermissionCode cho menu đã tạo thủ công
-- Quyền DOSSIER_PUBLISH / DOSSIER_PUBLISH_VIEW được EquipmentService tự sinh khi khởi động.

UPDATE APP_MENU
SET PermissionCode = 'DOSSIER_PUBLISH_VIEW',
    Url = '/dossier-management/publish',
    Icon = NVL(Icon, 'pi pi-cloud-upload')
WHERE Url IN ('/dossier-management/publish', 'dossierPublish', '/dossier-management/dossierPublish')
   OR Name = N'Xuất bản hồ sơ';

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 54,
       N'Xuất bản hồ sơ',
       '/dossier-management/publish',
       'pi pi-cloud-upload',
       (SELECT Id FROM APP_MENU WHERE Url = '/dossier-management/my-dossiers' AND ROWNUM = 1),
       3,
       1,
       'DOSSIER_PUBLISH_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/dossier-management/publish'
);

COMMIT;
