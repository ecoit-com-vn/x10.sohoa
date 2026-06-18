import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

@Injectable({
  providedIn: 'root'
})
export class MenuService {
  private api = inject(ApiService);

  private get base() {
    return `/api/v1/menus`;
  }

  getSidebarMenu(): Observable<any> {
    return this.api.get<any>(`${this.base}/sidebar`);
  }

  getMenus(): Observable<any> {
    return this.api.get<any>(this.base);
  }

  getPermissions(): Observable<any> {
    return this.api.get<any>(`/api/v1/roles/permissions/all`);
  }

  createMenu(menu: any): Observable<any> {
    return this.api.post<any>(this.base, menu);
  }

  updateMenu(id: number | string, menu: any): Observable<any> {
    return this.api.put<any>(`${this.base}/${id}`, menu);
  }

  deleteMenu(id: number | string): Observable<any> {
    return this.api.delete<any>(`${this.base}/${id}`);
  }
}
