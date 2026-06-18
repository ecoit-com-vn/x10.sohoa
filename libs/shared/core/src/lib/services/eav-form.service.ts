import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config.token';

export interface EavFormTemplate {
  id: string;
  name: string;
  code: string;
  category: string;
  description: string;
  descriptionInfo: string;
  formSchema: string; // JSON schema stringified
  version: number;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  status?: string;
  formType?: string;
}

@Injectable({
  providedIn: 'root'
})
export class EavFormService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private get apiUrl() {
    return `${this.config.apiGatewayUrl}/api/v1/eav-form-templates`;
  }

  getTemplates(): Observable<EavFormTemplate[]> {
    return this.http.get<EavFormTemplate[]>(this.apiUrl);
  }

  getTemplateById(id: string): Observable<EavFormTemplate> {
    return this.http.get<EavFormTemplate>(`${this.apiUrl}/${id}`);
  }

  createTemplate(name: string, code: string, category: string, description: string, descriptionInfo: string, formSchema: string, createdBy: string = 'admin'): Observable<EavFormTemplate> {
    return this.http.post<EavFormTemplate>(this.apiUrl, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      formSchema,
      createdBy
    });
  }

  updateTemplate(id: string, name: string, code: string, category: string, description: string, descriptionInfo: string, formSchema: string, updatedBy: string = 'admin'): Observable<EavFormTemplate> {
    return this.http.put<EavFormTemplate>(`${this.apiUrl}/${id}`, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      formSchema,
      updatedBy
    });
  }

  deleteTemplate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  submitTemplate(id: string): Observable<EavFormTemplate> {
    return this.http.put<EavFormTemplate>(`${this.apiUrl}/${id}/submit`, {});
  }

  approveTemplate(id: string): Observable<EavFormTemplate> {
    return this.http.put<EavFormTemplate>(`${this.apiUrl}/${id}/approve`, {});
  }

  rejectTemplate(id: string): Observable<EavFormTemplate> {
    return this.http.put<EavFormTemplate>(`${this.apiUrl}/${id}/reject`, {});
  }

  getCatalogTypes(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/catalog/types`);
  }

  getCatalogTypeByCode(code: string): Observable<any> {
    return this.http.get<any>(`${this.config.apiGatewayUrl}/api/Catalog/types/code/${code}`);
  }

  getCatalogsLookup(catalogTypeId: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/Catalog/lookup?catalogTypeId=${catalogTypeId}`);
  }
}
