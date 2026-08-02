import { Routes } from '@angular/router';

export const DIGITIZATION_ROUTES: Routes = [
  {
    path: 'ocr-upload',
    loadComponent: () => import('./components/ocr-upload/components/ocr-upload/ocr-upload.component').then(m => m.OcrUploadComponent)
  },
  {
    path: 'ocr-allocation',
    loadComponent: () => import('./components/ocr-allocation/ocr-allocation.component').then(m => m.OcrAllocationComponent)
  },
  {
    path: 'folder-allocation',
    loadComponent: () => import('./components/folder-allocation/folder-allocation.component').then(m => m.FolderAllocationComponent)
  },
  {
    path: 'ocr-training',
    loadComponent: () => import('./components/ocr-training/components/ocr-training/ocr-training.component').then(m => m.OcrTrainingComponent)
  },
  {
    path: 'virtual-folders',
    loadComponent: () => import('./components/virtual-folders/components/virtual-folders/virtual-folders.component').then(m => m.VirtualFoldersComponent)
  },
  {
    path: 'ocr-jobs',
    loadComponent: () => import('@sohoa.frontend/features/administration').then(m => m.OcrJobsMonitorComponent)
  },
  {
    path: '',
    redirectTo: 'ocr-upload',
    pathMatch: 'full'
  }
];
