import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
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
  if (auth.hasPermission('SUPER_ADMIN') || auth.hasPermission('DOSSIER_MANAGE')) {
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
  resolver: (auth: AuthService, router: Router, route?: ActivatedRouteSnapshot) => boolean | UrlTree
): CanActivateFn {
  return (route: ActivatedRouteSnapshot, _state: RouterStateSnapshot) => {
    const router = inject(Router);
    const auth = inject(AuthService);

    return auth.ensurePermissionsLoaded().pipe(
      map(() => resolver(auth, router, route))
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

function resolveWarehouseSearchAccess(auth: AuthService, router: Router, route?: ActivatedRouteSnapshot): boolean | UrlTree {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW')
  ) {
    return true;
  }

  if (
    route?.queryParamMap.get('from') === 'report' &&
    auth.hasPermission('REPORT_STATISTICS_VIEW')
  ) {
    return true;
  }

  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

/** Menu Tìm kiếm hồ sơ trong kho */
export const dossierWarehouseSearchGuard = withPermissionsLoaded(resolveWarehouseSearchAccess);

function resolveDocumentFulltextSearchAccess(auth: AuthService, router: Router): boolean | UrlTree {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('DOCUMENT_FULLTEXT_SEARCH_VIEW')
  ) {
    return true;
  }

  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

/** Menu / ô tìm kiếm toàn văn tài liệu */
export const documentFulltextSearchGuard = withPermissionsLoaded(resolveDocumentFulltextSearchAccess);

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

function resolveSubstationSearchAccess(auth: AuthService, router: Router): boolean | UrlTree {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('SEARCH_SUBSTATION_VIEW')
  ) {
    return true;
  }

  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

/** Menu Tra cứu tìm kiếm Trạm biến áp */
export const substationSearchGuard = withPermissionsLoaded(resolveSubstationSearchAccess);

function resolveLineSearchAccess(auth: AuthService, router: Router): boolean | UrlTree {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('SEARCH_TRANSMISSION_LINE_VIEW')
  ) {
    return true;
  }

  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

/** Menu Tra cứu tìm kiếm Đường dây */
export const lineSearchGuard = withPermissionsLoaded(resolveLineSearchAccess);
