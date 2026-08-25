import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export type PmisSyncObjectType = 'SUBSTATION' | 'TRANSMISSION_LINE' | 'EQUIPMENT';

export interface PmisManualSearchCriteria {
  maDonVi?: string | null;
  loaiTBA?: number | null;
  maLoaiDuongDay?: number | null;
  maTBA?: string | null;
  maDuongDay?: string | null;
  maLoaiTB?: string | null;
  namSanXuat?: number | null;
  tinhTrang?: number | null;
  kemQRCode?: boolean | null;
  tuNgay?: string | null;
  denNgay?: string | null;
  skip: number;
  take: number;
}

export interface PmisSyncPreviewItem {
  pmisCode: string;
  displayName: string;
  rawData: Record<string, unknown>;
}

export interface PmisManualSearchResponse {
  total: number;
  items: PmisSyncPreviewItem[];
}

export interface PmisManualSaveResponse {
  syncHistoryId: string;
  total: number;
  successCount: number;
  failedCount: number;
  errors: string[];
}

@Injectable({ providedIn: 'root' })
export class PmisManualSyncService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/sync/manual`;

  search(objectType: PmisSyncObjectType, criteria: PmisManualSearchCriteria): Observable<PmisManualSearchResponse> {
    return this.http.post<PmisManualSearchResponse>(`${this.apiUrl}/${objectType}/search`, criteria);
  }

  save(objectType: PmisSyncObjectType, items: Record<string, unknown>[]): Observable<PmisManualSaveResponse> {
    return this.http.post<PmisManualSaveResponse>(`${this.apiUrl}/${objectType}/save`, { items });
  }
}
