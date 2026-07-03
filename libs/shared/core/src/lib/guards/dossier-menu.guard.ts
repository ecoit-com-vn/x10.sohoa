import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { map } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

function hasAllPermissions(auth: AuthService, codes: string[]): boolean {
  return codes.every((code) => auth.hasPermission(code));
}

function resolveCreatorAccess(auth: AuthService, router: Router): boolean | UrlTree {
  if (auth.hasPermission('SUPER_ADMIN')) {
    return true;
  }

  if (auth.hasPermission('DOSSIER_VIEW') && !auth.hasPermission('DOSSIER_CREATE')) {
    return true;
  }

  if (hasAllPermissions(auth, ['DOSSIER_CREATE', 'DOSSIER_EDIT', 'DOSSIER_VIEW'])) {
    return true;
  }

  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

function resolveApproverAccess(auth: AuthService, router: Router): boolean | UrlTree {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    hasAllPermissions(auth, ['DOSSIER_MANAGE', 'DOSSIER_VIEW', 'DOSSIER_EDIT'])
  ) {
    return true;
  }

  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

function resolvePublisherAccess(auth: AuthService, router: Router): boolean | UrlTree {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('DOSSIER_PUBLISH_VIEW') ||
    auth.hasPermission('DOSSIER_PUBLISH_RELEASE')
  ) {
    return true;
  }

  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

function withPermissionsLoaded(
  resolver: (auth: AuthService, router: Router) => boolean | UrlTree
): CanActivateFn {
  return () => {
    const router = inject(Router);
    const auth = inject(AuthService);

    return auth.ensurePermissionsLoaded().pipe(
      map(() => resolver(auth, router))
    );
  };
}

/** Menu Quản lý hồ sơ — cán bộ tạo/sửa hồ sơ của mình */
export const dossierCreatorMenuGuard = withPermissionsLoaded(resolveCreatorAccess);

/** Menu Phê duyệt hồ sơ — quản lý duyệt */
export const dossierApproverMenuGuard = withPermissionsLoaded(resolveApproverAccess);

/** Menu Xuất bản hồ sơ — người phụ trách xuất bản */
export const dossierPublisherMenuGuard = withPermissionsLoaded(resolvePublisherAccess);

function resolveEquipmentLookupAccess(auth: AuthService, router: Router): boolean | UrlTree {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW')
  ) {
    return true;
  }

  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

/** Menu Tra cứu hồ sơ thiết bị */
export const dossierEquipmentLookupGuard = withPermissionsLoaded(resolveEquipmentLookupAccess);

function resolveDigitizationCreatorAccess(auth: AuthService, router: Router): boolean | UrlTree {
  if (auth.hasPermission('SUPER_ADMIN')) return true;
  if (auth.hasPermission('DOSSIER_DIGITIZATION_VIEW') && !auth.hasPermission('DOSSIER_DIGITIZATION_CREATE')) return true;
  if (hasAllPermissions(auth, ['DOSSIER_DIGITIZATION_CREATE', 'DOSSIER_DIGITIZATION_EDIT', 'DOSSIER_DIGITIZATION_VIEW'])) return true;
  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

function resolveDigitizationApproverAccess(auth: AuthService, router: Router): boolean | UrlTree {
  if (auth.hasPermission('SUPER_ADMIN')) return true;
  if (auth.hasPermission('DOSSIER_DIGITIZATION_MANAGE')) return true;
  if (auth.hasPermission('DOSSIER_DIGITIZATION_VIEW')) return true;
  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

/** Menu Nhập liệu hồ sơ số hóa */
export const dossierDigitizationCreatorMenuGuard = withPermissionsLoaded(resolveDigitizationCreatorAccess);

/** Menu Kiểm tra nhập liệu */
export const dossierDigitizationApproverMenuGuard = withPermissionsLoaded(resolveDigitizationApproverAccess);
