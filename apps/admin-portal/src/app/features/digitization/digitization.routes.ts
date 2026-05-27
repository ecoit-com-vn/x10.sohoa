import { Routes } from '@angular/router';

export const DIGITIZATION_ROUTES: Routes = [
  {
    path: 'ocr-upload',
    loadComponent: () => import('./components/ocr-upload/ocr-upload.component').then(m => m.OcrUploadComponent)
  },
  {
    path: '',
    redirectTo: 'ocr-upload',
    pathMatch: 'full'
  }
];
