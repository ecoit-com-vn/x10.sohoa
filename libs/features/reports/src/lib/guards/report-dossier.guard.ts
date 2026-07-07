import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { map } from 'rxjs/operators';
import { AuthService } from '@sohoa.frontend/shared/core';

export function reportDossierGuard(permissionCode: string): CanActivateFn {
  return () => {
    const router = inject(Router);
    const auth = inject(AuthService);

    return auth.ensurePermissionsLoaded().pipe(
      map((): boolean | UrlTree => {
        if (auth.hasPermission('SUPER_ADMIN') || auth.hasPermission(permissionCode)) {
          return true;
        }
        return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
      })
    );
  };
}
