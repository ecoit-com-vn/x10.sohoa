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
        path: 'administration/user-management',
        loadComponent: () => import('./features/administration/user-management').then(m => m.UserManagement)
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
        path: '',
        redirectTo: 'administration/user-management',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
