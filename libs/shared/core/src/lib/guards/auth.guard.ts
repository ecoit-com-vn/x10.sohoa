import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const router = inject(Router);
  const authService = inject(AuthService);
  
  // Nếu đang chạy trên server (SSR), cho phép pass để tránh redirect nhầm
  // do không có localStorage ở môi trường server. Việc kiểm tra thực tế
  // sẽ được thực hiện lại khi chạy ở client.
  if (typeof window === 'undefined') {
    return true;
  }

  const token = authService.getToken();

  if (token) {
    authService.loadPermissions();
    return true;
  }

  // Chuyển hướng về trang đăng nhập nếu chưa có token ở client
  router.navigate(['/login']);
  return false;
};
