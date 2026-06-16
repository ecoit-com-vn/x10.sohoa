import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, switchMap, forkJoin, of, catchError, map } from 'rxjs';
import { APP_CONFIG, WorkflowDefinition } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class BorrowRecordService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseBorrow() {
    return `${this.config.apiGatewayUrl}/api/v1/borrow-records`;
  }

  private get baseWorkflow() {
    return `${this.config.apiGatewayUrl}/api/v1/workflows`;
  }

  private get baseDefinition() {
    return `${this.config.apiGatewayUrl}/api/workflowdefinitions`;
  }

  getBorrowRecords(page: number, pageSize: number, keyword?: string, state?: string): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (state !== undefined && state !== null && state !== '') {
      params = params.set('state', state);
    }
    return this.http.get<any>(this.baseBorrow, { params });
  }

  createBorrowRecord(record: any): Observable<any> {
    return this.http.post<any>(this.baseBorrow, record);
  }

  getBorrowRecordById(id: string): Observable<any> {
    return this.http.get<any>(`${this.baseBorrow}/${id}`);
  }

  getUsersLookup(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/users/lookup`);
  }

  getDossiers(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/equipment/dossiers`);
  }

  updateState(id: string, state: number): Observable<any> {
    return this.http.put<any>(`${this.baseBorrow}/${id}/state`, state);
  }

  getMyTasks(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseBorrow}/get-my-tasks`);
  }

  getWorkflowByEntity(recordId: string, entityType: string = 'BorrowRecord'): Observable<any> {
    const params = new HttpParams().set('entityType', entityType);
    return this.http.get<any>(`${this.baseBorrow}/get-workflow-by-entity/${recordId}`, { params });
  }

  getActiveWorkflowDefinitions(): Observable<WorkflowDefinition[]> {
    const params = new HttpParams()
      .set('isActive', 'true')
      .set('pageSize', '9999');
    return this.http.get<WorkflowDefinition[]>(this.baseDefinition, { params });
  }

  submitWorkflow(definitionId: string, dossierId: string, entityType: string = 'BorrowRecord'): Observable<any> {
    const params = new HttpParams()
      .set('definitionId', definitionId)
      .set('dossierId', dossierId)
      .set('entityType', entityType);
    return this.http.post<any>(`${this.baseWorkflow}/submit`, null, { params });
  }

  getWorkflowDefinition(id: string): Observable<WorkflowDefinition> {
    return this.http.get<WorkflowDefinition>(`${this.baseBorrow}/get-workflow-definition/${id}`);
  }

  moveWorkflow(dossierId: string, nextNodeId: string, actionLabel: string, comment?: string, nextAssigneeUserId?: string): Observable<any> {
    const body = { dossierId, nextNodeId, actionLabel, comment, nextAssigneeUserId };
    return this.http.post<any>(`${this.baseBorrow}/move`, body);
  }

  getWorkflowHistory(borrowRecordId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseBorrow}/get-workflow-history/${borrowRecordId}`);
  }

  getWorkflowDetail(recordId: string): Observable<any> {
    return this.getWorkflowByEntity(recordId).pipe(
      switchMap(instance => {
        if (!instance) {
          return of({ instance: null, definition: null, history: [] });
        }
        const def$ = instance.workflowDefinitionId
          ? this.getWorkflowDefinition(instance.workflowDefinitionId).pipe(catchError(() => of(null)))
          : of(null);
        const history$ = this.getWorkflowHistory(recordId).pipe(catchError(() => of([])));
        return forkJoin({
          definition: def$,
          history: history$
        }).pipe(
          map(({ definition, history }) => ({
            instance,
            definition,
            history
          }))
        );
      })
    );
  }
}
