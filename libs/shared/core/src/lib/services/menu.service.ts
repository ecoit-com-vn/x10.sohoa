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

  getSidebarMenu(): Observable<any> {
    return this.http.get<any>(`${this.config.apiGatewayUrl}/api/v1/menus/sidebar`);
  }
}
