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

  private getBase(type?: string) {
    if (type === 'KE') return `${this.config.apiGatewayUrl}/api/catalog/shelf`;
    if (type === 'TANG') return `${this.config.apiGatewayUrl}/api/catalog/floor`;
    if (type === 'HOP') return `${this.config.apiGatewayUrl}/api/catalog/box`;
    if (type === 'CHUC_VU') return `${this.config.apiGatewayUrl}/api/catalog/position`;
    if (type === 'PROCESSING_CATEGORY') return `${this.config.apiGatewayUrl}/api/catalog/processing-category`;
    if (type === 'LINH_VUC') return `${this.config.apiGatewayUrl}/api/catalog/domain`;
    if (type === 'TINH_TRANG_VAT_LY') return `${this.config.apiGatewayUrl}/api/catalog/physical-status`;
    return `${this.config.apiGatewayUrl}/api/catalog`;
  }

  getCatalogTypes(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/types`);
  }

  getItems(catalogType: string, page: number, pageSize: number, keyword?: string, status?: string): Observable<any> {
    const isMappedType = ['KE', 'TANG', 'HOP', 'CHUC_VU', 'PROCESSING_CATEGORY', 'LINH_VUC', 'TINH_TRANG_VAT_LY'].includes(catalogType);
    const base = this.getBase(catalogType);
    
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    
    if (!isMappedType) {
      params = params.set('catalogType', catalogType);
    }
    
    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (status) {
      params = params.set('status', status);
    }
    
    return this.http.get<any>(base, { params });
  }

  getItemsByTypeId(catalogTypeId: number, page: number, pageSize: number, keyword?: string, status?: string): Observable<any> {
    let params = new HttpParams()
      .set('catalogTypeId', catalogTypeId.toString())
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    
    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (status) {
      params = params.set('status', status);
    }
    
    return this.http.get<any>(this.base, { params });
  }

  createItem(item: any, type?: string): Observable<any> {
    const base = this.getBase(type || item.catalogType);
    return this.http.post<any>(base, item);
  }

  updateItem(id: number | string, item: any, type?: string): Observable<any> {
    const base = this.getBase(type || item.catalogType);
    return this.http.put<any>(`${base}/${id}`, item);
  }

  deleteItem(id: number | string, type?: string): Observable<any> {
    const base = this.getBase(type);
    return this.http.delete<any>(`${base}/${id}`);
  }

  toggleStatus(id: number | string, isLocking: boolean, type?: string): Observable<any> {
    const base = this.getBase(type);
    const action = isLocking ? 'lock' : 'unlock';
    return this.http.post<any>(`${base}/${id}/${action}`, {});
  }

  getSharedCatalogTypes(keyword?: string, status?: string, isPrivate: boolean = false): Observable<any[]> {
    let params = new HttpParams();
    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (status) {
      params = params.set('status', status);
    }
    const suffix = isPrivate ? 'private' : 'shared';
    return this.http.get<any[]>(`${this.base}/${suffix}`, { params });
  }

  createCatalogType(type: any, isPrivate: boolean = false): Observable<any> {
    const suffix = isPrivate ? 'private' : 'shared';
    return this.http.post<any>(`${this.base}/${suffix}`, type);
  }

  updateCatalogType(id: number | string, type: any, isPrivate: boolean = false): Observable<any> {
    const suffix = isPrivate ? 'private' : 'shared';
    return this.http.put<any>(`${this.base}/${suffix}/${id}`, type);
  }

  deleteCatalogType(id: number | string, isPrivate: boolean = false): Observable<any> {
    const suffix = isPrivate ? 'private' : 'shared';
    return this.http.delete<any>(`${this.base}/${suffix}/${id}`);
  }

  toggleCatalogTypeStatus(id: number | string, isLocking: boolean, isPrivate: boolean = false): Observable<any> {
    const action = isLocking ? 'lock' : 'unlock';
    const suffix = isPrivate ? 'private' : 'shared';
    return this.http.post<any>(`${this.base}/${suffix}/${id}/${action}`, {});
  }
}
