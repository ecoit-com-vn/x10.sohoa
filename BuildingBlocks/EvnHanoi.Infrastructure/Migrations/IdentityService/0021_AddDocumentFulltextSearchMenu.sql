-- Migration: Quyền tìm kiếm toàn văn (ô header, không có URL sidebar)

INSERT INTO PERMISSION (Id, Code, Name, Description, ServiceName)
SELECT 'document_fulltext_search_view_id',
       'DOCUMENT_FULLTEXT_SEARCH_VIEW',
       N'Tìm kiếm toàn văn tài liệu',
       N'Tự động sinh: Quyền tra cứu nội dung OCR tài liệu đã xuất bản',
       'NotificationService'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'DOCUMENT_FULLTEXT_SEARCH_VIEW'
);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 56,
       N'Tìm kiếm toàn văn',
       NULL,
       'pi pi-search-plus',
       NULL,
       8,
       1,
       'DOCUMENT_FULLTEXT_SEARCH_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE PermissionCode = 'DOCUMENT_FULLTEXT_SEARCH_VIEW'
);

INSERT INTO ROLE_PERMISSION (RoleId, PermissionId)
SELECT (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1), 'document_fulltext_search_view_id'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM ROLE_PERMISSION
    WHERE RoleId = (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = 'document_fulltext_search_view_id'
)
AND (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL;

COMMIT;
