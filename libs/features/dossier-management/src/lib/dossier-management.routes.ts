import { Route } from '@angular/router';
import { dossierApproverMenuGuard, dossierCreatorMenuGuard, dossierDigitizationApproverMenuGuard, dossierDigitizationCreatorMenuGuard, dossierPublisherMenuGuard } from '@sohoa.frontend/shared/core';

const loadShell = () =>
  import('./feature/dossier-management.component').then((m) => m.DossierManagementComponent);

const creatorData = { menuScope: 'creator' as const, listTitle: 'Quản lý hồ sơ' };
const approverData = { menuScope: 'approver' as const, listTitle: 'Phê duyệt hồ sơ' };
const publisherData = { menuScope: 'publisher' as const, listTitle: 'Xuất bản hồ sơ' };

const digitizationCreatorData = {
  kindId: 1,
  kindCode: 'Digitization',
  menuScope: 'creator' as const,
  listTitle: 'Nhập liệu hồ sơ số hóa',
};

const digitizationApproverData = {
  kindId: 1,
  kindCode: 'Digitization',
  menuScope: 'approver' as const,
  listTitle: 'Kiểm tra nhập liệu',
};

export const DOSSIER_MANAGEMENT_ROUTES: Route[] = [
  { path: '', redirectTo: 'my-dossiers', pathMatch: 'full' },
  {
    path: 'digitization',
    children: [
      { path: '', redirectTo: 'my-dossiers', pathMatch: 'full' },
      {
        path: 'my-dossiers',
        loadComponent: loadShell,
        canActivate: [dossierDigitizationCreatorMenuGuard],
        data: digitizationCreatorData,
      },
      {
        path: 'my-dossiers/new',
        loadComponent: loadShell,
        canActivate: [dossierDigitizationCreatorMenuGuard],
        data: digitizationCreatorData,
      },
      {
        path: 'my-dossiers/:id/edit',
        loadComponent: loadShell,
        canActivate: [dossierDigitizationCreatorMenuGuard],
        data: digitizationCreatorData,
      },
      {
        path: 'my-dossiers/:id',
        loadComponent: loadShell,
        canActivate: [dossierDigitizationCreatorMenuGuard],
        data: digitizationCreatorData,
      },
      {
        path: 'approve',
        loadComponent: loadShell,
        canActivate: [dossierDigitizationApproverMenuGuard],
        data: digitizationApproverData,
      },
      {
        path: 'approve/:id',
        loadComponent: loadShell,
        canActivate: [dossierDigitizationApproverMenuGuard],
        data: digitizationApproverData,
      },
    ],
  },
  {
    path: 'my-dossiers',
    loadComponent: loadShell,
    canActivate: [dossierCreatorMenuGuard],
    data: creatorData,
  },
  {
    path: 'my-dossiers/new',
    loadComponent: loadShell,
    canActivate: [dossierCreatorMenuGuard],
    data: creatorData,
  },
  {
    path: 'my-dossiers/:id/edit',
    loadComponent: loadShell,
    canActivate: [dossierCreatorMenuGuard],
    data: creatorData,
  },
  {
    path: 'my-dossiers/:id',
    loadComponent: loadShell,
    canActivate: [dossierCreatorMenuGuard],
    data: creatorData,
  },
  {
    path: 'approve',
    loadComponent: loadShell,
    canActivate: [dossierApproverMenuGuard],
    data: approverData,
  },
  {
    path: 'approve/:id',
    loadComponent: loadShell,
    canActivate: [dossierApproverMenuGuard],
    data: approverData,
  },
  {
    path: 'publish',
    loadComponent: loadShell,
    canActivate: [dossierPublisherMenuGuard],
    data: publisherData,
  },
  {
    path: 'publish/:id',
    loadComponent: loadShell,
    canActivate: [dossierPublisherMenuGuard],
    data: publisherData,
  },
];
