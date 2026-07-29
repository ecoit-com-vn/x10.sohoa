import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class EquipmentTypeService {
  private api = inject(ApiService);
  private gridTypes$: Observable<any[]> | null = null;

  private get base() {
    return `/api/v1/equipmenttype`;
  }

  getEquipmentTypes(
    page: number,
    pageSize: number,
    code?: string,
    name?: string,
    gridTypeId?: number,
    isActive?: boolean
  ): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (code && code.trim()) {
      params = params.set('code', code.trim());
    }
    if (name && name.trim()) {
      params = params.set('name', name.trim());
    }
    if (gridTypeId !== undefined && gridTypeId !== null) {
      params = params.set('gridTypeId', gridTypeId.toString());
    }
    if (isActive !== undefined && isActive !== null) {
      params = params.set('isActive', isActive.toString());
    }

    return this.api.get<any>(this.base, { params });
  }

  getById(id: string): Observable<any> {
    return this.api.get<any>(`${this.base}/${id}`);
  }

  create(item: any): Observable<any> {
    return this.api.post<any>(this.base, item);
  }

  update(id: string, item: any): Observable<any> {
    return this.api.put<any>(`${this.base}/${id}`, item);
  }

  delete(id: string): Observable<any> {
    return this.api.delete<any>(`${this.base}/${id}`);
  }

  toggleStatus(id: string, isLocking: boolean): Observable<any> {
    const action = isLocking ? 'lock' : 'unlock';
    return this.api.post<any>(`${this.base}/${id}/${action}`, {});
  }

  getGridTypesLookup(): Observable<any[]> {
    if (!this.gridTypes$) {
      this.gridTypes$ = this.api.get<any[]>(`${this.base}/grid-types/lookup`)
    }
    return this.gridTypes$;
  }
}
