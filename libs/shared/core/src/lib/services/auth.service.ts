import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config.token';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  currentUserPermissions = signal<string[]>([]);

  private get base() {
    return `${this.config.apiGatewayUrl}/api/v1/auth`;
  }

  private decodeTokenPayload(token: string): any {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return null;
      const base64Url = parts[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(
        atob(base64)
          .split('')
          .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join('')
      );
      return JSON.parse(jsonPayload);
    } catch {
      return null;
    }
  }

  loadPermissionsFromToken(): void {
    const token = this.getToken();
    if (token) {
      const payload = this.decodeTokenPayload(token);
      const perms = payload?.permissionCodes || payload?.permissions || payload?.permission || [];
      this.currentUserPermissions.set(Array.isArray(perms) ? perms : [perms]);
    } else {
      this.currentUserPermissions.set([]);
    }
  }

  hasPermission(code: string): boolean {
    return this.currentUserPermissions().includes(code);
  }

  loginLocal(username: string, password: string): Observable<any> {
    return this.http.post<any>(`${this.base}/login`, {
      username,
      password
    });
  }

  verifySsoTicket(ticket: string): Observable<any> {
    return this.http.post<any>(`${this.base}/login?ticket=${ticket}`, {});
  }

  redirectToSso(): void {
    if (typeof window !== 'undefined') {
      const appCode = 'SOHOAX10';
      const redirectUrl = encodeURIComponent(window.location.origin + '/login');
      window.location.href = `https://sso.evnhanoi.vn//sso/login?appCode=${appCode}&returnUrl=${redirectUrl}`;
    }
  }

  logout(): void {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('token');
      localStorage.removeItem('refreshToken');
    }
    this.currentUserPermissions.set([]);
  }

  getToken(): string | null {
    if (typeof window !== 'undefined') {
      return localStorage.getItem('token');
    }
    return null;
  }
}
