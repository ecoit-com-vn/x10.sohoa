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
  status: 'RUNNING' | 'SUCCESS' | 'FAILED' | 'WARNING';
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
  status: 'SUCCESS' | 'FAILED' | 'WARNING';
  dataContent: string | null;
  errorMessage: string | null;
  syncTime: string;
}

export type SyncHistoryCleanupMode = 'DATE_RANGE' | 'KEEP_LAST_1_DAY' | 'KEEP_LAST_7_DAYS' | 'ALL';

export interface GroupedSyncHistory extends SyncHistory {
  /** > 1 nghĩa là dòng này đại diện cho N lần "Thất bại" liên tiếp giống nhau (cùng đối tượng, 0 bản ghi). */
  groupedCount?: number;
  /** startTime của lần cũ nhất trong nhóm — items đến theo thứ tự mới nhất trước nên đây là mốc nhỏ hơn `startTime`. */
  groupedFrom?: string;
}

/**
 * Gộp các dòng "Thất bại" liên tiếp (0 bản ghi, cùng đối tượng) thành 1 dòng đại diện — tránh rối mắt
 * khi PMIS sập kéo dài sinh ra hàng chục dòng thất bại giống hệt nhau trong Lịch sử đồng bộ. Chỉ gộp
 * hiển thị trên dữ liệu đã tải về (trang hiện tại) — không đổi gì ở API/backend.
 */
export function groupConsecutiveFailures(items: SyncHistory[]): GroupedSyncHistory[] {
  const result: GroupedSyncHistory[] = [];
  for (const item of items) {
    const prev = result[result.length - 1];
    const isNoopFailure = item.status === 'FAILED' && item.totalRecords === 0;
    const prevIsSameGroup =
      !!prev && prev.status === 'FAILED' && prev.totalRecords === 0 && prev.objectType === item.objectType;

    if (isNoopFailure && prevIsSameGroup) {
      prev.groupedCount = (prev.groupedCount ?? 1) + 1;
      prev.groupedFrom = item.startTime;
      continue;
    }
    result.push({ ...item });
  }
  return result;
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

  cleanup(
    objectType: PmisSyncObjectType,
    mode: SyncHistoryCleanupMode,
    fromDate?: string | null,
    toDate?: string | null
  ): Observable<{ deletedCount: number }> {
    return this.http.post<{ deletedCount: number }>(`${this.apiUrl}/cleanup`, {
      objectType,
      mode,
      fromDate: fromDate || null,
      toDate: toDate || null,
    });
  }
}
