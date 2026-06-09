import { Route } from '@angular/router';

export const CATALOG_ROUTES: Route[] = [
  {
    path: 'fond',
    loadComponent: () => import('./components/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'PHONG', title: 'Phông' }
  },
  {
    path: 'dossier-toc',
    loadComponent: () => import('./components/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'MUC_LUC', title: 'Mục lục hồ sơ' }
  },
  {
    path: 'dossier-type',
    loadComponent: () => import('./components/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'LOAI_HO_SO', title: 'Loại hồ sơ' }
  },
  {
    path: 'shelf',
    loadComponent: () => import('./components/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'KE', title: 'Kệ hồ sơ' }
  },
  {
    path: 'floor',
    loadComponent: () => import('./components/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'TANG', title: 'Tầng hồ sơ' }
  },
  {
    path: 'box',
    loadComponent: () => import('./components/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'HOP', title: 'Hộp hồ sơ' }
  },
  {
    path: 'position',
    loadComponent: () => import('./components/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'CHUC_VU', title: 'Chức vụ' }
  },
  {
    path: 'domain',
    loadComponent: () => import('./components/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'LINH_VUC', title: 'Lĩnh vực' }
  },
  {
    path: 'physical-status',
    loadComponent: () => import('./components/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'TINH_TRANG_VAT_LY', title: 'Tình trạng vật lý' }
  }
];
