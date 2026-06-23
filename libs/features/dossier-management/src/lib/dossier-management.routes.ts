import { Route } from '@angular/router';

const loadShell = () =>
  import('./feature/dossier-management.component').then((m) => m.DossierManagementComponent);

export const DOSSIER_MANAGEMENT_ROUTES: Route[] = [
  {
    path: 'new',
    loadComponent: loadShell,
  },
  {
    path: ':id/edit',
    loadComponent: loadShell,
  },
  {
    path: ':id',
    loadComponent: loadShell,
  },
  {
    path: '',
    loadComponent: loadShell,
  },
];
