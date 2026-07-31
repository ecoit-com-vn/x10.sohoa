import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, switchMap, forkJoin, of, catchError, map } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { DossierListTab, DossierMenuScope, DossierTabCounts } from '../utils/dossier-status.util';

export interface DossierWorkflowAction {
  code?: string;
  name: string;
  nextNodeId: string;
  requiresNextAssignee?: boolean;
  nextStepRole?: string | null;
  /** Danh sách ID nhóm quyền đơn vị của bước tiếp theo (CSV). */
  unitGroupIds?: string | null;
  /** Danh sách ID nhóm quyền hệ thống của bước tiếp theo (CSV). */
  systemGroupIds?: string | null;
  /** Bắt buộc người xử lý tiếp theo phải cùng đơn vị với người chuyển bước. */
  requireSameUnit?: boolean;
  /** ID "Người cụ thể" của bước tiếp theo — 1 ID hoặc CSV nhiều ID. */
  staticAssigneeId?: string | null;
}

function normalizeCsvField(raw: Record<string, unknown>, camelKey: string, pascalKey: string): string | null {
  const value = raw[camelKey] ?? raw[pascalKey];
  return value != null && String(value).trim() !== '' ? String(value).trim() : null;
}

function normalizeBooleanField(raw: Record<string, unknown>, camelKey: string, pascalKey: string): boolean {
  const value = raw[camelKey] ?? raw[pascalKey];
  return value === true
    || value === 1
    || (typeof value === 'string' && ['true', '1', 'yes'].includes(value.trim().toLowerCase()));
}

export function normalizeDossierWorkflowAction(raw: Record<string, unknown>): DossierWorkflowAction {
  return {
    code: String(raw['code'] ?? raw['Code'] ?? ''),
    name: String(raw['name'] ?? raw['Name'] ?? ''),
    nextNodeId: String(raw['nextNodeId'] ?? raw['NextNodeId'] ?? ''),
    requiresNextAssignee: normalizeBooleanField(raw, 'requiresNextAssignee', 'RequiresNextAssignee'),
    nextStepRole: normalizeCsvField(raw, 'nextStepRole', 'NextStepRole'),
    unitGroupIds: normalizeCsvField(raw, 'unitGroupIds', 'UnitGroupIds'),
    systemGroupIds: normalizeCsvField(raw, 'systemGroupIds', 'SystemGroupIds'),
    requireSameUnit: normalizeBooleanField(raw, 'requireSameUnit', 'RequireSameUnit'),
    staticAssigneeId: normalizeCsvField(raw, 'staticAssigneeId', 'StaticAssigneeId'),
  };
}

export interface BhsCatalogColumn {
  /** Key hiển thị — trùng catalog.Name, map vào catalogData */
  key: string;
  /** Key trong FormDataJson — catalog.Code (EAV field key) */
  code: string;
  label: string;
  priority: number;
}

