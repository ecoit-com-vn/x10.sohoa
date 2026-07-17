import { Route } from '@angular/router';
import { documentFulltextSearchGuard, dossierEquipmentLookupGuard, dossierWarehouseSearchGuard, substationSearchGuard } from '@sohoa.frontend/shared/core';

export const SEARCH_ROUTES: Route[] = [
  {
    path: 'documents/:versionId',
    loadComponent: () =>
      import('./components/document-fulltext-detail/document-fulltext-detail.component').then(
        m => m.DocumentFulltextDetailComponent
      ),
    canActivate: [documentFulltextSearchGuard]
  },
  {
    path: 'documents',
    loadComponent: () =>
      import('./components/document-fulltext-search/document-fulltext-search.component').then(
        m => m.DocumentFulltextSearchComponent
      ),
    canActivate: [documentFulltextSearchGuard]
  },
  {
    path: 'substation',
    loadComponent: () => import('./components/substation-search/substation-search.component').then(m => m.SubstationSearchComponent),
    canActivate: [substationSearchGuard]
  },
  {
    path: 'substation/:id',
    loadComponent: () => import('./components/substation-search/substation-search.component').then(m => m.SubstationSearchComponent),
    canActivate: [substationSearchGuard]
  },
  {
    path: 'equipment',
    loadComponent: () => import('./components/equipment-search/equipment-search.component').then(m => m.EquipmentSearchComponent)
  },
  {
    path: 'dossier',
    loadComponent: () => import('./components/dossier-search/dossier-search.component').then(m => m.DossierSearchComponent),
    canActivate: [dossierWarehouseSearchGuard]
  },
  {
    path: 'dossier/detail/:id',
    loadComponent: () => import('./components/dossier-detail/dossier-detail.component').then(m => m.DossierDetailComponent),
    canActivate: [dossierWarehouseSearchGuard]
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
