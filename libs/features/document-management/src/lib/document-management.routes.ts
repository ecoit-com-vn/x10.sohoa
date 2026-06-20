import { Routes } from '@angular/router';
import { DocumentManagementComponent } from './feature/document-management.component';

export const DOCUMENT_MANAGEMENT_ROUTES: Routes = [
  {
    path: '',
    component: DocumentManagementComponent,
  },
];
