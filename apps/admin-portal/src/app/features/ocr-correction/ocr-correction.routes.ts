import { Routes } from '@angular/router';

export const OCR_CORRECTION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./ocr-correction.component').then(m => m.OcrCorrectionComponent)
  }
];
