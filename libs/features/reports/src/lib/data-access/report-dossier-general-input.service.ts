import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { ObjectTypeLookupItem, UnitLookupItem } from './report-dossier-by-year.service';

export interface DossierGeneralInputFilter {
  unitId?: number | null;
  objectType?: number | null;
  fromDate?: string | null;
  toDate?: string | null;
  page?: number;
  pageSize?: number;
}

export interface DossierGeneralInputChartStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

export interface DossierGeneralInputRatioStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  percentage: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierGeneralInputService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/reports/statistics`;
  }

  getUnitsLookup(): Observable<UnitLookupItem[]> {
    return this.http.get<UnitLookupItem[]>(`${this.baseUrl}/lookups/units`);
  }

  getObjectTypesLookup(): Observable<ObjectTypeLookupItem[]> {
    return this.http.get<ObjectTypeLookupItem[]>(`${this.baseUrl}/lookups/object-types`);
  }

  getChartStats(filter: DossierGeneralInputFilter): Observable<DossierGeneralInputChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierGeneralInputChartStat[]>(`${this.baseUrl}/dossier-general-input/chart-stats`, { params });
  }

  getRatioStats(filter: DossierGeneralInputFilter): Observable<DossierGeneralInputRatioStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierGeneralInputRatioStat[]>(`${this.baseUrl}/dossier-general-input/ratio-stats`, { params });
  }

  exportExcel(filter: DossierGeneralInputFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-general-input/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierGeneralInputFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId != null && filter.unitId > 0) {
      params = params.set('unitId', filter.unitId.toString());
    }
    if (filter.objectType != null) {
      params = params.set('objectType', filter.objectType.toString());
    }
    if (filter.fromDate) {
      params = params.set('fromDate', filter.fromDate);
    }
    if (filter.toDate) {
      params = params.set('toDate', filter.toDate);
    }
    if (filter.page != null) {
      params = params.set('page', filter.page.toString());
    }
    if (filter.pageSize != null) {
      params = params.set('pageSize', filter.pageSize.toString());
    }
    return params;
  }
}
