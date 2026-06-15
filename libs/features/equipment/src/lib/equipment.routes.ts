import { Routes } from '@angular/router';

export const EQUIPMENT_ROUTES: Routes = [
  {
    path: 'list',
    loadComponent: () => import('./components/equipment/equipment.component').then(m => m.EquipmentComponent)
  },
  {
    path: 'equipment-type',
    loadComponent: () => import('./components/equipment-type/equipment-type.component').then(m => m.EquipmentTypeComponent)
  },
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
    redirectTo: 'list',
    pathMatch: 'full'
  }
];
