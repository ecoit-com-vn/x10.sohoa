import { Route } from '@angular/router';

export const appRoutes: Route[] = [
  {
    path: 'login',
    loadComponent: () => import('./features/administration/login').then(m => m.Login)
  },
  {
    path: '',
    loadComponent: () => import('./layout/admin-layout').then(m => m.AdminLayout),
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
        path: 'administration/audit-log',
        loadComponent: () => import('./features/administration/audit-log.component').then(m => m.AuditLogComponent)
      },
      {
        path: 'administration/sync-config',
        loadComponent: () => import('./features/administration/sync-config.component').then(m => m.SyncConfigComponent)
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
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
