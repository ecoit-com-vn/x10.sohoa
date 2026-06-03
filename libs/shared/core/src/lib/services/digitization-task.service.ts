import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config.token';

export interface DigitizationTask {
  id: string; // Guid
  dossierId: string;
  workflowStepId: string; // Guid
  assignedToUserId: string;
  status: string;
  createdAt: string;
  completedAt?: string;
  notes: string;
}

@Injectable({
  providedIn: 'root'
})
export class DigitizationTaskService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private get apiUrl() {
    return `${this.config.apiGatewayUrl}/api/v1/digitization-task`;
  }

  getTasks(): Observable<DigitizationTask[]> {
    return this.http.get<DigitizationTask[]>(this.apiUrl);
  }

  assignTask(dossierId: string, assignedToUserId: string, notes: string = ''): Observable<any> {
    // Standard OCR Verification Step constant Guid
    const defaultWorkflowStepId = 'c8b671a2-2b36-4cbf-8bc9-93e18a93cb34';
    
    return this.http.post<any>(`${this.apiUrl}/assign`, {
      dossierId,
      workflowStepId: defaultWorkflowStepId,
      assignedToUserId,
      notes
    });
  }

  updateTaskStatus(id: string, status: string, notes?: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/status`, {
      status,
      notes
    });
  }
}
