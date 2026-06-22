import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, switchMap, forkJoin, of, catchError, map } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

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

  private get base() {
    return `${this.config.apiGatewayUrl}/api/v1/dossiers`;
  }

  private get searchBase() {
    return `${this.config.apiGatewayUrl}/api/v1/search/dossiers`;
  }

  private get catalogBase() {
    return `${this.config.apiGatewayUrl}/api/catalog`;
  }

  private get dossierSetBase() {
    return `${this.config.apiGatewayUrl}/api/v1/dossier-sets`;
  }

  // ===== DANH SÁCH (ES qua NotificationService) =====

  getDossiers(filter: {
    keyword?: string;
    infrastructureId?: string;
    gridTypeId?: number;
    unitId?: number;
    status?: string;
    page: number;
    pageSize: number;
  }): Observable<any> {
    let params = new HttpParams()
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.keyword?.trim()) params = params.set('keyword', filter.keyword.trim());
    if (filter.infrastructureId) params = params.set('infrastructureId', filter.infrastructureId);
    if (filter.gridTypeId != null) params = params.set('gridTypeId', filter.gridTypeId.toString());
    if (filter.unitId != null) params = params.set('unitId', filter.unitId.toString());
    if (filter.status) params = params.set('status', filter.status);

    return this.http.get<any>(this.searchBase, { params });
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

  // ===== GỬI DUYỆT =====

  submitForApproval(id: string): Observable<any> {
    return this.http.post<any>(`${this.base}/${id}/submit`, {});
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

  // ===== WORKFLOW =====

  moveWorkflow(id: string, request: { nextNodeId: string; actionLabel: string; comment?: string; nextAssigneeUserId?: string }): Observable<any> {
    return this.http.post<any>(`${this.base}/${id}/move`, request);
  }

  getWorkflowByEntity(id: string): Observable<any> {
    return this.http.get<any>(`${this.base}/${id}/get-workflow-by-entity`).pipe(
      catchError(() => of(null))
    );
  }

  getWorkflowHistory(id: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/${id}/get-workflow-history`).pipe(
      catchError(() => of([]))
    );
  }

  getWorkflowDefinition(definitionId: string): Observable<any> {
    return this.http.get<any>(`${this.base}/get-workflow-definition/${definitionId}`).pipe(
      catchError(() => of(null))
    );
  }

  getMyTasks(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/get-my-tasks`).pipe(
      catchError(() => of([]))
    );
  }

  /**
   * Tải gộp workflow instance + definition + history cho 1 hồ sơ
   */
  getWorkflowDetail(id: string): Observable<any> {
    return this.getWorkflowByEntity(id).pipe(
      switchMap(instance => {
        if (!instance) return of({ instance: null, definition: null, history: [] });

        const definitionId = instance.workflowDefinitionId ?? instance.WorkflowDefinitionId;
        const def$ = definitionId
          ? this.getWorkflowDefinition(definitionId).pipe(catchError(() => of(null)))
          : of(null);

        const history$ = this.getWorkflowHistory(id).pipe(catchError(() => of([])));

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

  getGridTypeLookup(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/dossiers/grid-types/lookup`);
  }

  getDossierTypeLookup(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/dossiers/dossier-type/lookup`);
  }

  getEquipmentLookup(params?: { infrastructureId?: string; gridTypeId?: number; keyword?: string; page?: number; pageSize?: number }): Observable<any> {
    let httpParams = new HttpParams();
    if (params?.infrastructureId) httpParams = httpParams.set('infrastructureId', params.infrastructureId);
    if (params?.gridTypeId != null) httpParams = httpParams.set('gridTypeId', params.gridTypeId.toString());
    // EquipmentController lọc theo `name` (và `code`), không có tham số `keyword`
    if (params?.keyword?.trim()) httpParams = httpParams.set('name', params.keyword.trim());
    httpParams = httpParams.set('page', (params?.page ?? 1).toString());
    httpParams = httpParams.set('pageSize', (params?.pageSize ?? 10).toString());
    return this.http.get<any>(`${this.config.apiGatewayUrl}/api/v1/equipment`, { params: httpParams });
  }

  getCatalogsByType(catalogTypeCode: string): Observable<any[]> {
    if (catalogTypeCode === 'BHS') {
      return this.getBhsCatalogColumns();
    }
    return this.http.get<any[]>(`${this.catalogBase}/lookup`).pipe(catchError(() => of([])));
  }

  getUsersLookup(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/users/lookup`).pipe(
      catchError(() => of([]))
    );
  }

  /**
   * Lấy EAV form template theo formId để gen trường nhập liệu động.
   * Gọi endpoint /get-form (bypass DynamicPermission).
   */
  getFormTemplate(formId: string): Observable<any> {
    return this.http.get<any>(
      `${this.config.apiGatewayUrl}/api/v1/eav-form-templates/${formId}/get-form`
    ).pipe(catchError(() => of(null)));
  }
}
