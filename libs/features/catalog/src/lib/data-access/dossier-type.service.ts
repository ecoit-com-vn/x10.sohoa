import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class DossierTypeService {
  private api = inject(ApiService);

  private get base() {
    return `/api/catalog/dossier-type`;
  }

  private get formTemplatesBase() {
    return `/api/v1/eav-form-templates`;
  }

  getDossierTypes(page: number, pageSize: number, keyword?: string, status?: string): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (status !== undefined && status !== null && status !== '') {
      params = params.set('status', status);
    }

    return this.api.get<any>(this.base, { params });
  }

  getDossierTypeById(id: string): Observable<any> {
    return this.api.get<any>(`${this.base}/${id}`);
  }

  createDossierType(item: any): Observable<any> {
    return this.api.post<any>(this.base, item);
  }

  updateDossierType(id: string, item: any): Observable<any> {
    return this.api.put<any>(`${this.base}/${id}`, item);
  }

  deleteDossierType(id: string): Observable<any> {
    return this.api.delete<any>(`${this.base}/${id}`);
  }

  toggleStatus(id: string, isLocking: boolean): Observable<any> {
    const action = isLocking ? 'lock' : 'unlock';
    return this.api.post<any>(`${this.base}/${id}/${action}`, {});
  }

  getEavFormTemplates(): Observable<any[]> {
    return this.api.get<any[]>(this.formTemplatesBase);
  }
}
