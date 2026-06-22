import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { map } from 'rxjs/operators';
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

  if (!authService.isTokenExpired(token)) {
    authService.loadPermissions();
    return true;
  }

  return authService.ensureValidToken().pipe(
    map((valid) => {
      if (valid) {
        authService.loadPermissions();
        return true;
      }
      return router.createUrlTree(['/login']);
    })
  );
};
