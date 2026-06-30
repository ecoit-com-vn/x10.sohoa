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
