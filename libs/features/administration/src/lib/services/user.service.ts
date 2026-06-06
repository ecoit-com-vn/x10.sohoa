import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get base() {
    return `${this.config.apiGatewayUrl}/api/v1`;
  }

  getUsers(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/users`);
  }

  createUser(user: any): Observable<any> {
    return this.http.post<any>(`${this.base}/users`, user);
  }

  updateUser(id: string, user: any): Observable<any> {
    return this.http.put<any>(`${this.base}/users/${id}`, user);
  }

  deleteUser(id: string): Observable<any> {
    return this.http.delete<any>(`${this.base}/users/${id}`);
  }

  getOrganizationUnits(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/organization-units/lookup`);
  }

  getSystemRoles(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/roles/lookup`);
  }

  getUserUnitRoles(userId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/user-unit-roles/user/${userId}`);
  }

  saveUserUnitRoles(userId: string, assignedRoles: any[]): Observable<any> {
    return this.http.post<any>(`${this.base}/user-unit-roles/user/${userId}`, assignedRoles);
  }

  getUserPermissions(userId: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/users/${userId}/permissions`);
  }

  saveUserPermissions(userId: string, permissions: string[]): Observable<any> {
    return this.http.post<any>(`${this.base}/users/${userId}/permissions`, permissions);
  }

  getSystemPermissions(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/permissions/lookup`);
  }

  getMenus(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/menus/lookup`);
  }

  getUserRoles(userId: string): Observable<number[]> {
    return this.http.get<number[]>(`${this.base}/users/${userId}/roles`);
  }

  saveUserRoles(userId: string, roleIds: number[]): Observable<any> {
    return this.http.post<any>(`${this.base}/users/${userId}/roles`, roleIds);
  }
}