@Injectable({
  providedIn: 'root'
})
export class DossierManagementService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private kindId = 2;

  /** Gọi từ shell component theo route data (kindId: 1 = digitization). */
  setKindContext(kindId: number): void {
    this.kindId = kindId;
  }

  private get isDigitization(): boolean {
    return this.kindId === 1;
  }

  private baseFor(kindId?: number): string {
    const digitization = (kindId ?? this.kindId) === 1;
    return digitization
      ? `${this.config.apiGatewayUrl}/api/v1/dossier-digitization/dossiers`
      : `${this.config.apiGatewayUrl}/api/v1/dossiers`;
  }

  private get base() {
    return this.baseFor(this.kindId);
  }

  /** Tác vụ workflow hồ sơ — digitization dùng controller riêng (WorkflowTypeId=3). */
  private workflowBaseFor(kindId?: number): string {
    const digitization = (kindId ?? this.kindId) === 1;
    return digitization
      ? `${this.config.apiGatewayUrl}/api/v1/dossier-digitization-workflow`
      : `${this.config.apiGatewayUrl}/api/v1/dossiers-workflow`;
  }

  private get workflowBase() {
    return this.workflowBaseFor(this.kindId);
  }

  private get searchBase() {
    return `${this.config.apiGatewayUrl}/api/v1/search/dossiers`;
  }

  private get dossierWarehouseSearchBase() {
    return `${this.config.apiGatewayUrl}/api/v1/dossiers/search`;
  }

  private get catalogBase() {
    return `${this.config.apiGatewayUrl}/api/catalog`;
  }

  private get dossierSetBase() {
    return `${this.config.apiGatewayUrl}/api/v1/dossier-sets`;
  }

  // ===== DANH SÁCH (ES qua NotificationService) =====

  getDossiers(filter: {
    menuScope?: DossierMenuScope;
    tab?: DossierListTab;
    kindId?: number;
    keyword?: string;
    infrastructureId?: string;
    gridTypeId?: number;
    unitId?: number;
    statusId?: number;
    dossierTypeId?: string;
    equipmentId?: string;
    page: number;
    pageSize: number;
  }): Observable<any> {
    let params = new HttpParams()
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.menuScope) params = params.set('menuScope', filter.menuScope);
    const effectiveKindId = filter.kindId ?? this.kindId;
    if (effectiveKindId) params = params.set('kindId', effectiveKindId.toString());

    if (filter.tab) params = params.set('tab', filter.tab);
    if (filter.keyword?.trim()) params = params.set('keyword', filter.keyword.trim());
    if (filter.infrastructureId) params = params.set('infrastructureId', filter.infrastructureId);
    if (filter.gridTypeId != null) params = params.set('gridTypeId', filter.gridTypeId.toString());
    if (filter.unitId != null) params = params.set('unitId', filter.unitId.toString());
    if (filter.statusId != null) params = params.set('statusId', filter.statusId.toString());
    if (filter.dossierTypeId) params = params.set('dossierTypeId', filter.dossierTypeId);
    if (filter.equipmentId) params = params.set('equipmentId', filter.equipmentId);

    const isDraftCreator = filter.tab === 'draft' && filter.menuScope === 'creator';
    const url = isDraftCreator ? this.base : this.searchBase;
    return this.http.get<any>(url, { params });
  }

  getCatalogDossiers(filter: {
    keyword?: string;
    infrastructureId?: string;
    unitId?: number;
    dossierTypeId?: string;
    page: number;
    pageSize: number;
  }): Observable<any> {
    let params = new HttpParams()
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.keyword?.trim()) params = params.set('keyword', filter.keyword.trim());
    if (filter.infrastructureId) params = params.set('infrastructureId', filter.infrastructureId);
    if (filter.unitId != null) params = params.set('unitId', filter.unitId.toString());
    if (filter.dossierTypeId) params = params.set('dossierTypeId', filter.dossierTypeId);

    return this.http.get<any>(`${this.dossierWarehouseSearchBase}/catalog`, { params });
  }

  /** Chi tiết hồ sơ đã xuất bản — màn Tìm kiếm hồ sơ trong kho */
  getWarehouseSearchDossierById(id: string): Observable<any> {
    return this.http.get<any>(`${this.dossierWarehouseSearchBase}/${id}`);
  }

  /** Thiết bị liên quan — màn Tìm kiếm hồ sơ trong kho */
  getWarehouseSearchEquipments(id: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.dossierWarehouseSearchBase}/${id}/equipments`);
  }

  getDossierTabCounts(filter: {
    menuScope: DossierMenuScope;
    kindId?: number;
    keyword?: string;
    infrastructureId?: string;
    gridTypeId?: number;
    unitId?: number;
    dossierTypeId?: string;
    equipmentId?: string;
  }): Observable<DossierTabCounts> {
    let params = new HttpParams().set('menuScope', filter.menuScope);
    const effectiveKindId = filter.kindId ?? this.kindId;
    if (effectiveKindId) params = params.set('kindId', effectiveKindId.toString());
    if (filter.keyword?.trim()) params = params.set('keyword', filter.keyword.trim());
    if (filter.infrastructureId) params = params.set('infrastructureId', filter.infrastructureId);
    if (filter.gridTypeId != null) params = params.set('gridTypeId', filter.gridTypeId.toString());
    if (filter.unitId != null) params = params.set('unitId', filter.unitId.toString());
    if (filter.dossierTypeId) params = params.set('dossierTypeId', filter.dossierTypeId);
    if (filter.equipmentId) params = params.set('equipmentId', filter.equipmentId);

    return this.http.get<DossierTabCounts>(`${this.searchBase}/tab-counts`, { params });
  }

  /** Cột động danh sách — catalog thuộc catalogType.Code = BHS, sắp theo priority */
  getBhsCatalogColumns(): Observable<BhsCatalogColumn[]> {
    return this.http.get<any>(`${this.catalogBase}/types/code/BHS`).pipe(
      switchMap((type) => {
        if (!type?.id) return of([]);
        const params = new HttpParams().set('catalogTypeId', String(type.id));
        return this.http.get<any[]>(`${this.catalogBase}/lookup`, { params });
      }),
      map((catalogs) => (catalogs ?? [])
        .filter((c) => c.status === 1 || c.status === undefined)
        .sort((a, b) => (a.priority ?? 0) - (b.priority ?? 0))
        .map((c) => ({
          key: c.name,
          code: c.code,
          label: c.name,
          priority: c.priority ?? 0,
        }))),
      catchError(() => of([]))
    );
  }

  // ===== CHI TIẾT =====

  getDossierById(id: string): Observable<any> {
    return this.http.get<any>(`${this.base}/${id}`);
  }

  /** Chi tiết hồ sơ đã xuất bản — màn tra cứu hồ sơ thiết bị */
  getDossierByEquipmentLookup(id: string): Observable<any> {
    return this.http.get<any>(`${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment/${id}`);
  }

  // ===== CRUD =====

  createDossier(dto: any): Observable<any> {
    return this.http.post<any>(this.base, dto);
  }

  updateDossier(id: string, dto: any): Observable<any> {
    return this.http.put<any>(`${this.base}/${id}`, dto);
  }

  deleteDossier(id: string): Observable<any> {
    return this.http.delete<any>(`${this.base}/${id}`);
  }

  completeInput(id: string): Observable<any> {
    return this.http.put<any>(`${this.base}/${id}/complete-input`, {});
  }

  // ===== GỬI DUYỆT =====

  submitForApproval(id: string, request: { nextNodeId: string; actionLabel: string; comment?: string; nextAssigneeUserId?: string }, kindId?: number): Observable<any> {
    return this.http.post<any>(`${this.workflowBaseFor(kindId)}/${id}/submit`, request);
  }

  getNextStepInfo(kindId?: number): Observable<any> {
    return this.http.get<any>(`${this.workflowBaseFor(kindId)}/next-step-info`);
  }

  // ===== FORM DATA =====

  saveFormData(id: string, dto: { formDataJson: string; changeNote?: string; rowVersion: number }): Observable<any> {
    return this.http.post<any>(`${this.base}/${id}/form-data`, dto);
  }

  // ===== PHIÊN BẢN =====

  getVersions(id: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/${id}/versions`);
  }

  // ===== THIẾT BỊ =====

  getEquipments(id: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/${id}/equipment`);
  }

  addEquipment(id: string, equipmentId: string): Observable<any> {
    return this.http.post<any>(`${this.base}/${id}/equipment`, { equipmentId });
  }

  removeEquipment(id: string, equipmentId: string): Observable<any> {
    return this.http.delete<any>(`${this.base}/${id}/equipment/${equipmentId}`);
  }

  getRelatedDossiers(
    dossierId: string,
    filter: { keyword?: string; equipmentId?: string; dossierTypeId?: string; page?: number; pageSize?: number },
    kindId?: number
  ): Observable<any> {
    let params = new HttpParams();
    if (filter.keyword) params = params.set('keyword', filter.keyword);
    if (filter.equipmentId) params = params.set('equipmentId', filter.equipmentId);
    if (filter.dossierTypeId) params = params.set('dossierTypeId', filter.dossierTypeId);
    if (filter.page) params = params.set('page', filter.page.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<any>(`${this.baseFor(kindId)}/${dossierId}/related`, { params });
  }

  // ===== WORKFLOW =====

  moveWorkflow(id: string, request: { nextNodeId: string; actionLabel: string; comment?: string; nextAssigneeUserId?: string }, kindId?: number): Observable<any> {
    return this.http.post<any>(`${this.workflowBaseFor(kindId)}/${id}/move`, request);
  }

  /** Gửi duyệt lại từ tab Trả lại — map DOSSIER_CREATE */
  resubmitWorkflow(id: string, request: { nextNodeId: string; actionLabel: string; comment?: string; nextAssigneeUserId?: string }, kindId?: number): Observable<any> {
    return this.http.post<any>(`${this.workflowBaseFor(kindId)}/${id}/resubmit`, request);
  }

  getWorkflowByEntity(id: string, kindId?: number): Observable<any> {
    return this.http.get<any>(`${this.workflowBaseFor(kindId)}/${id}/get-workflow-by-entity`).pipe(
      catchError(() => of(null))
    );
  }

  getWorkflowHistory(id: string, kindId?: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.workflowBaseFor(kindId)}/${id}/get-workflow-history`).pipe(
      catchError(() => of([]))
    );
  }

  getWorkflowDefinition(definitionId: string, kindId?: number): Observable<any> {
    return this.http.get<any>(`${this.workflowBaseFor(kindId)}/get-workflow-definition/${definitionId}`).pipe(
      catchError(() => of(null))
    );
  }

  getMyTasks(instanceId?: string | null, kindId?: number): Observable<any[]> {
    let params = new HttpParams();
    if (instanceId?.trim()) {
      params = params.set('instanceId', instanceId.trim());
    }
    return this.http.get<any[]>(`${this.workflowBaseFor(kindId)}/get-my-tasks`, { params }).pipe(
      catchError(() => of([]))
    );
  }

  /**
   * Tải gộp workflow instance + definition + history cho 1 hồ sơ
   */
  getWorkflowDetail(id: string, kindId?: number): Observable<any> {
    const effectiveKindId = kindId ?? this.kindId;
    return this.getWorkflowByEntity(id, effectiveKindId).pipe(
      switchMap(instance => {
        if (!instance) return of({ instance: null, definition: null, history: [] });

        const definitionId = instance.workflowDefinitionId ?? instance.WorkflowDefinitionId;
        const def$ = definitionId
          ? this.getWorkflowDefinition(definitionId, effectiveKindId).pipe(catchError(() => of(null)))
          : of(null);

        const history$ = this.getWorkflowHistory(id, effectiveKindId).pipe(catchError(() => of([])));

        return forkJoin({ definition: def$, history: history$ }).pipe(
          map(({ definition, history }) => ({ instance, definition, history }))
        );
      })
    );
  }

  // ===== DOSSIER SET =====

  getDossierSets(unitId?: number): Observable<any[]> {
    let params = new HttpParams();
    if (unitId != null) params = params.set('unitId', unitId.toString());
    return this.http.get<any[]>(this.dossierSetBase, { params });
  }

  createDossierSet(dto: any): Observable<any> {
    return this.http.post<any>(this.dossierSetBase, dto);
  }

  // ===== LOOKUP (bypass quyền) =====

  getInfrastructureLookup(keyword?: string): Observable<any[]> {
    let params = new HttpParams();
    if (keyword) params = params.set('keyword', keyword);
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/dossiers/infrastructures/lookup`, { params });
  }

  /** Cây kệ → tầng → hộp theo đơn vị (DOSSIER_VIEW / DOSSIER_DIGITIZATION_VIEW). */
  getPhysicalStorageTree(unitId?: number | null): Observable<any[]> {
    let params = new HttpParams();
    if (unitId != null && unitId > 0) {
      params = params.set('unitId', unitId.toString());
    }
    return this.http.get<any[]>(`${this.base}/physical-storage/tree`, { params });
  }

  getGridTypeLookup(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/dossiers/grid-types/lookup`);
  }

  getDossierTypeLookup(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/dossiers/dossier-type/lookup`);
  }

  getDossierGroupLookup(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/dossiers/dossier-groups/lookup`);
  }

  getOrganizationUnitsLookup(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/organization-units/lookup`);
  }

  getEquipmentLookup(params?: { infrastructureId?: string; gridTypeId?: number; keyword?: string; code?: string; name?: string; unitId?: number; isActive?: boolean; page?: number; pageSize?: number }): Observable<any> {
    let httpParams = new HttpParams();
    if (params?.infrastructureId) httpParams = httpParams.set('infrastructureId', params.infrastructureId);
    if (params?.gridTypeId != null) httpParams = httpParams.set('gridTypeId', params.gridTypeId.toString());
    if (params?.keyword?.trim()) httpParams = httpParams.set('keyword', params.keyword.trim());
    if (params?.code?.trim()) httpParams = httpParams.set('code', params.code.trim());
    if (params?.name?.trim()) httpParams = httpParams.set('name', params.name.trim());
    if (params?.unitId != null) httpParams = httpParams.set('unitId', params.unitId.toString());
    if (params?.isActive != null) httpParams = httpParams.set('isActive', params.isActive.toString());
    httpParams = httpParams.set('page', (params?.page ?? 1).toString());
    httpParams = httpParams.set('pageSize', (params?.pageSize ?? 10).toString());
    return this.http.get<any>(`${this.base}/equipment/lookup`, { params: httpParams });
  }

  getCatalogsByType(catalogTypeCode: string): Observable<any[]> {
    if (catalogTypeCode === 'BHS') {
      return this.getBhsCatalogColumns();
    }
    return this.http.get<any[]>(`${this.catalogBase}/lookup`).pipe(catchError(() => of([])));
  }

  getUsersLookup(role?: string | null): Observable<any[]> {
    let params = new HttpParams();
    if (role?.trim()) {
      params = params.set('role', role.trim());
    }
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/users/lookup`, { params }).pipe(
      catchError(() => of([]))
    );
  }

  /**
   * Lấy EAV form template theo ngữ cảnh hồ sơ (preview tài liệu / xem chi tiết).
   * Không gọi api/v1/eav-form-templates — dùng endpoint gắn với hồ sơ, quyền DOSSIER_* / DOSSIER_PUBLISH_*.
   */
  getDossierFormTemplate(
    dossierId: string,
    formId?: string | null,
    scope: 'default' | 'publish' | 'lookup' = 'default'
  ): Observable<any> {
    const url =
      scope === 'publish'
        ? `${this.config.apiGatewayUrl}/api/v1/dossier-publish/${dossierId}/form-template`
        : scope === 'lookup'
          ? `${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment/${dossierId}/form-template`
          : `${this.base}/${dossierId}/form-template`;

    let params = new HttpParams();
    if (formId) params = params.set('formId', formId);

    return this.http.get<any>(url, { params }).pipe(catchError(() => of(null)));
  }

  /**
   * Lấy EAV form template theo formId — chỉ dùng khi tạo hồ sơ mới (chưa có dossierId).
   * Gọi endpoint /get-form (bypass DynamicPermission).
   */
  getFormTemplate(formId: string): Observable<any> {
    return this.http.get<any>(
      `${this.config.apiGatewayUrl}/api/v1/eav-form-templates/${formId}/get-form`
    ).pipe(catchError(() => of(null)));
  }

  getDossiersByEquipment(equipmentId: string, page: number, pageSize: number): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<any>(`${this.base}/by-equipment/${equipmentId}`, { params });
  }

  downloadImportTemplate(): Observable<Blob> {
    return this.http.get(`${this.base}/import/template`, { responseType: 'blob' });
  }

  importDossiers(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<any>(`${this.base}/import`, formData);
  }
}
