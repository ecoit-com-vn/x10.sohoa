import { Routes } from '@angular/router';

export const OCR_CORRECTION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./components/ocr-correction/ocr-correction.component').then(m => m.OcrCorrectionComponent)
  }
];
