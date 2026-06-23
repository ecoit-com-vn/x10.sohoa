import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';

function isAuthEndpoint(url: string): boolean {
  return url.includes('/auth/login') || url.includes('/auth/refresh');
}

export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const messageService = inject(MessageService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (typeof ErrorEvent !== 'undefined' && error.error instanceof ErrorEvent) {
        messageService.add({
          severity: 'error',
          summary: 'Lỗi ứng dụng',
          detail: error.error.message
        });
      } else {
        switch (error.status) {
          case 400:
            messageService.add({
              severity: 'error',
              summary: 'Yêu cầu không hợp lệ',
              detail: error.error?.message || 'Thông tin nhập vào không hợp lệ. Vui lòng kiểm tra lại.'
            });
            break;
          case 401:
            if (!isAuthEndpoint(req.url)) {
              if (typeof window !== 'undefined') {
                localStorage.removeItem('token');
                localStorage.removeItem('refreshToken');
              }
              router.navigate(['/login']);
              messageService.add({
                severity: 'error',
                summary: 'Phiên làm việc hết hạn',
                detail: 'Vui lòng đăng nhập lại.'
              });
            }
            break;
          case 403:
            messageService.add({
              severity: 'warn',
              summary: 'Không có quyền',
              detail: 'Bạn không có quyền thực hiện thao tác này.'
            });
            break;
          case 404:
            messageService.add({
              severity: 'warn',
              summary: 'Không tìm thấy',
              detail: 'Tài nguyên được yêu cầu không tồn tại.'
            });
            break;
          case 500:
            messageService.add({
              severity: 'error',
              summary: 'Lỗi hệ thống',
              detail: 'Lỗi máy chủ nội bộ. Vui lòng thử lại sau.'
            });
            break;
          default:
            messageService.add({
              severity: 'error',
              summary: `Lỗi kết nối (${error.status})`,
              detail: 'Không thể kết nối đến máy chủ.'
            });
            break;
        }
      }
      return throwError(() => error);
    })
  );
};
