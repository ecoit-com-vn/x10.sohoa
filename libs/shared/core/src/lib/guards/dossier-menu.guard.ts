import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

function hasAllPermissions(auth: AuthService, codes: string[]): boolean {
  return codes.every((code) => auth.hasPermission(code));
}

/** Menu Quản lý hồ sơ — cán bộ tạo/sửa hồ sơ của mình */
export const dossierCreatorMenuGuard: CanActivateFn = () => {
  const router = inject(Router);
  const auth = inject(AuthService);
  auth.loadPermissions();

  if (auth.hasPermission('SUPER_ADMIN')) {
    return true;
  }

  // Chỉ xem danh sách/chi tiết (không sửa)
  if (auth.hasPermission('DOSSIER_VIEW') && !auth.hasPermission('DOSSIER_CREATE')) {
    return true;
  }

  // Tạo + sửa hồ sơ (cần đủ CREATE, EDIT, VIEW)
  if (
    hasAllPermissions(auth, ['DOSSIER_CREATE', 'DOSSIER_EDIT', 'DOSSIER_VIEW'])
  ) {
    return true;
  }

  return router.createUrlTree(['/error']);
};

/** Menu Phê duyệt hồ sơ — quản lý duyệt */
export const dossierApproverMenuGuard: CanActivateFn = () => {
  const router = inject(Router);
  const auth = inject(AuthService);
  auth.loadPermissions();

  if (
    auth.hasPermission('SUPER_ADMIN') ||
    hasAllPermissions(auth, ['DOSSIER_MANAGE', 'DOSSIER_VIEW', 'DOSSIER_EDIT'])
  ) {
    return true;
  }

  return router.createUrlTree(['/error']);
};

/** Menu Xuất bản hồ sơ — người phụ trách xuất bản */
export const dossierPublisherMenuGuard: CanActivateFn = () => {
  const router = inject(Router);
  const auth = inject(AuthService);
  auth.loadPermissions();

  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('DOSSIER_PUBLISH_VIEW') ||
    auth.hasPermission('DOSSIER_PUBLISH_RELEASE')
  ) {
    return true;
  }

  return router.createUrlTree(['/error']);
};
