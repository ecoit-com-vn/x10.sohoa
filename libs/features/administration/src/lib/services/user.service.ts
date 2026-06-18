import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private api = inject(ApiService);

  private get base() {
    return `/api/v1`;
  }

  private get catalogBase() {
    return `/api/catalog`;
  }

  getUsers(page: number, pageSize: number, keyword?: string): Observable<any> {
    return this.api.get<any>(`${this.base}/users?page=${page}&pageSize=${pageSize}&keyword=${keyword || ''}`);
  }

  createUser(user: any): Observable<any> {
    return this.api.post<any>(`${this.base}/users`, user);
  }

  updateUser(id: string, user: any): Observable<any> {
    return this.api.put<any>(`${this.base}/users/${id}`, user);
  }

  deleteUser(id: string): Observable<any> {
    return this.api.delete<any>(`${this.base}/users/${id}`);
  }

  getOrganizationUnits(): Observable<any[]> {
    return this.api.get<any[]>(`${this.base}/organization-units/lookup`);
  }

  getSystemRoles(): Observable<any[]> {
    return this.api.get<any[]>(`${this.base}/roles/lookup`);
  }

  getUserUnitRoles(userId: string): Observable<any[]> {
    return this.api.get<any[]>(`${this.base}/user-unit-roles/user/${userId}`);
  }

  saveUserUnitRoles(userId: string, assignedRoles: any[]): Observable<any> {
    return this.api.post<any>(`${this.base}/user-unit-roles/user/${userId}`, assignedRoles);
  }

  getUserPermissions(userId: string): Observable<string[]> {
    return this.api.get<string[]>(`${this.base}/users/${userId}/permissions`);
  }

  saveUserPermissions(userId: string, permissions: string[]): Observable<any> {
    return this.api.post<any>(`${this.base}/users/${userId}/permissions`, permissions);
  }

  getSystemPermissions(): Observable<any[]> {
    return this.api.get<any[]>(`${this.base}/permissions/lookup`);
  }

  getMenus(): Observable<any[]> {
    return this.api.get<any[]>(`${this.base}/menus/lookup`);
  }

  getUserRoles(userId: string): Observable<number[]> {
    return this.api.get<number[]>(`${this.base}/users/${userId}/roles`);
  }

  saveUserRoles(userId: string, roleIds: number[]): Observable<any> {
    return this.api.post<any>(`${this.base}/users/${userId}/roles`, roleIds);
  }

  // ── Catalog (EquipmentService) ─────────────────────────────────────────────

  /** Lấy danh sách tất cả loại danh mục (để tìm ID của loại "Chức vụ") */
  getCatalogTypes(): Observable<any[]> {
    return this.api.get<any[]>(`${this.catalogBase}/types/lookup`);
  }

  /** Lấy danh sách catalog đang Active theo loại — dùng cho dropdown Chức vụ */
  getPositions(catalogTypeId: number): Observable<any[]> {
    return this.api.get<any[]>(`${this.catalogBase}/lookup?catalogTypeId=${catalogTypeId}`);
  }
}

