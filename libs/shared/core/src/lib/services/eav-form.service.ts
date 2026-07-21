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
  formSchema?: string;
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
  extractionPosition?: string;
  categoryName?: string;
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
    return this.getApprovalTemplatesForm();
  }

  /** @deprecated Dùng getCompletedTemplatesForm — API tách EavCompletedFormController */
  getCompletedTemplates(): Observable<EavFormTemplate[]> {
    return this.getCompletedTemplatesForm();
  }

  /** @deprecated Dùng getDesignTemplates / getApprovalTemplatesForm / getCompletedTemplatesForm */
  getTemplates(): Observable<EavFormTemplate[]> {
    return this.getDesignTemplates();
  }

  getTemplateById(id: string): Observable<EavFormTemplate> {
    return this.api.get<EavFormTemplate>(`${this.apiUrl}/${id}`);
  }

  createTemplate(name: string, code: string, category: string, description: string, descriptionInfo: string, formSchema: string, createdBy: string = 'admin', gridTypeId?: number, equipmentTypeId?: string, extractionProcess?: string, extractionPosition?: string): Observable<EavFormTemplate> {
    return this.api.post<EavFormTemplate>(this.apiUrl, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      extractionProcess,
      extractionPosition,
      formSchema,
      createdBy,
      gridTypeId,
      equipmentTypeId
    });
  }

  updateTemplate(id: string, name: string, code: string, category: string, description: string, descriptionInfo: string, formSchema: string, updatedBy: string = 'admin', gridTypeId?: number, equipmentTypeId?: string, extractionProcess?: string, extractionPosition?: string): Observable<EavFormTemplate> {
    return this.api.put<EavFormTemplate>(`${this.apiUrl}/${id}`, {
      name,
      code,
      category,
      description,
      descriptionInfo,
      extractionProcess,
      extractionPosition,
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

  getApprovalTemplateVersions(code: string): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(`${this.approvalApiUrl}/code/${code}/versions`);
  }

  getApprovalTemplateByIdAndVersion(id: string, version: number): Observable<EavFormTemplate> {
    return this.api.get<EavFormTemplate>(`${this.approvalApiUrl}/${id}/versions/${version}`);
  }

  restoreApprovalTemplateVersion(id: string, version: number): Observable<{ message?: string }> {
    return this.api.put<{ message?: string }>(`${this.approvalApiUrl}/${id}/versions/${version}/restore`, {});
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

  getCompletedTemplateByIdAndVersion(id: string, version: number): Observable<EavFormTemplate> {
    return this.api.get<EavFormTemplate>(`${this.completedApiUrl}/${id}/versions/${version}`);
  }

  restoreCompletedTemplateVersion(id: string, version: number): Observable<{ message?: string }> {
    return this.api.put<{ message?: string }>(`${this.completedApiUrl}/${id}/versions/${version}/restore`, {});
  }

  /** Lookup danh mục cho preview form hoàn thành — quyền EAV_COMPLETED_FORM_VIEW */
  getCompletedCatalogOptions(typeCode: string): Observable<{ code: string; name: string }[]> {
    return this.api.get<{ code: string; name: string }[]>(
      `${this.completedApiUrl}/catalog-options/${encodeURIComponent(typeCode)}`
    );
  }

  getCompletedHmadCategories(): Observable<{ id: number; code: string; name: string }[]> {
    return this.api.get<{ id: number; code: string; name: string }[]>(`${this.completedApiUrl}/hmad-categories`);
  }

  getCompletedCatalogTypes(): Observable<{ id: number; code: string; name: string }[]> {
    return this.api.get<{ id: number; code: string; name: string }[]>(`${this.completedApiUrl}/catalog-types`);
  }

  // --- API thiết kế form (EavFormTemplateController) — versions ---
  getTemplateVersions(code: string): Observable<EavFormTemplate[]> {
    return this.api.get<EavFormTemplate[]>(`${this.apiUrl}/code/${code}/versions`);
  }

  getTemplateByIdAndVersion(id: string, version: number): Observable<EavFormTemplate> {
    return this.api.get<EavFormTemplate>(`${this.apiUrl}/${id}/versions/${version}`);
  }

  restoreTemplateVersion(id: string, version: number): Observable<{ message?: string }> {
    return this.api.put<{ message?: string }>(`${this.apiUrl}/${id}/versions/${version}/restore`, {});
  }

  /** Lookup danh mục field — quyền EAV_FORM_TEMPLATE_VIEW */
  getDesignCatalogOptions(typeCode: string): Observable<{ code: string; name: string }[]> {
    return this.api.get<{ code: string; name: string }[]>(
      `${this.apiUrl}/catalog-options/${encodeURIComponent(typeCode)}`
    );
  }

  /** Hạng mục HMAD cho dropdown tạo/sửa — quyền EAV_FORM_TEMPLATE_VIEW */
  getDesignHmadCategories(): Observable<{ id: number; code: string; name: string }[]> {
    return this.api.get<{ id: number; code: string; name: string }[]>(`${this.apiUrl}/hmad-categories`);
  }

  /** Loại danh mục cho builder — quyền EAV_FORM_TEMPLATE_VIEW */
  getDesignCatalogTypes(): Observable<{ id: number; code: string; name: string }[]> {
    return this.api.get<{ id: number; code: string; name: string }[]>(`${this.apiUrl}/catalog-types`);
  }

  /** Lookup danh mục preview phê duyệt — quyền EAV_FORM_APPROVAL_VIEW */
  getApprovalCatalogOptions(typeCode: string): Observable<{ code: string; name: string }[]> {
    return this.api.get<{ code: string; name: string }[]>(
      `${this.approvalApiUrl}/catalog-options/${encodeURIComponent(typeCode)}`
    );
  }

  /** @deprecated Dùng lockCompletedTemplate */
  lockTemplate(id: string): Observable<any> {
    return this.lockCompletedTemplate(id);
  }

  /** @deprecated Dùng unlockCompletedTemplate */
  unlockTemplate(id: string): Observable<any> {
    return this.unlockCompletedTemplate(id);
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

  /** Lookup danh mục theo mã CatalogType (vd. EQUIPMENT_STATUS) — 1 lần gọi, không cần resolve id trước. */
  getCatalogsLookupByCode(code: string): Observable<any[]> {
    return this.api.get<any[]>(`/api/Catalog/lookup?code=${encodeURIComponent(code)}`);
  }
}
