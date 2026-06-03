import { Routes } from '@angular/router';

export const EQUIPMENT_ROUTES: Routes = [
  {
    path: 'form-management',
    loadComponent: () => import('./components/form-management/components/form-management/form-management.component').then(m => m.FormManagementComponent)
  },
  {
    path: 'form-builder',
    redirectTo: 'form-management',
    pathMatch: 'full'
  },
  {
    path: 'form-renderer',
    redirectTo: 'form-management',
    pathMatch: 'full'
  },
  {
    path: '',
    redirectTo: 'form-management',
    pathMatch: 'full'
  }
];
