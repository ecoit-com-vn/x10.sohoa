import { Routes } from '@angular/router';

export const DIGITIZATION_ROUTES: Routes = [
  {
    path: 'ocr-upload',
    loadComponent: () => import('./components/ocr-upload/ocr-upload.component').then(m => m.OcrUploadComponent)
  },
  {
    path: 'ocr-allocation',
    loadComponent: () => import('./ocr-allocation.component').then(m => m.OcrAllocationComponent)
  },
  {
    path: 'ocr-training',
    loadComponent: () => import('./components/ocr-training/ocr-training.component').then(m => m.OcrTrainingComponent)
  },
  {
    path: 'virtual-folders',
    loadComponent: () => import('./components/virtual-folders/virtual-folders.component').then(m => m.VirtualFoldersComponent)
  },
  {
    path: '',
    redirectTo: 'ocr-upload',
    pathMatch: 'full'
  }
];


