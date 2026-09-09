import { Routes } from '@angular/router';
import { DocumentManagementComponent } from './feature/document-management.component';
import { PmisDocumentWarehouseComponent } from './feature/pmis-document-warehouse.component';

export const DOCUMENT_MANAGEMENT_ROUTES: Routes = [
  {
    path: '',
    component: DocumentManagementComponent,
  },
  {
    path: 'pmis-warehouse',
    component: PmisDocumentWarehouseComponent,
  },
];
