import { Route } from '@angular/router';

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
    path: '',
    redirectTo: 'equipment',
    pathMatch: 'full'
  }
];
