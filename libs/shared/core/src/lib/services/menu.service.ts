import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config.token';

@Injectable({
  providedIn: 'root'
})
export class MenuService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get base() {
    return `${this.config.apiGatewayUrl}/api/v1/menus`;
  }

  getSidebarMenu(): Observable<any> {
    return this.http.get<any>(`${this.base}/sidebar`);
  }

  getMenus(): Observable<any> {
    return this.http.get<any>(this.base);
  }

  getPermissions(): Observable<any> {
    return this.http.get<any>(`${this.config.apiGatewayUrl}/api/v1/system-permission-groups/permissions/all`);
  }

  createMenu(menu: any): Observable<any> {
    return this.http.post<any>(this.base, menu);
  }

  updateMenu(id: number | string, menu: any): Observable<any> {
    return this.http.put<any>(`${this.base}/${id}`, menu);
  }

  deleteMenu(id: number | string): Observable<any> {
    return this.http.delete<any>(`${this.base}/${id}`);
  }
}
