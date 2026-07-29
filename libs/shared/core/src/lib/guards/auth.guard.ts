import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { of } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const router = inject(Router);
  const authService = inject(AuthService);

  if (typeof window === 'undefined') {
    return true;
  }

  const token = authService.getToken();
  if (!token) {
    return router.createUrlTree(['/login']);
  }

  const loadPermsAndReturn = () =>
    authService.ensurePermissionsLoaded().pipe(map(() => true as const));

  if (!authService.isTokenExpired(token)) {
    return loadPermsAndReturn();
  }

  return authService.ensureValidToken().pipe(
    switchMap((valid) => {
      if (valid) {
        return loadPermsAndReturn();
      }
      return of(router.createUrlTree(['/login']));
    })
  );
};
