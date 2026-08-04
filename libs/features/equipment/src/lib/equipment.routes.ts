import { Routes } from '@angular/router';
import {
  eavFormApprovalMenuGuard,
  eavFormCompletedEditGuard,
  eavFormCompletedMenuGuard,
  eavFormDesignMenuGuard,
} from '@sohoa.frontend/shared/core';

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
    path: 'equipment-external',
    loadComponent: () => import('./components/equipment-external/equipment-external.component').then(m => m.EquipmentExternalComponent)
  },
  {
    path: 'equipment-factory',
    loadComponent: () => import('./components/equipment-factory/equipment-factory.component').then(m => m.EquipmentFactoryComponent)
  },
  {
    path: 'equipment-type',
    loadComponent: () => import('./components/equipment-type/equipment-type.component').then(m => m.EquipmentTypeComponent)
  },
  {
    path: 'form-management',
    canActivate: [eavFormDesignMenuGuard],
    loadComponent: () => import('./components/form-management/form-management.component').then(m => m.FormManagementComponent)
  },
  {
    path: 'form-management/new',
    canActivate: [eavFormDesignMenuGuard],
    loadComponent: () => import('./components/form-management/form-management.component').then(m => m.FormManagementComponent)
  },
  {
    path: 'form-management/:id/edit',
    canActivate: [eavFormDesignMenuGuard],
    loadComponent: () => import('./components/form-management/form-management.component').then(m => m.FormManagementComponent)
  },
  {
    path: 'form-management/:id',
    canActivate: [eavFormDesignMenuGuard],
    loadComponent: () => import('./components/form-management/form-management.component').then(m => m.FormManagementComponent)
  },
  {
    path: 'form-approval',
    canActivate: [eavFormApprovalMenuGuard],
    loadComponent: () => import('./components/form-approval/form-approval.component').then(m => m.FormApprovalComponent)
  },
  {
    path: 'form-approval/:id',
    canActivate: [eavFormApprovalMenuGuard],
    loadComponent: () => import('./components/form-approval/form-approval.component').then(m => m.FormApprovalComponent)
  },
  {
    path: 'form-builder',
    canActivate: [eavFormDesignMenuGuard],
    loadComponent: () => import('./components/form-builder/form-builder.component').then(m => m.FormBuilderComponent)
  },
  {
    path: 'form-template',
    canActivate: [eavFormDesignMenuGuard],
    loadComponent: () => import('./components/form-template/form-template.component').then(m => m.FormTemplateComponent)
  },
  {
    path: 'form-template/:id',
    canActivate: [eavFormDesignMenuGuard],
    loadComponent: () => import('./components/form-template/form-template.component').then(m => m.FormTemplateComponent)
  },
  {
    path: 'completed-forms',
    canActivate: [eavFormCompletedMenuGuard],
    loadComponent: () => import('./components/completed-form-list/completed-form-list.component').then(m => m.CompletedFormListComponent)
  },
  {
    path: 'completed-forms/:id/edit',
    canActivate: [eavFormCompletedEditGuard],
    data: { formContext: 'completed' },
    loadComponent: () => import('./components/form-management/form-management.component').then(m => m.FormManagementComponent)
  },
  {
    path: 'completed-forms/:id',
    canActivate: [eavFormCompletedMenuGuard],
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
