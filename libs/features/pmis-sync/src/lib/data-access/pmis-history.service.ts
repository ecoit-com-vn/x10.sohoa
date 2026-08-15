import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { PmisSyncObjectType } from './pmis-manual-sync.service';

export interface SyncHistory {
  id: string;
  objectType: string;
  syncType: 'AUTO' | 'MANUAL';
  startTime: string;
  endTime: string | null;
  status: 'RUNNING' | 'SUCCESS' | 'FAILED';
  totalRecords: number;
  successRecords: number;
  failedRecords: number;
  errorMessage: string | null;
  createdBy: string | null;
}

export interface SyncHistoryDetail {
  id: string;
  sourceId: string | null;
  sourceCode: string | null;
  sourceName: string | null;
  targetId: string | null;
  actionType: 'CREATE' | 'UPDATE' | 'SKIP';
  status: 'SUCCESS' | 'FAILED';
  dataContent: string | null;
  errorMessage: string | null;
  syncTime: string;
}

@Injectable({ providedIn: 'root' })
export class PmisHistoryService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/sync/history`;

  getHistory(objectType: PmisSyncObjectType, page: number, pageSize: number): Observable<{ items: SyncHistory[]; totalCount: number }> {
    return this.http.get<{ items: SyncHistory[]; totalCount: number }>(this.apiUrl, {
      params: { objectType, page: String(page), pageSize: String(pageSize) },
    });
  }

  getHistoryItems(historyId: string, page: number, pageSize: number): Observable<{ items: SyncHistoryDetail[]; totalCount: number }> {
    return this.http.get<{ items: SyncHistoryDetail[]; totalCount: number }>(`${this.apiUrl}/${historyId}/items`, {
      params: { page: String(page), pageSize: String(pageSize) },
    });
  }
}
