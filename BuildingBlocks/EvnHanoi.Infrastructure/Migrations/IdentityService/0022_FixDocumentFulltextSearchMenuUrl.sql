-- Sửa menu tìm kiếm toàn văn: không gắn URL (truy cập qua ô tìm kiếm header)

UPDATE APP_MENU
SET Url = NULL
WHERE PermissionCode = 'DOCUMENT_FULLTEXT_SEARCH_VIEW'
  AND Url IS NOT NULL;

COMMIT;
