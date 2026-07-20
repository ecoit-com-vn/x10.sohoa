import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { BhsCatalogColumn } from '@sohoa.frontend/features/dossier-management';

export interface ReportStatisticsDossierListItem {
  stt: number;
  dossierId: string;
  catalogData?: Record<string, string>;
  infrastructureName: string;
  dossierTypeName: string;
  documentCount: number;
}

export interface ReportStatisticsDossierListResponse {
  items: ReportStatisticsDossierListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportStatisticsStationGridItem {
  stt: number;
  catalogData?: Record<string, string>;
  gridTypeName: string;
  totalDossiers: number;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsStationGridResponse {
  items: ReportStatisticsStationGridItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportStatisticsEquipmentTypeGridItem {
  stt: number;
  equipmentTypeCode: string;
  equipmentTypeName: string;
  gridTypeName: string;
  totalDossiers: number;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsEquipmentTypeGridResponse {
  items: ReportStatisticsEquipmentTypeGridItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class ReportStatisticsService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/reports/statistics`;
  }

  getBhsColumns(): Observable<BhsCatalogColumn[]> {
    return this.http.get<BhsCatalogColumn[]>(`${this.baseUrl}/lookups/bhs-columns`);
  }

  getDossierList(
    listSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsDossierListResponse> {
    return this.http.get<ReportStatisticsDossierListResponse>(`${this.baseUrl}/${listSegment}/list`, {
      params: this.buildParams(filter)
    });
  }

  getStationGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsStationGridResponse> {
    return this.http.get<ReportStatisticsStationGridResponse>(`${this.baseUrl}/${gridSegment}/station-grid`, {
      params: this.buildParams(filter)
    });
  }

  getEquipmentTypeGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsEquipmentTypeGridResponse> {
    return this.http.get<ReportStatisticsEquipmentTypeGridResponse>(`${this.baseUrl}/${gridSegment}/equipment-type-grid`, {
      params: this.buildParams(filter)
    });
  }

  private buildParams(filter: Record<string, string | number | string[] | null | undefined>): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value == null || value === '') continue;
      if (Array.isArray(value)) {
        const items = value
          .map((item) => String(item).trim())
          .filter((item) => item !== '');
        if (items.length === 0) continue;
        if (key === 'equipmentTypeIds') {
          params = params.set(key, items.join(','));
        } else {
          for (const item of items) {
            params = params.append(key, item);
          }
        }
        continue;
      }
      params = params.set(key, String(value));
    }
    return params;
  }
}
