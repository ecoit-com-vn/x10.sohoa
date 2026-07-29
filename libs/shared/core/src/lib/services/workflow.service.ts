import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { APP_CONFIG } from '../config/app-config.token';

// ─── Models khớp với backend WorkflowDefinition ──────────────────────────────

export interface WorkflowStep {
  id?: string;
  workflowDefinitionId?: string;
  stepName: string;
  order: number;
  requiredRole: string;
  actionType: string; // 'Scan' | 'DataEntry' | 'Approve' | 'Review'
  /** Danh sách ID nhóm quyền hệ thống (CSV) */
  systemPermissionGroupIds?: string;
  /** Danh sách ID nhóm quyền đơn vị (CSV) */
  unitPermissionGroupIds?: string;
  /** Bắt buộc cùng đơn vị với người chuyển bước */
  requireSameUnit?: boolean;
  /** ID người dùng được giao việc đích danh */
  assigneeId?: string;
}

export interface WorkflowDefinition {
  id?: string;
  name: string;
  workflowTypeId?: number;
  description: string;
  version: string;         // Phiên bản
  forceActivate: boolean;  // Ép buộc kích hoạt
  isActive: boolean;       // Trạng thái
  createdAt?: string;
  updatedAt?: string;
  createdBy?: string;
  createdByUsername?: string;
  createdByFullName?: string;
  updatedBy?: string;
  updatedByUsername?: string;
  updatedByFullName?: string;
  bpmnXml?: string;
  steps: WorkflowStep[];
}

export interface ToggleStatusResponse {
  id: string;
  isActive: boolean;
}

// ─── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class WorkflowService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private get BASE() {
    return `${this.config.apiGatewayUrl}/api/workflowdefinitions`;
  }

  /** Lấy danh sách quy trình, có thể lọc theo từ khóa và trạng thái */
  getAll(page: number, pageSize: number, keyword?: string, isActive?: boolean): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (keyword) params = params.set('keyword', keyword);
    if (isActive !== undefined) params = params.set('isActive', String(isActive));
    return this.http.get<any>(this.BASE, { params })
      .pipe(catchError(this.handleError));
  }

  /** Lấy chi tiết một quy trình theo ID */
  getById(id: string): Observable<WorkflowDefinition> {
    return this.http.get<WorkflowDefinition>(`${this.BASE}/${id}`)
      .pipe(catchError(this.handleError));
  }

  /** Tạo mới quy trình */
  create(dto: WorkflowDefinition): Observable<WorkflowDefinition> {
    return this.http.post<WorkflowDefinition>(this.BASE, dto)
      .pipe(catchError(this.handleError));
  }

  /** Cập nhật quy trình */
  update(id: string, dto: WorkflowDefinition): Observable<WorkflowDefinition> {
    return this.http.put<WorkflowDefinition>(`${this.BASE}/${id}`, dto)
      .pipe(catchError(this.handleError));
  }

  /** Xóa quy trình */
  delete(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.BASE}/${id}`)
      .pipe(catchError(this.handleError));
  }

  /** Bật/tắt trạng thái nhanh */
  toggleStatus(id: string): Observable<ToggleStatusResponse> {
    return this.http.patch<ToggleStatusResponse>(`${this.BASE}/${id}/toggle-status`, {})
      .pipe(catchError(this.handleError));
  }

  /** Tái kích hoạt một phiên bản quy trình cũ */
  reactivate(id: string): Observable<any> {
    return this.http.post<any>(`${this.BASE}/${id}/reactivate`, {})
      .pipe(catchError(this.handleError));
  }

  /** Lấy lịch sử tất cả phiên bản của một quy trình theo tên */
  getVersions(workflowTypeId: number): Observable<WorkflowDefinition[]> {
    return this.http.get<WorkflowDefinition[]>(`${this.BASE}/versions/${workflowTypeId}`)
      .pipe(catchError(this.handleError));
  }

  private get EXEC_BASE() {
    return `${this.config.apiGatewayUrl}/api/v1/workflows`;
  }

  /** Gửi hồ sơ/yêu cầu vào quy trình theo loại quy trình (WorkflowTypeId) */
  submitWorkflow(entityId: string, workflowTypeId: number = 2): Observable<any> {
    const body = { entityId, workflowTypeId };
    return this.http.post<any>(`${this.EXEC_BASE}/submit`, body)
      .pipe(catchError(this.handleError));
  }

  /** Lấy danh sách nhiệm vụ cần làm của tôi */
  getMyTasks(): Observable<any[]> {
    return this.http.get<any[]>(`${this.EXEC_BASE}/tasks/my-tasks`)
      .pipe(catchError(this.handleError));
  }

  /** Phê duyệt nhiệm vụ */
  approveTask(taskId: string, comment?: string, nextAssigneeUserId?: string): Observable<any> {
    const headers = { 'Content-Type': 'application/json' };
    const body = { comment, nextAssigneeUserId };
    return this.http.post<any>(`${this.EXEC_BASE}/tasks/${taskId}/approve`, body, { headers })
      .pipe(catchError(this.handleError));
  }

  /** Từ chối / Trả lại nhiệm vụ */
  rejectTask(taskId: string, comment?: string): Observable<any> {
    const headers = { 'Content-Type': 'application/json' };
    const body = comment !== undefined && comment !== null ? JSON.stringify(comment) : '""';
    return this.http.post<any>(`${this.EXEC_BASE}/tasks/${taskId}/reject`, body, { headers })
      .pipe(catchError(this.handleError));
  }

  /** Chuyển bước quy trình động dựa trên sơ đồ BPMN */
  moveWorkflow(dossierId: string, nextNodeId: string, actionLabel: string, comment?: string, nextAssigneeUserId?: string): Observable<any> {
    const body = { dossierId, nextNodeId, actionLabel, comment, nextAssigneeUserId };
    return this.http.post<any>(`${this.EXEC_BASE}/move`, body)
      .pipe(catchError(this.handleError));
  }

  /** Lấy lịch sử phê duyệt của Instance */
  getWorkflowHistory(instanceId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.EXEC_BASE}/instances/${instanceId}/history`)
      .pipe(catchError(this.handleError));
  }

  /** Lấy instance hiện tại của target entity */
  getInstanceByEntity(entityId: string, workflowTypeId: number = 2): Observable<any> {
    const params = new HttpParams().set('workflowTypeId', workflowTypeId.toString());
    return this.http.get<any>(`${this.EXEC_BASE}/get-workflow-by-entity/${entityId}`, { params })
      .pipe(catchError(this.handleError));
  }

  /** Lấy danh sách người dùng đủ điều kiện nhận bàn giao/chuyển xử lý */
  getEligibleAssignees(systemGroupIds?: string, unitGroupIds?: string, unitId?: number | string, keyword?: string): Observable<any[]> {
    let params = new HttpParams();
    if (systemGroupIds) params = params.set('systemGroupIds', systemGroupIds);
    if (unitGroupIds)   params = params.set('unitGroupIds', unitGroupIds);
    if (unitId)          params = params.set('unitId', unitId.toString());
    if (keyword)         params = params.set('keyword', keyword);
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/users/eligible-assignees`, { params })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    const msg = error?.error?.message || error?.message || 'Lỗi không xác định từ máy chủ.';
    return throwError(() => new Error(msg));
  }
}
