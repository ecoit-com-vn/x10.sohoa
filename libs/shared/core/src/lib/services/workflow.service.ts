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
}

export interface WorkflowDefinition {
  id?: string;
  name: string;            // Loại quy trình
  description: string;     // Mô tả
  version: string;         // Phiên bản
  forceActivate: boolean;  // Ép buộc kích hoạt
  isActive: boolean;       // Trạng thái
  createdAt?: string;
  updatedAt?: string;
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
    if (keyword)              params = params.set('keyword', keyword);
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

  private get EXEC_BASE() {
    return `${this.config.apiGatewayUrl}/api/v1/workflows`;
  }

  /** Gửi hồ sơ/yêu cầu vào quy trình */
  submitWorkflow(definitionId: string, dossierId: string, entityType: string = 'BorrowRecord'): Observable<any> {
    let params = new HttpParams()
      .set('definitionId', definitionId)
      .set('dossierId', dossierId)
      .set('entityType', entityType);
    return this.http.post<any>(`${this.EXEC_BASE}/submit`, null, { params })
      .pipe(catchError(this.handleError));
  }

  /** Lấy danh sách nhiệm vụ cần làm của tôi */
  getMyTasks(): Observable<any[]> {
    return this.http.get<any[]>(`${this.EXEC_BASE}/tasks/my-tasks`)
      .pipe(catchError(this.handleError));
  }

  /** Phê duyệt nhiệm vụ */
  approveTask(taskId: string, comment?: string): Observable<any> {
    const headers = { 'Content-Type': 'application/json' };
    const body = comment !== undefined && comment !== null ? JSON.stringify(comment) : '""';
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
  moveWorkflow(dossierId: string, nextNodeId: string, actionLabel: string, comment?: string): Observable<any> {
    const body = { dossierId, nextNodeId, actionLabel, comment };
    return this.http.post<any>(`${this.EXEC_BASE}/move`, body)
      .pipe(catchError(this.handleError));
  }

  /** Lấy lịch sử phê duyệt của Instance */
  getWorkflowHistory(instanceId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.EXEC_BASE}/instances/${instanceId}/history`)
      .pipe(catchError(this.handleError));
  }

  /** Lấy instance hiện tại của target entity */
  getInstanceByEntity(entityId: string, entityType: string = 'BorrowRecord'): Observable<any> {
    let params = new HttpParams().set('entityType', entityType);
    return this.http.get<any>(`${this.EXEC_BASE}/instances/entity/${entityId}`, { params })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    const msg = error?.error?.message || error?.message || 'Lỗi không xác định từ máy chủ.';
    return throwError(() => new Error(msg));
  }
}
