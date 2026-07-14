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
    path: 'processing-category',
    loadComponent: () => import('./feature/catalog-list/catalog-list.component').then(m => m.CatalogListComponent),
    data: { type: 'PROCESSING_CATEGORY', title: 'Quy trình xử lý' }
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
  },
  {
    path: 'dossier-type',
    loadComponent: () => import('./feature/dossier-type/dossier-type.component').then(m => m.DossierTypeComponent)
  },
  {
    path: 'document-type',
    loadComponent: () => import('./feature/document-type/document-type.component').then(m => m.DocumentTypeComponent)
  },
  {
    path: 'substation/:id',
    loadComponent: () => import('./feature/infrastructure/infrastructure.component').then(m => m.InfrastructureComponent),
    data: { infraTypeId: 1, title: 'Chi tiết Trạm biến áp' }
  },
  {
    path: 'substation/:parentId/device-add',
    loadComponent: () => import('@sohoa.frontend/features/equipment').then(m => m.EquipmentComponent)
  },
  {
    path: 'substation/:parentId/device-detail/:id',
    loadComponent: () => import('@sohoa.frontend/features/equipment').then(m => m.EquipmentComponent)
  },
  {
    path: 'substation',
    loadComponent: () => import('./feature/infrastructure/infrastructure.component').then(m => m.InfrastructureComponent),
    data: { infraTypeId: 1, title: 'Danh mục trạm biến áp' }
  },
  {
    path: 'transmission-line/:id',
    loadComponent: () => import('./feature/infrastructure/infrastructure.component').then(m => m.InfrastructureComponent),
    data: { infraTypeId: 2, title: 'Chi tiết Đường dây' }
  },
  {
    path: 'transmission-line/:parentId/device-add',
    loadComponent: () => import('@sohoa.frontend/features/equipment').then(m => m.EquipmentComponent)
  },
  {
    path: 'transmission-line/:parentId/device-detail/:id',
    loadComponent: () => import('@sohoa.frontend/features/equipment').then(m => m.EquipmentComponent)
  },
  {
    path: 'transmission-line',
    loadComponent: () => import('./feature/infrastructure/infrastructure.component').then(m => m.InfrastructureComponent),
    data: { infraTypeId: 2, title: 'Danh mục đường dây' }
  }
];
