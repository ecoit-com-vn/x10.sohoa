import { Route } from '@angular/router';

export const DOSSIER_MANAGEMENT_ROUTES: Route[] = [
  {
    path: '',
    loadComponent: () => import('./feature/dossier-management.component').then(m => m.DossierManagementComponent)
  }
];
