import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export type PmisFrequencyUnit = 'MINUTE' | 'HOUR' | 'DAY';

export interface SyncConfig {
  id: string;
  objectType: string;
  frequencyValue: number;
  frequencyUnit: PmisFrequencyUnit;
  isEnabled: boolean;
  lastSyncAt: string | null;
  nextSyncAt: string | null;
  rowVersion: number;
}

export interface UpdateSyncConfigRequest {
  isEnabled: boolean;
  frequencyValue: number;
  frequencyUnit: PmisFrequencyUnit;
  rowVersion: number;
}

@Injectable({ providedIn: 'root' })
export class PmisScheduleService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/sync/config`;

  getAll(): Observable<SyncConfig[]> {
    return this.http.get<SyncConfig[]>(this.apiUrl);
  }

  update(objectType: string, request: UpdateSyncConfigRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${objectType}`, request);
  }
}
