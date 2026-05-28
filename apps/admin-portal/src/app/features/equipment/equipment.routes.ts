import { Routes } from '@angular/router';

export const EQUIPMENT_ROUTES: Routes = [
  {
    path: 'form-builder',
    loadComponent: () => import('./components/form-builder/form-builder.component').then(m => m.FormBuilderComponent)
  },
  {
    path: 'form-renderer',
    loadComponent: () => import('./components/form-renderer/form-renderer.component').then(m => m.FormRendererComponent)
  },
  {
    path: 'form-management',
    loadComponent: () => import('./components/form-management/form-management.component').then(m => m.FormManagementComponent)
  },
  {
    path: '',
    redirectTo: 'form-management',
    pathMatch: 'full'
  }
];
