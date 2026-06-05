import { HttpInterceptorFn } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformServer } from '@angular/common';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const platformId = inject(PLATFORM_ID);
  const token = typeof window !== 'undefined' ? localStorage.getItem('token') : null;
  
  let url = req.url;
  if (isPlatformServer(platformId) && url.startsWith('/')) {
    url = `http://apigateway:8080${url}`;
  }

  let headers = req.headers;
  if (token) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  const authReq = req.clone({ url, headers });
  return next(authReq);
};
