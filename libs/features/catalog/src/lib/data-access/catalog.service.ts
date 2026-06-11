import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class CatalogService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get base() {
    return `${this.config.apiGatewayUrl}/api/catalog`;
  }

  getCatalogTypes(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/types`);
  }

  getItems(catalogType: string, keyword?: string, status?: string): Observable<any[]> {
    let params = new HttpParams().set('catalogType', catalogType);
    
    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (status) {
      params = params.set('status', status);
    }
    
    return this.http.get<any[]>(this.base, { params });
  }

  createItem(item: any): Observable<any> {
    return this.http.post<any>(this.base, item);
  }

  updateItem(id: number | string, item: any): Observable<any> {
    return this.http.put<any>(`${this.base}/${id}`, item);
  }

  deleteItem(id: number | string): Observable<any> {
    return this.http.delete<any>(`${this.base}/${id}`);
  }

  toggleStatus(id: number | string, isLocking: boolean): Observable<any> {
    const action = isLocking ? 'lock' : 'unlock';
    return this.http.post<any>(`${this.base}/${id}/${action}`, {});
  }
}
