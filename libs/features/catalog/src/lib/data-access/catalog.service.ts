import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class CatalogService {
  private api = inject(ApiService);

  private get base() {
    return `/api/catalog`;
  }

  private getBase(type?: string) {
    if (type === 'KE') return `/api/catalog/shelf`;
    if (type === 'TANG') return `/api/catalog/floor`;
    if (type === 'HOP') return `/api/catalog/box`;
    if (type === 'CHUC_VU') return `/api/catalog/position`;
    if (type === 'LINH_VUC') return `/api/catalog/domain`;
    if (type === 'TINH_TRANG_VAT_LY') return `/api/catalog/physical-status`;
    return `/api/catalog`;
  }

  getCatalogTypes(): Observable<any[]> {
    return this.api.get<any[]>(`${this.base}/types`);
  }

  getItems(catalogType: string, page: number, pageSize: number, keyword?: string, status?: string): Observable<any> {
    const isMappedType = ['KE', 'TANG', 'HOP', 'CHUC_VU', 'LINH_VUC', 'TINH_TRANG_VAT_LY'].includes(catalogType);
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

    return this.api.get<any>(base, { params });
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

    return this.api.get<any>(this.base, { params });
  }

  createItem(item: any, type?: string): Observable<any> {
    const base = this.getBase(type || item.catalogType);
    return this.api.post<any>(base, item);
  }

  updateItem(id: number | string, item: any, type?: string): Observable<any> {
    const base = this.getBase(type || item.catalogType);
    return this.api.put<any>(`${base}/${id}`, item);
  }

  deleteItem(id: number | string, type?: string): Observable<any> {
    const base = this.getBase(type);
    return this.api.delete<any>(`${base}/${id}`);
  }

  toggleStatus(id: number | string, isLocking: boolean, type?: string): Observable<any> {
    const base = this.getBase(type);
    const action = isLocking ? 'lock' : 'unlock';
    return this.api.post<any>(`${base}/${id}/${action}`, {});
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
    return this.api.get<any[]>(`${this.base}/${suffix}`, { params });
  }

  createCatalogType(type: any, isPrivate: boolean = false): Observable<any> {
    const suffix = isPrivate ? 'private' : 'shared';
    return this.api.post<any>(`${this.base}/${suffix}`, type);
  }

  updateCatalogType(id: number | string, type: any, isPrivate: boolean = false): Observable<any> {
    const suffix = isPrivate ? 'private' : 'shared';
    return this.api.put<any>(`${this.base}/${suffix}/${id}`, type);
  }

  deleteCatalogType(id: number | string, isPrivate: boolean = false): Observable<any> {
    const suffix = isPrivate ? 'private' : 'shared';
    return this.api.delete<any>(`${this.base}/${suffix}/${id}`);
  }

  toggleCatalogTypeStatus(id: number | string, isLocking: boolean, isPrivate: boolean = false): Observable<any> {
    const action = isLocking ? 'lock' : 'unlock';
    const suffix = isPrivate ? 'private' : 'shared';
    return this.api.post<any>(`${this.base}/${suffix}/${id}/${action}`, {});
  }
}
