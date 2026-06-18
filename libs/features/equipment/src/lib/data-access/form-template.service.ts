import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';

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
  equipmentTypeId?: string;
  formType?: string;
}

@Injectable({
  providedIn: 'root'
})
export class FormTemplateService {
  private api = inject(ApiService);

  private get apiUrl() {
    return `/api/v1/form-templates`;
  }

  getTemplates(): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(this.apiUrl);
  }

  getTemplateById(id: string): Observable<EavFormTemplate> {
    return this.api.get<EavFormTemplate>(`${this.apiUrl}/${id}`);
  }

  createTemplate(
    name: string, 
    code: string, 
    category: string, 
    description: string, 
    descriptionInfo: string, 
    formSchema: string, 
    createdBy: string = 'admin',
    equipmentTypeId?: string
  ): Observable<EavFormTemplate> {
    return this.api.post<EavFormTemplate>(this.apiUrl, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      formSchema,
      createdBy,
      equipmentTypeId
    });
  }

  updateTemplate(
    id: string, 
    name: string, 
    code: string, 
    category: string, 
    description: string, 
    descriptionInfo: string, 
    formSchema: string, 
    updatedBy: string = 'admin',
    equipmentTypeId?: string
  ): Observable<EavFormTemplate> {
    return this.api.put<EavFormTemplate>(`${this.apiUrl}/${id}`, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      formSchema,
      updatedBy,
      equipmentTypeId
    });
  }

  deleteTemplate(id: string): Observable<void> {
    return this.api.delete<void>(`${this.apiUrl}/${id}`);
  }

  getCatalogTypes(): Observable<any[]> {
    return this.api.get<any[]>('/api/catalog/types');
  }

  getCatalogTypeByCode(code: string): Observable<any> {
    return this.api.get<any>(`/api/Catalog/types/code/${code}`);
  }

  getCatalogsLookup(catalogTypeId: number): Observable<any[]> {
    return this.api.get<any[]>(`/api/Catalog/lookup?catalogTypeId=${catalogTypeId}`);
  }
}
