import { Routes } from '@angular/router';

export const PHYSICAL_STORAGE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./physical-storage.component').then(m => m.PhysicalStorageComponent)
  }
];
