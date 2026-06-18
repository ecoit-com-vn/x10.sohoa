import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private api = inject(ApiService);

  currentUserPermissions = signal<string[]>([]);

  private get base() {
    return `/api/v1/auth`;
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

  getUserRoles(): string[] {
    const token = this.getToken();
    if (!token) return [];
    const payload = this.decodeTokenPayload(token);
    if (!payload) return [];
    const roles = payload['role'] || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || [];
    return Array.isArray(roles) ? roles : [roles];
  }
  getPermissions(): Observable<string[]> {
    return this.api.get<string[]>(`${this.base}/permissions`);
  }

  loadPermissions(): void {
    const token = this.getToken();
    if (token) {
      if (this.currentUserPermissions().length === 0) {
        this.getPermissions().subscribe({
          next: (perms) => {
            this.currentUserPermissions.set(perms || []);
          },
          error: () => {
            this.currentUserPermissions.set([]);
          }
        });
      }
    } else {
      this.currentUserPermissions.set([]);
    }
  }

  hasPermission(code: string): boolean {
    return this.currentUserPermissions().includes(code);
  }

  loginLocal(username: string, password: string): Observable<any> {
    return this.api.post<any>(`${this.base}/login`, {
      username,
      password
    });
  }

  verifySsoTicket(ticket: string): Observable<any> {
    return this.api.post<any>(`${this.base}/login?ticket=${ticket}`, {});
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
