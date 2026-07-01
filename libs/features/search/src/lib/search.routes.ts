import { Route } from '@angular/router';
import { dossierEquipmentLookupGuard } from '@sohoa.frontend/shared/core';

export const SEARCH_ROUTES: Route[] = [
  {
    path: 'equipment',
    loadComponent: () => import('./components/equipment-search/equipment-search.component').then(m => m.EquipmentSearchComponent)
  },
  {
    path: 'dossier',
    loadComponent: () => import('./components/dossier-search/dossier-search.component').then(m => m.DossierSearchComponent)
  },
  {
    path: 'dossier-by-equipment',
    loadComponent: () => import('./components/dossier-lookup/dossier-lookup.component').then(m => m.DossierLookupComponent)
  },
  {
    path: 'dossier-by-equipment/:id',
    loadComponent: () => import('./components/dossier-lookup-detail/dossier-lookup-detail.component').then(m => m.DossierLookupDetailComponent),
    canActivate: [dossierEquipmentLookupGuard]
  },
  {
    path: '',
    redirectTo: 'dossier-by-equipment',
    pathMatch: 'full'
  }
];
