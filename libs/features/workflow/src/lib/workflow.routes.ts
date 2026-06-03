import { Routes } from '@angular/router';
import { BorrowReturnComponent } from './components/borrow-return/borrow-return.component';

export const WORKFLOW_ROUTES: Routes = [
  {
    path: 'borrow-return',
    component: BorrowReturnComponent,
  },
  {
    path: 'builder',
    loadComponent: () => import('./components/workflow-builder/workflow-builder.component').then(m => m.WorkflowBuilderComponent)
  },
  {
    path: '',
    redirectTo: 'borrow-return',
    pathMatch: 'full'
  }
];
