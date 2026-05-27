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
