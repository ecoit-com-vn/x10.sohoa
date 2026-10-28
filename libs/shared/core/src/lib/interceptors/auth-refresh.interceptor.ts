import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

function isAuthEndpoint(url: string): boolean {
  return url.includes('/auth/login')
    || url.includes('/auth/sso-login')
    || url.includes('/auth/refresh');
}

let isRefreshing = false;
let refreshTokenSubject = new BehaviorSubject<string | null>(null);

export const authRefreshInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || isAuthEndpoint(req.url)) {
        return throwError(() => error);
      }

      // Nếu token hiện tại trong storage đã khác token được gửi đi trong request,
      // nghĩa là một request song song trước đó đã refresh thành công token rồi.
      // Chỉ cần lấy token mới và retry ngay lập tức, tránh gọi lặp lại refresh.
      const currentToken = authService.getToken();
      const reqAuth = req.headers.get('Authorization');
      if (currentToken && reqAuth && reqAuth !== `Bearer ${currentToken}`) {
        const retryReq = req.clone({
          headers: req.headers.set('Authorization', `Bearer ${currentToken}`)
        });
        return next(retryReq);
      }

      if (!isRefreshing) {
        isRefreshing = true;
        refreshTokenSubject.next(null);

        return authService.refreshAccessToken().pipe(
          switchMap((newToken) => {
            isRefreshing = false;
            refreshTokenSubject.next(newToken);
            const retryReq = req.clone({
              headers: req.headers.set('Authorization', `Bearer ${newToken}`)
            });
            return next(retryReq);
          }),
          catchError((refreshErr) => {
            isRefreshing = false;
            refreshTokenSubject.error(refreshErr);
            refreshTokenSubject = new BehaviorSubject<string | null>(null);
            authService.logout();
            return throwError(() => error);
          })
        );
      } else {
        // Có request đang thực hiện refresh token -> xếp hàng chờ BehaviorSubject phát token mới
        return refreshTokenSubject.pipe(
          filter((token): token is string => token !== null),
          take(1),
          switchMap((newToken) => {
            const retryReq = req.clone({
              headers: req.headers.set('Authorization', `Bearer ${newToken}`)
            });
            return next(retryReq);
          })
        );
      }
    })
  );
};
