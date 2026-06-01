import { Route } from '@angular/router';

export const CATALOG_ROUTES: Route[] = [
  {
    path: 'unit-of-measurement',
    loadComponent: () => import('./unit-of-measurement.component').then(m => m.UnitOfMeasurement)
  }
];
