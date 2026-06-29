import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { catchError, finalize, map, shareReplay, tap } from 'rxjs/operators';
import { APP_CONFIG } from '../config/app-config.token';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  currentUserPermissions = signal<string[]>([]);
  private refreshInFlight: Observable<string> | null = null;

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

  isTokenExpired(token?: string | null, bufferSeconds = 60): boolean {
    const value = token ?? this.getToken();
    if (!value) return true;
    const payload = this.decodeTokenPayload(value);
    if (!payload?.exp) return true;
    const expiresAtMs = payload.exp * 1000;
    return Date.now() >= expiresAtMs - bufferSeconds * 1000;
  }

  getRefreshToken(): string | null {
    if (typeof window !== 'undefined') {
      return localStorage.getItem('refreshToken');
    }
    return null;
  }

  private storeTokens(accessToken: string, refreshToken?: string | null): void {
    if (typeof window === 'undefined') return;
    localStorage.setItem('token', accessToken);
    if (refreshToken) {
      localStorage.setItem('refreshToken', refreshToken);
    }
  }

  refreshAccessToken(): Observable<string> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token'));
    }

    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }

    this.refreshInFlight = this.http.post<any>(`${this.base}/refresh`, { refreshToken }).pipe(
      map((res) => {
        const accessToken = res.AccessToken || res.accessToken || res.access_token;
        const newRefreshToken = res.RefreshToken || res.refreshToken || res.refresh_token;
        if (!accessToken) {
          throw new Error('Refresh response missing access token');
        }
        this.storeTokens(accessToken, newRefreshToken ?? refreshToken);
        return accessToken as string;
      }),
      catchError((err) => {
        this.logout();
        return throwError(() => err);
      }),
      finalize(() => {
        this.refreshInFlight = null;
      }),
      shareReplay(1)
    );

    return this.refreshInFlight;
  }

  ensureValidToken(): Observable<boolean> {
    const token = this.getToken();
    if (!token) {
      return of(false);
    }
    if (!this.isTokenExpired(token)) {
      return of(true);
    }
    return this.refreshAccessToken().pipe(
      map(() => true),
      catchError(() => of(false))
    );
  }

  getUserRoles(): string[] {
    const token = this.getToken();
    if (!token) return [];
    const payload = this.decodeTokenPayload(token);
    if (!payload) return [];
    const roles = payload['role'] || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || [];
    return Array.isArray(roles) ? roles : [roles];
  }
  getUserId(): string | null {
    const token = this.getToken();
    if (!token) return null;
    const payload = this.decodeTokenPayload(token);
    if (!payload) return null;
    console.log('SOHOA_DEBUG JWT Payload:', payload);
    return payload['id'] || 
           payload['nameid'] || 
           payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || 
           payload['sub'] || 
           payload['unique_name'] || 
           null;
  }
  getPermissions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/permissions`);
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

  getUserUnitId(): number | null {
    const token = this.getToken();
    if (!token) return null;
    const payload = this.decodeTokenPayload(token);
    if (!payload) return null;
    const unitId = payload['unit_id'] || payload['UnitId'] || payload['Unit_Id'];
    return unitId ? parseInt(unitId, 10) : null;
  }
}
