import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface EavFormTemplate {
  id: string;
  name: string;
  code: string;
  category: string;
  description: string;
  descriptionInfo: string;
  extractionProcess?: string;
  formSchema: string; 
  version: number;
  isActive: boolean;
  isDeleted?: boolean;
  isLocked?: boolean;
  createdAt: string;
  createdBy: string;
  creatorFullName: string;
  status?: string;
  formType?: string;
  gridTypeId?: number;
  gridTypeName?: string;
  equipmentTypeId?: string;
  equipmentTypeName?: string;
}

@Injectable({
  providedIn: 'root'
})
export class EavFormService {
  private api = inject(ApiService);

  private get apiUrl() {
    return `/api/v1/eav-form-templates`;
  }

  getDesignTemplates(): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(`${this.apiUrl}/design`);
  }

  getApprovalTemplates(): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(`${this.apiUrl}/approval`);
  }

  getCompletedTemplates(): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(`${this.apiUrl}/completed`);
  }

  /** @deprecated Dùng getDesignTemplates / getApprovalTemplates / getCompletedTemplates */
  getTemplates(): Observable<EavFormTemplate[]> {
    return this.getDesignTemplates();
  }

  getTemplateById(id: string): Observable<EavFormTemplate> {
    return this.api.get<EavFormTemplate>(`${this.apiUrl}/${id}`);
  }

  createTemplate(name: string, code: string, category: string, description: string, descriptionInfo: string, formSchema: string, createdBy: string = 'admin', gridTypeId?: number, equipmentTypeId?: string, extractionProcess?: string): Observable<EavFormTemplate> {
    return this.api.post<EavFormTemplate>(this.apiUrl, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      extractionProcess,
      formSchema,
      createdBy,
      gridTypeId,
      equipmentTypeId
    });
  }

  updateTemplate(id: string, name: string, code: string, category: string, description: string, descriptionInfo: string, formSchema: string, updatedBy: string = 'admin', gridTypeId?: number, equipmentTypeId?: string, extractionProcess?: string): Observable<EavFormTemplate> {
    return this.api.put<EavFormTemplate>(`${this.apiUrl}/${id}`, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      extractionProcess,
      formSchema,
      updatedBy,
      gridTypeId,
      equipmentTypeId
    });
  }

  deleteTemplate(id: string): Observable<void> {
    return this.api.delete<void>(`${this.apiUrl}/${id}`);
  }

  submitTemplate(id: string): Observable<EavFormTemplate> {
    return this.api.put<EavFormTemplate>(`${this.apiUrl}/${id}/submit`, {});
  }

  // --- API cho Phê duyệt (EavFormApprovalController) ---
  private get approvalApiUrl() {
    return `/api/v1/eav-form-approvals`;
  }

  getApprovalTemplatesForm(): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(this.approvalApiUrl);
  }

  getApprovalTemplateById(id: string): Observable<EavFormTemplate> {
    return this.api.get<EavFormTemplate>(`${this.approvalApiUrl}/${id}`);
  }

  approveTemplate(id: string): Observable<EavFormTemplate> {
    return this.api.put<EavFormTemplate>(`${this.approvalApiUrl}/${id}/approve`, {});
  }

  rejectTemplate(id: string): Observable<EavFormTemplate> {
    return this.api.put<EavFormTemplate>(`${this.approvalApiUrl}/${id}/reject`, {});
  }

  // --- API cho Form hoàn thành (EavCompletedFormController) ---
  private get completedApiUrl() {
    return `/api/v1/eav-completed-forms`;
  }

  getCompletedTemplatesForm(): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(this.completedApiUrl);
  }

  getCompletedTemplateById(id: string): Observable<EavFormTemplate> {
    return this.api.get<EavFormTemplate>(`${this.completedApiUrl}/${id}`);
  }

  lockCompletedTemplate(id: string): Observable<any> {
    return this.api.post<any>(`${this.completedApiUrl}/${id}/lock`, {});
  }

  unlockCompletedTemplate(id: string): Observable<any> {
    return this.api.post<any>(`${this.completedApiUrl}/${id}/unlock`, {});
  }

  deleteCompletedTemplate(id: string): Observable<void> {
    return this.api.delete<void>(`${this.completedApiUrl}/${id}`);
  }

  getCompletedTemplateVersions(code: string): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(`${this.completedApiUrl}/code/${code}/versions`);
  }

  // --- API cũ (giữ nguyên để tránh lỗi biên dịch) ---
  lockTemplate(id: string): Observable<any> {
    return this.api.post<any>(`${this.apiUrl}/${id}/lock`, {});
  }

  unlockTemplate(id: string): Observable<any> {
    return this.api.post<any>(`${this.apiUrl}/${id}/unlock`, {});
  }

  getTemplateVersions(code: string): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(`${this.apiUrl}/code/${code}/versions`);
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
