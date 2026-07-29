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

export interface ReportStatisticsDocumentListItem {
  stt: number;
  documentId: string;
  dossierId: string;
  documentTypeName: string;
  dossierTypeName: string;
  infrastructureName: string;
  equipmentName: string;
  documentName: string;
}

export interface ReportStatisticsDocumentListResponse {
  items: ReportStatisticsDocumentListItem[];
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

export interface ReportStatisticsDossierViewGridItem {
  stt: number;
  dossierId: string;
  catalogData?: Record<string, string>;
  infrastructureName: string;
  viewCount: number;
}

export interface ReportStatisticsDossierViewGridResponse {
  items: ReportStatisticsDossierViewGridItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportStatisticsEquipmentGridItem {
  stt: number;
  equipmentCode: string;
  equipmentName: string;
  infrastructureName: string;
  manufactureYear: number | null;
  totalDossiers: number;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsEquipmentGridResponse {
  items: ReportStatisticsEquipmentGridItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportStatisticsEquipmentStatusGridItem {
  stt: number;
  equipmentCode: string;
  equipmentName: string;
  infrastructureName: string;
  equipmentStatusName: string;
  totalDossiers: number;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsEquipmentStatusGridResponse {
  items: ReportStatisticsEquipmentStatusGridItem[];
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

export interface ReportStatisticsDossierTypeGridItem {
  stt: number;
  dossierTypeCode: string;
  dossierTypeName: string;
  totalDossiers: number;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsDossierTypeGridResponse {
  items: ReportStatisticsDossierTypeGridItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportStatisticsDocumentTypeGridItem {
  stt: number;
  documentTypeCode: string;
  documentTypeName: string;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsDocumentTypeGridResponse {
  items: ReportStatisticsDocumentTypeGridItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportStatisticsShelfGridItem {
  stt: number;
  shelfCode: string;
  shelfName: string;
  totalDossiers: number;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsShelfGridResponse {
  items: ReportStatisticsShelfGridItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportStatisticsBoxGridItem {
  stt: number;
  boxCode: string;
  boxName: string;
  totalDossiers: number;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsBoxGridResponse {
  items: ReportStatisticsBoxGridItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportStatisticsFloorGridItem {
  stt: number;
  floorCode: string;
  floorName: string;
  totalDossiers: number;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsFloorGridResponse {
  items: ReportStatisticsFloorGridItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportStatisticsCreatorGridItem {
  stt: number;
  username: string;
  fullName: string;
  unitName: string;
  totalDossiers: number;
  totalDocuments: number;
  totalPages: number;
}

export interface ReportStatisticsCreatorGridResponse {
  items: ReportStatisticsCreatorGridItem[];
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

  getDocumentList(
    listSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsDocumentListResponse> {
    return this.http.get<ReportStatisticsDocumentListResponse>(`${this.baseUrl}/${listSegment}/list`, {
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

  getDossierViewGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsDossierViewGridResponse> {
    return this.http.get<ReportStatisticsDossierViewGridResponse>(`${this.baseUrl}/${gridSegment}/grid`, {
      params: this.buildParams(filter)
    });
  }

  getEquipmentGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsEquipmentGridResponse> {
    return this.http.get<ReportStatisticsEquipmentGridResponse>(`${this.baseUrl}/${gridSegment}/equipment-grid`, {
      params: this.buildParams(filter)
    });
  }

  getEquipmentStatusGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsEquipmentStatusGridResponse> {
    return this.http.get<ReportStatisticsEquipmentStatusGridResponse>(`${this.baseUrl}/${gridSegment}/equipment-grid`, {
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

  getDossierTypeGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsDossierTypeGridResponse> {
    return this.http.get<ReportStatisticsDossierTypeGridResponse>(`${this.baseUrl}/${gridSegment}/dossier-type-grid`, {
      params: this.buildParams(filter)
    });
  }

  getDocumentTypeGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsDocumentTypeGridResponse> {
    return this.http.get<ReportStatisticsDocumentTypeGridResponse>(`${this.baseUrl}/${gridSegment}/document-type-grid`, {
      params: this.buildParams(filter)
    });
  }

  getShelfGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsShelfGridResponse> {
    return this.http.get<ReportStatisticsShelfGridResponse>(`${this.baseUrl}/${gridSegment}/shelf-grid`, {
      params: this.buildParams(filter)
    });
  }

  getBoxGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsBoxGridResponse> {
    return this.http.get<ReportStatisticsBoxGridResponse>(`${this.baseUrl}/${gridSegment}/box-grid`, {
      params: this.buildParams(filter)
    });
  }

  getFloorGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsFloorGridResponse> {
    return this.http.get<ReportStatisticsFloorGridResponse>(`${this.baseUrl}/${gridSegment}/floor-grid`, {
      params: this.buildParams(filter)
    });
  }

  getCreatorGrid(
    gridSegment: string,
    filter: Record<string, string | number | string[] | null | undefined>
  ): Observable<ReportStatisticsCreatorGridResponse> {
    return this.http.get<ReportStatisticsCreatorGridResponse>(`${this.baseUrl}/${gridSegment}/creator-grid`, {
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
        if (key === 'equipmentTypeIds' || key === 'dossierTypeIds' || key === 'documentTypeIds' || key === 'shelfIds' || key === 'floorIds' || key === 'boxIds' || key === 'stationIds' || key === 'lineIds') {
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
