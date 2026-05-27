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
    path: '',
    redirectTo: 'form-builder',
    pathMatch: 'full'
  }
];
