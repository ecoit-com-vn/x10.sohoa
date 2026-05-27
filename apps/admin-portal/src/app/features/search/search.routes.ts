import { Route } from '@angular/router';

export const SEARCH_ROUTES: Route[] = [
  {
    path: '',
    loadComponent: () => import('./equipment-search.component').then(m => m.EquipmentSearchComponent)
  }
];
