-- Menu Thống kê báo cáo (quyền REPORT_STATISTICS_VIEW được tự động sinh qua PermissionDiscoveryService)
-- Menu này là menu con của menu cha "Báo cáo & Thống kê" (Id = 24), SortOrder = 3

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 92,
       N'Thống kê báo cáo',
       '/reports/statistics',
       'pi pi-chart-bar',
       24,
       3,
       1,
       'REPORT_STATISTICS_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/reports/statistics'
);

COMMIT;
