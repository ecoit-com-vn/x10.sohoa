import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { catchError, finalize, map, shareReplay, tap } from 'rxjs/operators';
import { APP_CONFIG } from '../config/app-config.token';

export interface UserProfile {
  id: string;
  username: string;
  fullName: string;
  email: string;
  positionId?: number | null;
  positionName?: string | null;
  organizationUnitId?: number | null;
  unitId?: number | null;
  organizationUnit?: { id: number; name: string } | null;
  isActive?: boolean;
  roles?: string[];
  permissions?: string[];
}

export interface UpdateProfileRequest {
  fullName: string;
  email: string;
  positionId?: number | null;
  positionName?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private static readonly PERMISSIONS_STORAGE_KEY = 'userPermissions';

  currentUserPermissions = signal<string[]>([]);
  currentUserProfile = signal<UserProfile | null>(null);
  private refreshInFlight: Observable<string> | null = null;

  constructor() {
    this.restorePermissionsFromStorage();
  }

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

  getProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.base}/profile`).pipe(
      tap((profile) => this.currentUserProfile.set(profile))
    );
  }

  loadProfile(): Observable<UserProfile> {
    return this.getProfile();
  }

  updateProfile(dto: UpdateProfileRequest): Observable<UserProfile> {
    return this.http.put<UserProfile>(`${this.base}/profile`, dto).pipe(
      tap((profile) => this.currentUserProfile.set(profile))
    );
  }

  setPermissions(perms: string[]): void {
    const normalized = perms || [];
    this.currentUserPermissions.set(normalized);
    if (typeof window !== 'undefined') {
      if (normalized.length > 0) {
        sessionStorage.setItem(AuthService.PERMISSIONS_STORAGE_KEY, JSON.stringify(normalized));
      } else {
        sessionStorage.removeItem(AuthService.PERMISSIONS_STORAGE_KEY);
      }
    }
  }

  private restorePermissionsFromStorage(): void {
    if (typeof window === 'undefined' || this.currentUserPermissions().length > 0) {
      return;
    }
    const raw = sessionStorage.getItem(AuthService.PERMISSIONS_STORAGE_KEY);
    if (!raw) {
      return;
    }
    try {
      const parsed = JSON.parse(raw);
      if (Array.isArray(parsed) && parsed.length > 0) {
        this.currentUserPermissions.set(parsed);
      }
    } catch {
      sessionStorage.removeItem(AuthService.PERMISSIONS_STORAGE_KEY);
    }
  }

  ensurePermissionsLoaded(): Observable<string[]> {
    this.restorePermissionsFromStorage();
    const cached = this.currentUserPermissions();
    if (cached.length > 0) {
      return of(cached);
    }
    if (!this.getToken()) {
      return of([]);
    }
    return this.getPermissions().pipe(
      tap((perms) => this.setPermissions(perms || [])),
      catchError(() => of(this.currentUserPermissions()))
    );
  }

  loadPermissions(): void {
    const token = this.getToken();
    if (token) {
      if (this.currentUserPermissions().length === 0) {
        this.ensurePermissionsLoaded().subscribe();
      }
    } else {
      this.setPermissions([]);
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
      sessionStorage.removeItem(AuthService.PERMISSIONS_STORAGE_KEY);
    }
    this.currentUserPermissions.set([]);
    this.currentUserProfile.set(null);
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
