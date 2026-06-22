import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class DocumentTypeService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get base() {
    return `${this.config.apiGatewayUrl}/api/catalog/document-type`;
  }

  private get formTemplatesBase() {
    return `${this.config.apiGatewayUrl}/api/v1/eav-form-templates`;
  }

  getDocumentTypes(page: number, pageSize: number, keyword?: string, status?: string): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (status !== undefined && status !== null && status !== '') {
      params = params.set('status', status);
    }

    return this.http.get<any>(this.base, { params });
  }

  getDocumentTypeById(id: string): Observable<any> {
    return this.http.get<any>(`${this.base}/${id}`);
  }

  createDocumentType(item: any): Observable<any> {
    return this.http.post<any>(this.base, item);
  }

  updateDocumentType(id: string, item: any): Observable<any> {
    return this.http.put<any>(`${this.base}/${id}`, item);
  }

  deleteDocumentType(id: string): Observable<any> {
    return this.http.delete<any>(`${this.base}/${id}`);
  }

  toggleStatus(id: string, isLocking: boolean): Observable<any> {
    const action = isLocking ? 'lock' : 'unlock';
    return this.http.post<any>(`${this.base}/${id}/${action}`, {});
  }

  getEavFormTemplatesLookup(): Observable<any[]> {
    return this.http.get<any[]>(`${this.formTemplatesBase}/lookup`);
  }
}
