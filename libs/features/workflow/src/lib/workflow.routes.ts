import { Routes } from '@angular/router';
import { BorrowReturnComponent } from './feature/borrow-return/borrow-return.component';

export const WORKFLOW_ROUTES: Routes = [
  {
    path: 'borrow-return',
    component: BorrowReturnComponent,
  },
  {
    path: 'builder/new',
    loadComponent: () => import('./feature/workflow-builder/workflow-builder.component').then(m => m.WorkflowBuilderComponent)
  },
  {
    path: 'builder/:id',
    loadComponent: () => import('./feature/workflow-builder/workflow-builder.component').then(m => m.WorkflowBuilderComponent)
  },
  {
    path: 'builder',
    loadComponent: () => import('./feature/workflow-builder/workflow-builder.component').then(m => m.WorkflowBuilderComponent)
  },
  {
    path: '',
    redirectTo: 'borrow-return',
    pathMatch: 'full'
  }
];
