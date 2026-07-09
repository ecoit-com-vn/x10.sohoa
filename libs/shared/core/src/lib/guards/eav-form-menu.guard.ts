import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { map } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

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

function deny(router: Router): UrlTree {
  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

function hasAny(auth: AuthService, codes: string[]): boolean {
  return codes.some((code) => auth.hasPermission(code));
}

/** Menu Cấu hình form */
export const eavFormDesignMenuGuard = withPermissionsLoaded((auth, router) => {
  if (auth.hasPermission('SUPER_ADMIN') || auth.hasPermission('EAV_FORM_TEMPLATE_VIEW')) {
    return true;
  }
  return deny(router);
});

/** Menu Phê duyệt form */
export const eavFormApprovalMenuGuard = withPermissionsLoaded((auth, router) => {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('EAV_FORM_APPROVAL_VIEW') ||
    auth.hasPermission('EAV_FORM_APPROVAL_APPROVE')
  ) {
    return true;
  }
  return deny(router);
});

/** Menu Danh sách form hoàn thành */
export const eavFormCompletedMenuGuard = withPermissionsLoaded((auth, router) => {
  if (auth.hasPermission('SUPER_ADMIN') || auth.hasPermission('EAV_COMPLETED_FORM_VIEW')) {
    return true;
  }
  return deny(router);
});

/** Sửa form từ danh sách hoàn thành */
export const eavFormCompletedEditGuard = withPermissionsLoaded((auth, router) => {
  if (auth.hasPermission('SUPER_ADMIN')) {
    return true;
  }
  if (
    auth.hasPermission('EAV_COMPLETED_FORM_VIEW') &&
    hasAny(auth, ['EAV_FORM_TEMPLATE_EDIT', 'EAV_COMPLETED_FORM_MANAGE', 'EAV_COMPLETED_FORM_DELETE'])
  ) {
    return true;
  }
  return deny(router);
});
