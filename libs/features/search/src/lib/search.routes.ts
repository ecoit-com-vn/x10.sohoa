import { Route } from '@angular/router';

export const SEARCH_ROUTES: Route[] = [
  {
    path: '',
    loadComponent: () => import('./components/equipment-search/equipment-search.component').then(m => m.EquipmentSearchComponent)
  }
];
