-- Cleanup legacy static publish permissions to allow services to auto-generate them correctly
DELETE FROM PERMISSION_DETAIL WHERE PermissionId IN ('perm-uuid-dossier-publish-act-1111', 'perm-uuid-dossier-publish-view-1111');
DELETE FROM ROLE_PERMISSION WHERE PermissionId IN ('perm-uuid-dossier-publish-act-1111', 'perm-uuid-dossier-publish-view-1111');
DELETE FROM USER_PERMISSION WHERE PermissionId IN ('perm-uuid-dossier-publish-act-1111', 'perm-uuid-dossier-publish-view-1111');
DELETE FROM USER_GROUP_PERMISSION WHERE PermissionId IN ('perm-uuid-dossier-publish-act-1111', 'perm-uuid-dossier-publish-view-1111');

DELETE FROM PERMISSION_DETAIL WHERE PermissionId IN (SELECT Id FROM PERMISSION WHERE Code IN ('DOSSIER_PUBLISH', 'DOSSIER_PUBLISH_VIEW'));
DELETE FROM ROLE_PERMISSION WHERE PermissionId IN (SELECT Id FROM PERMISSION WHERE Code IN ('DOSSIER_PUBLISH', 'DOSSIER_PUBLISH_VIEW'));
DELETE FROM USER_PERMISSION WHERE PermissionId IN (SELECT Id FROM PERMISSION WHERE Code IN ('DOSSIER_PUBLISH', 'DOSSIER_PUBLISH_VIEW'));
DELETE FROM USER_GROUP_PERMISSION WHERE PermissionId IN (SELECT Id FROM PERMISSION WHERE Code IN ('DOSSIER_PUBLISH', 'DOSSIER_PUBLISH_VIEW'));

DELETE FROM PERMISSION WHERE Id IN ('perm-uuid-dossier-publish-act-1111', 'perm-uuid-dossier-publish-view-1111');
DELETE FROM PERMISSION WHERE Code IN ('DOSSIER_PUBLISH', 'DOSSIER_PUBLISH_VIEW');
