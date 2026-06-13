import { Route } from '@angular/router';

export const CATALOG_ROUTES: Route[] = [
  {
    path: 'shelf',
    loadComponent: () => import('./feature/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'KE', title: 'Kệ hồ sơ' }
  },
  {
    path: 'floor',
    loadComponent: () => import('./feature/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'TANG', title: 'Tầng hồ sơ' }
  },
  {
    path: 'box',
    loadComponent: () => import('./feature/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'HOP', title: 'Hộp hồ sơ' }
  },
  {
    path: 'position',
    loadComponent: () => import('./feature/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'CHUC_VU', title: 'Chức vụ' }
  },
  {
    path: 'domain',
    loadComponent: () => import('./feature/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'LINH_VUC', title: 'Lĩnh vực' }
  },
  {
    path: 'physical-status',
    loadComponent: () => import('./feature/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'TINH_TRANG_VAT_LY', title: 'Tình trạng vật lý' }
  },
  {
    path: 'shared',
    loadComponent: () => import('./feature/catalog/catalog.component').then(m => m.CatalogComponent),
    data: { isPrivate: false, title: 'Danh mục dùng chung' }
  },
  {
    path: 'private',
    loadComponent: () => import('./feature/catalog/catalog.component').then(m => m.CatalogComponent),
    data: { isPrivate: true, title: 'Danh mục riêng' }
  }
];
