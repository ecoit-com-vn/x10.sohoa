import { Route } from '@angular/router';
import { authGuard } from '@sohoa.frontend/shared/core';

export const appRoutes: Route[] = [
  {
    path: 'login',
    loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.Login)
  },
  {
    path: 'sso-login',
    loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.Login)
  },
  {
    path: '',
    loadComponent: () => import('@sohoa.frontend/shared/layout').then(m => m.AdminLayout),
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('@sohoa.frontend/features/dashboard').then(m => m.DashboardComponent)
      },
      {
        path: 'profile',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.UserProfileComponent)
      },
      {
        path: 'profile/change-password',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.ChangePasswordComponent)
      },
      {
        path: 'administration/user-management',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.UserManagement)
      },
      {
        path: 'administration/menu-management',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.MenuManagement)
      },
      {
        path: 'administration/user-groups',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.UserGroupComponent)
      },
      {
        path: 'administration/upload-configuration',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.UploadConfigComponent)
      },
      {
        path: 'administration/system-permission-groups',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.SystemPermissionGroupManagement)
      },
      {
        path: 'administration/unit-permission-groups',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.UnitPermissionGroupManagement)
      },
      {
        path: 'administration/roles',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.RoleManagement)
      },
      {
        path: 'administration/role-management',
        redirectTo: 'administration/system-permission-groups',
        pathMatch: 'full'
      },
      {
        path: 'administration/system-param',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.SystemParam)
      },
      {
        path: 'administration/organization-settings',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.OrganizationSettings)
      },
      {
        path: 'administration/audit-log',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.AuditLogComponent)
      },
      {
        path: 'administration/sync-config',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.SyncConfigComponent)
      },
      {
        path: 'administration/external-api-keys',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.ExternalApiKeyComponent)
      },
      {
        path: 'administration/history-api-keys',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.ExternalApiKeyHistoryComponent)
      },
      {
        path: 'administration/trainning-ai-ocr',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.AiOcrTrainingDocumentListComponent)
      },
      {
        path: 'administration/trainning-ai-ocr/:jobId/ocr-analysis',
        loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.AiOcrTrainingDocumentOcrInsightsPageComponent)
      },
      {
        path: 'administration/workflow-builder/new',
        loadComponent: () => import('@sohoa.frontend/features/workflow').then(m => m.WorkflowBuilderComponent)
      },
      {
        path: 'administration/workflow-builder/:id',
        loadComponent: () => import('@sohoa.frontend/features/workflow').then(m => m.WorkflowBuilderComponent)
      },
      {
        path: 'administration/workflow-builder',
        loadComponent: () => import('@sohoa.frontend/features/workflow').then(m => m.WorkflowBuilderComponent)
      },
      {
        path: 'catalog',
        loadChildren: () => import('@sohoa.frontend/features/catalog').then(m => m.CATALOG_ROUTES)
      },
      {
        path: 'equipment',
        loadChildren: () => import('@sohoa.frontend/features/equipment').then(m => m.EQUIPMENT_ROUTES)
      },
      {
        path: 'digitization',
        loadChildren: () => import('@sohoa.frontend/features/digitization').then(m => m.DIGITIZATION_ROUTES)
      },
      {
        path: 'search',
        loadChildren: () => import('@sohoa.frontend/features/search').then(m => m.SEARCH_ROUTES)
      },
      {
        path: 'workflow',
        loadChildren: () => import('@sohoa.frontend/features/workflow').then(m => m.WORKFLOW_ROUTES)
      },
      {
        path: 'borrow-records',
        loadComponent: () => import('@sohoa.frontend/features/workflow').then(m => m.BorrowReturnComponent)
      },
      {
        path: 'reports',
        loadChildren: () => import('@sohoa.frontend/features/reports').then(m => m.REPORTS_ROUTES)
      },
      {
        path: 'physical-storage',
        loadChildren: () => import('@sohoa.frontend/features/physical-storage').then(m => m.PHYSICAL_STORAGE_ROUTES)
      },
      {
        path: 'ocr-correction',
        loadChildren: () => import('@sohoa.frontend/features/ocr-correction').then(m => m.OCR_CORRECTION_ROUTES)
      },
      {
        path: 'dossier-management',
        loadChildren: () => import('@sohoa.frontend/features/dossier-management').then(m => m.DOSSIER_MANAGEMENT_ROUTES)
      },
      {
        path: 'documents',
        loadChildren: () => import('@sohoa.frontend/features/document-management').then(m => m.DOCUMENT_MANAGEMENT_ROUTES)
      },
      {
        path: 'pmis-sync',
        loadChildren: () => import('@sohoa.frontend/features/pmis-sync').then(m => m.PMIS_SYNC_ROUTES)
      },
      {
        path: 'error',
        loadComponent: () => import('@sohoa.frontend/features/error').then(m => m.GlobalErrorComponent)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'error'
  }
];
