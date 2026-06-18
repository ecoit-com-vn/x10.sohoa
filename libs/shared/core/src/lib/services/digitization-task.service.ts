import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

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
  private api = inject(ApiService);
  
  private get apiUrl() {
    return `/api/v1/digitization-task`;
  }

  getTasks(page: number, pageSize: number, keyword?: string): Observable<any> {
    return this.api.get<any>(`${this.apiUrl}?page=${page}&pageSize=${pageSize}&keyword=${keyword || ''}`);
  }

  assignTask(dossierId: string, assignedToUserId: string, notes: string = ''): Observable<any> {
    // Standard OCR Verification Step constant Guid
    const defaultWorkflowStepId = 'c8b671a2-2b36-4cbf-8bc9-93e18a93cb34';
    
    return this.api.post<any>(`${this.apiUrl}/assign`, {
      dossierId,
      workflowStepId: defaultWorkflowStepId,
      assignedToUserId,
      notes
    });
  }

  updateTaskStatus(id: string, status: string, notes?: string): Observable<void> {
    return this.api.put<void>(`${this.apiUrl}/${id}/status`, {
      status,
      notes
    });
  }
}
