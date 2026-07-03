-- Đổi tên menu cũ "Phân bổ hồ sơ số hóa" -> "Phân bổ OCR"
UPDATE APP_MENU
SET Name = N'Phân bổ OCR'
WHERE Url = '/digitization/ocr-allocation' OR Name = N'Phân bổ hồ sơ số hóa';

-- Thêm menu mới "Phân bổ nhập liệu"
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 55,
       N'Phân bổ nhập liệu',
       '/digitization/folder-allocation',
       'pi pi-user-plus',
       (SELECT Id FROM APP_MENU WHERE Name = N'Số hóa hồ sơ' AND ROWNUM = 1),
       4,
       1,
       'FOLDER_ALLOCATION_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/digitization/folder-allocation'
);

COMMIT;
