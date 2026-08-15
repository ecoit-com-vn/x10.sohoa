import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { DigitizationProcessOption } from '@sohoa.frontend/features/dossier-management';

export interface PmisSpecDiffResponse {
  formSchema: string | null;
  localFormValues: string | null;
  pmisFormValues: string | null;
  pmisSyncedAt: string | null;
  fieldMappingWarnings: { fieldName: string; label: string }[];
}

@Injectable({
  providedIn: 'root'
})
export class EquipmentService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get base() {
    return `${this.config.apiGatewayUrl}/api/v1/equipment`;
  }

  getEquipments(
    page: number,
    pageSize: number,
    code?: string,
    name?: string,
    unitId?: number,
    infrastructureId?: string,
    gridTypeId?: number,
    equipmentTypeId?: string,
    isActive?: boolean,
    keyword?: string
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
    if (unitId !== undefined && unitId !== null) {
      params = params.set('unitId', unitId.toString());
    }
    if (infrastructureId && infrastructureId.trim()) {
      params = params.set('infrastructureId', infrastructureId.trim());
    }
    if (gridTypeId !== undefined && gridTypeId !== null) {
      params = params.set('gridTypeId', gridTypeId.toString());
    }
    if (equipmentTypeId && equipmentTypeId.trim()) {
      params = params.set('equipmentTypeId', equipmentTypeId.trim());
    }
    if (isActive !== undefined && isActive !== null) {
      params = params.set('isActive', isActive.toString());
    }
    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }

    return this.http.get<any>(this.base, { params });
  }

  getById(id: string): Observable<any> {
    return this.http.get<any>(`${this.base}/${id}`);
  }

  getSubstationSearchById(id: string): Observable<any> {
    return this.http.get<any>(`${this.config.apiGatewayUrl}/api/catalog/substation-search/equipments/${id}`);
  }

  getDossierEquipmentSearchById(dossierId: string, equipmentId: string): Observable<any> {
    return this.http.get<any>(
      `${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment/${dossierId}/equipments/${equipmentId}`
    );
  }

  confirmEquipment(id: string): Observable<any> {
    return this.http.put<any>(`${this.base}/${id}/confirm`, null);
  }

  checkCodeExists(code: string, excludeId?: string): Observable<boolean> {
    let params = new HttpParams().set('code', code.trim());
    if (excludeId?.trim()) {
      params = params.set('excludeId', excludeId.trim());
    }
    return this.http.get<{ exists: boolean }>(`${this.base}/check-code`, { params }).pipe(
      map(res => !!res?.exists)
    );
  }

  create(item: any): Observable<any> {
    return this.http.post<any>(this.base, item);
  }

  update(id: string, item: any): Observable<any> {
    return this.http.put<any>(`${this.base}/${id}`, item);
  }

  copyById(id: string, infrastructureId: string, note?: string): Observable<any> {
    const params = new HttpParams()
      .set('InfrastructureId', String(infrastructureId))
      .set('note', note ?? '');
    return this.http.post<any>(`${this.base}/${id}/copy-byid`, null, { params });
  }

  copyDetailById(id: string): Observable<any> {
    return this.http.post<any>(`${this.base}/${id}/copy-detailbyid`, {});
  }

  delete(id: string): Observable<any> {
    return this.http.delete<any>(`${this.base}/${id}`);
  }

  toggleStatus(id: string, isLocking: boolean): Observable<any> {
    const action = isLocking ? 'lock' : 'unlock';
    return this.http.post<any>(`${this.base}/${id}/${action}`, {});
  }

  getLookup(): Observable<any> {
    return this.http.get<any>(`${this.base}/lookup`);
  }

  getOrganizationUnits(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/organization-units/lookup`);
  }

  getAllOrganizationUnits(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/organization-units/lookup-all-active`);
  }

  getInfrastructures(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/get-infrastructures`);
  }

  /** Trạm/Đường dây của tất cả đơn vị — dùng cho dialog Chuyển thiết bị, không giới hạn theo đơn vị người dùng. */
  getAllInfrastructures(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/get-infrastructures-all`);
  }

  getGridTypes(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/get-grid-types`);
  }

  getEquipmentTypes(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/get-equipment-types`);
  }

  updateFormValues(id: string, formValues: string): Observable<any> {
    return this.http.put<any>(`${this.base}/${id}/form-values`, { formValues });
  }

  /** Biểu mẫu EAV thông số theo loại thiết bị — quyền EQUIPMENT_VIEW. */
  getFormTemplate(id: string): Observable<{ id?: string; name?: string; formSchema?: string }> {
    return this.http.get<{ id?: string; name?: string; formSchema?: string }>(`${this.base}/${id}/form-template`);
  }

  /** Module 6 — so sánh thông số kỹ thuật đồng bộ từ PMIS với dữ liệu nội bộ. */
  getPmisSpecDiff(id: string): Observable<PmisSpecDiffResponse> {
    return this.http.get<PmisSpecDiffResponse>(`${this.base}/${id}/pmis-spec-diff`);
  }

  /** Lấy danh sách tài liệu lý lịch thiết bị kỹ thuật EAV/OCR. */
  getProfileDocuments(
    equipmentId: string,
    page: number,
    pageSize: number,
    keyword?: string
  ): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    return this.http.get<any>(`${this.base}/${equipmentId}/profile-documents`, { params });
  }

  getPublishedProfileDocuments(
    equipmentId: string,
    page: number,
    pageSize: number,
    keyword?: string
  ): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    return this.http.get<any>(`${this.base}/${equipmentId}/published-profile-documents`, { params });
  }

  getFactoryAcceptanceDocuments(
    equipmentId: string,
    page: number,
    pageSize: number,
    keyword?: string
  ): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    return this.http.get<any>(`${this.base}/${equipmentId}/factory-acceptance-documents`, { params });
  }

  getExternalFactoryAcceptanceEquipments(page = 1, pageSize = 1000, keyword?: string): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (keyword?.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    return this.http.get<any>(`${this.base}/external/factory-acceptance`, { params });
  }

  getFactoryAcceptanceEquipmentDetail(
    equipmentId: string,
    page = 1,
    pageSize = 10,
    keyword?: string
  ): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (keyword?.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    return this.http.get<any>(`${this.base}/factory-acceptance/${equipmentId}`, { params });
  }

  getFactoryProfileEquipmentDetail(
    equipmentId: string,
    page = 1,
    pageSize = 10,
    keyword?: string
  ): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (keyword?.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    return this.http.get<any>(`${this.base}/factory-profile/${equipmentId}`, { params });
  }

  getCbmDocumentsEquipmentDetail(
    equipmentId: string,
    page = 1,
    pageSize = 10,
    keyword?: string
  ): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (keyword?.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    return this.http.get<any>(`${this.base}/cbm-documents/${equipmentId}`, { params });
  }

  /** Gửi OCR + Bóc tách tài liệu lý lịch theo biểu mẫu thiết bị. */
  submitDocumentDigitizationOnly(equipmentId: string, versionId: string): Observable<any> {
    return this.http.post<any>(`${this.base}/${equipmentId}/documents/${versionId}/digitization`, {});
  }

  /** OCR/bóc tách tài liệu hồ sơ liên quan — dùng biểu mẫu thiết bị, quyền EQUIPMENT_EDIT. */
  submitDossierDocumentDigitization(
    equipmentId: string,
    dossierId: string,
    versionId: string,
    processOption: DigitizationProcessOption = 'OcrAndExtract'
  ): Observable<any> {
    return this.http.post<any>(
      `${this.base}/${equipmentId}/dossiers/${dossierId}/documents/${versionId}/digitization`,
      { processOption }
    );
  }

  /** Bóc tách lại tài liệu hồ sơ liên quan — quyền EQUIPMENT_EDIT. */
  rerunDossierDocumentExtraction(
    equipmentId: string,
    dossierId: string,
    versionId: string
  ): Observable<any> {
    return this.http.post<any>(
      `${this.base}/${equipmentId}/dossiers/${dossierId}/documents/${versionId}/digitization/rerun-extraction`,
      {}
    );
  }

  /** Bóc tách lại tài liệu lý lịch theo biểu mẫu thiết bị — quyền EQUIPMENT_EDIT. */
  rerunEquipmentDocumentExtraction(equipmentId: string, versionId: string): Observable<any> {
    return this.http.post<any>(
      `${this.base}/${equipmentId}/documents/${versionId}/digitization/rerun-extraction`,
      {}
    );
  }

  /** Lấy kết quả bóc tách của tài liệu theo thiết bị. */
  getDigitizationResultForEquipment(equipmentId: string, versionId: string): Observable<any> {
    return this.http.get<any>(`${this.base}/${equipmentId}/documents/${versionId}/digitization/result`);
  }

  /** Lấy tiến trình OCR (kèm bucket/filePath nguồn) — dùng cho phân hệ Module OCR (Nhóm A). */
  getDigitizationProgressForEquipment(equipmentId: string, versionId: string): Observable<any> {
    return this.http.get<any>(`${this.base}/${equipmentId}/documents/${versionId}/digitization/progress`);
  }

  /** 404 = chưa có kết quả bóc tách (null). */
  getDigitizationResultForEquipmentOrNull(equipmentId: string, versionId: string): Observable<any | null> {
    return this.getDigitizationResultForEquipment(equipmentId, versionId).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          return of(null);
        }
        return throwError(() => error);
      })
    );
  }

  /** Lưu kết quả bóc tách; updateEquipmentFormValues=true → thay toàn bộ thông số thiết bị. */
  saveEquipmentExtractionData(
    equipmentId: string,
    versionId: string,
    mergedDataJson: string,
    updateEquipmentFormValues = false
  ): Observable<any> {
    return this.http.put<any>(
      `${this.base}/${equipmentId}/documents/${versionId}/digitization/result`,
      { mergedDataJson, updateEquipmentFormValues }
    );
  }
}
