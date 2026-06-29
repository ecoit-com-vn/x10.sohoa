import { Routes } from '@angular/router';

export const EQUIPMENT_ROUTES: Routes = [
  {
    path: 'device-list',
    loadComponent: () => import('./components/equipment/equipment.component').then(m => m.EquipmentComponent)
  },
  {
    path: 'device-list/add',
    loadComponent: () => import('./components/equipment/equipment.component').then(m => m.EquipmentComponent)
  },
  {
    path: 'device-list/:id',
    loadComponent: () => import('./components/equipment/equipment.component').then(m => m.EquipmentComponent)
  },
  {
    path: 'equipment-type',
    loadComponent: () => import('./components/equipment-type/equipment-type.component').then(m => m.EquipmentTypeComponent)
  },
  {
    path: 'form-management',
    loadComponent: () => import('./components/form-management/form-management.component').then(m => m.FormManagementComponent)
  },
  {
    path: 'form-approval',
    loadComponent: () => import('./components/form-approval/form-approval.component').then(m => m.FormApprovalComponent)
  },
  {
    path: 'form-builder',
    loadComponent: () => import('./components/form-builder/form-builder.component').then(m => m.FormBuilderComponent)
  },
  {
    path: 'form-template',
    loadComponent: () => import('./components/form-template/form-template.component').then(m => m.FormTemplateComponent)
  },
  {
    path: 'completed-forms',
    loadComponent: () => import('./components/completed-form-list/completed-form-list.component').then(m => m.CompletedFormListComponent)
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
