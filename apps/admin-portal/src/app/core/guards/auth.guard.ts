// e:/ecoit/sohoax10/sohoa.frontend/apps/admin-portal/src/app/core/guards/auth.guard.ts
import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';

export const authGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  
  // Nếu đang chạy trên server (SSR), cho phép pass để tránh redirect nhầm
  // do không có localStorage ở môi trường server. Việc kiểm tra thực tế
  // sẽ được thực hiện lại khi chạy ở client.
  if (typeof window === 'undefined') {
    return true;
  }

  const token = localStorage.getItem('token');

  if (token) {
    return true;
  }

  // Chuyển hướng về trang đăng nhập nếu chưa có token ở client
  router.navigate(['/login']);
  return false;
};
