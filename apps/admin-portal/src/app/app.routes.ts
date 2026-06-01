import { Route } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const appRoutes: Route[] = [
  {
    path: 'login',
    loadComponent: () => import('./features/administration/login').then(m => m.Login)
  },
  {
    path: '',
    loadComponent: () => import('./layout/admin-layout').then(m => m.AdminLayout),
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'administration/user-management',
        loadComponent: () => import('./features/administration/user-management').then(m => m.UserManagement)
      },
      {
        path: 'administration/menu-management',
        loadComponent: () => import('./features/administration/menu-management.component').then(m => m.MenuManagement)
      },
      {
        path: 'administration/user-groups',
        loadComponent: () => import('./features/administration/user-group.component').then(m => m.UserGroupComponent)
      },
      {
        path: 'administration/upload-configuration',
        loadComponent: () => import('./features/administration/upload-config.component').then(m => m.UploadConfigComponent)
      },
      {
        path: 'administration/role-management',
        loadComponent: () => import('./features/administration/role-management.component').then(m => m.RoleManagement)
      },
      {
        path: 'administration/system-param',
        loadComponent: () => import('./features/administration/system-param.component').then(m => m.SystemParam)
      },
      {
        path: 'administration/organization-settings',
        loadComponent: () => import('./features/administration/organization-settings.component').then(m => m.OrganizationSettings)
      },
      {
        path: 'administration/audit-log',
        loadComponent: () => import('./features/administration/audit-log.component').then(m => m.AuditLogComponent)
      },
      {
        path: 'administration/sync-config',
        loadComponent: () => import('./features/administration/sync-config.component').then(m => m.SyncConfigComponent)
      },
      {
        path: 'catalog',
        loadChildren: () => import('./features/catalog/catalog.routes').then(m => m.CATALOG_ROUTES)
      },
      {
        path: 'equipment',
        loadChildren: () => import('./features/equipment/equipment.routes').then(m => m.EQUIPMENT_ROUTES)
      },

      {
        path: 'digitization',
        loadChildren: () => import('./features/digitization/digitization.routes').then(m => m.DIGITIZATION_ROUTES)
      },
      {
        path: 'search',
        loadChildren: () => import('./features/search/search.routes').then(m => m.SEARCH_ROUTES)
      },
      {
        path: 'workflow',
        loadChildren: () => import('./features/workflow/workflow.routes').then(m => m.WORKFLOW_ROUTES)
      },
      {
        path: 'reports',
        loadChildren: () => import('./features/reports/reports.routes').then(m => m.REPORTS_ROUTES)
      },
      {
        path: 'physical-storage',
        loadChildren: () => import('./features/physical-storage/physical-storage.routes').then(m => m.PHYSICAL_STORAGE_ROUTES)
      },
      {
        path: 'ocr-correction',
        loadChildren: () => import('./features/ocr-correction/ocr-correction.routes').then(m => m.OCR_CORRECTION_ROUTES)
      },
      {
        path: 'error',
        loadComponent: () => import('./features/error/global-error.component').then(m => m.GlobalErrorComponent)
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
