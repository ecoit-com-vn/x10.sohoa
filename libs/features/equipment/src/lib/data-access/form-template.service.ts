import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { ApiService } from '@sohoa.frontend/shared/core';

export interface EavFormTemplate {
  id: string;
  name: string;
  code: string;
  category: string;
  description: string;
  descriptionInfo: string;
  extractionProcess?: string;
  formSchema: string; // JSON schema stringified
  version: number;
  isActive: boolean;
  isDeleted?: boolean;
  isLocked?: boolean;
  createdAt: string;
  createdBy: string;
  status?: string;
  equipmentTypeId?: string;
  formType?: string;
  gridTypeId?: number;
  gridTypeName?: string;
}

@Injectable({
  providedIn: 'root'
})
export class FormTemplateService {
  private api = inject(ApiService);
  private templates$: Observable<EavFormTemplate[]> | null = null;

  private get apiUrl() {
    return `/api/v1/form-templates`;
  }

  getTemplates(keyword?: string): Observable<EavFormTemplate[]> {
    if (!this.templates$ || keyword) {
      const options = keyword
        ? { params: { keyword: keyword.trim() } }
        : undefined;
      this.templates$ = this.api.get<EavFormTemplate[]>(this.apiUrl, options);
    }
    return this.templates$;
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
    equipmentTypeId?: string,
    gridTypeId?: number,
    extractionProcess?: string
  ): Observable<EavFormTemplate> {
    return this.api.post<EavFormTemplate>(this.apiUrl, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      extractionProcess,
      formSchema,
      createdBy,
      equipmentTypeId,
      gridTypeId
    }).pipe(
      tap(() => this.templates$ = null)
    );
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
    equipmentTypeId?: string,
    gridTypeId?: number,
    extractionProcess?: string
  ): Observable<EavFormTemplate> {
    return this.api.put<EavFormTemplate>(`${this.apiUrl}/${id}`, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      extractionProcess,
      formSchema,
      updatedBy,
      equipmentTypeId,
      gridTypeId
    }).pipe(
      tap(() => this.templates$ = null)
    );
  }

  deleteTemplate(id: string): Observable<void> {
    return this.api.delete<void>(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.templates$ = null)
    );
  }

  getCatalogTypes(): Observable<any[]> {
    return this.api.get<any[]>('/api/catalog/types');
  }

  getCatalogTypeByCode(code: string): Observable<any> {
    return this.api.get<any>(`/api/Catalog/types/code/${code}`);
  }

  lockTemplate(id: string): Observable<any> {
    return this.api.post<any>(`${this.apiUrl}/${id}/lock`, {}).pipe(
      tap(() => this.templates$ = null)
    );
  }

  unlockTemplate(id: string): Observable<any> {
    return this.api.post<any>(`${this.apiUrl}/${id}/unlock`, {}).pipe(
      tap(() => this.templates$ = null)
    );
  }

  getTemplateVersions(code: string): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(`${this.apiUrl}/code/${code}/versions`);
  }
}
