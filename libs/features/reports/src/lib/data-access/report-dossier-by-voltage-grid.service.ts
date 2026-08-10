import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { ObjectTypeLookupItem, UnitLookupItem } from './report-dossier-by-year.service';

export interface GridTypeLookupItem {
  id: string;
  name: string;
  code?: string;
}

export interface DossierByVoltageGridFilter {
  unitId?: number | null;
  objectType?: number | null;
  gridTypeId?: number | null;
  year?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByVoltageGridChartStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

export interface DossierByVoltageGridRatioStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  percentage: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByVoltageGridService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/reports/statistics`;
  }

  getUnitsLookup(): Observable<UnitLookupItem[]> {
    const params = new HttpParams().set('isactive', 1);
    return this.http.get<UnitLookupItem[]>(`${this.baseUrl}/lookups/units`, { params });
  }

  getObjectTypesLookup(): Observable<ObjectTypeLookupItem[]> {
    return this.http.get<ObjectTypeLookupItem[]>(`${this.baseUrl}/lookups/object-types`);
  }

  getGridTypesLookup(): Observable<GridTypeLookupItem[]> {
    return this.http.get<GridTypeLookupItem[]>(`${this.baseUrl}/lookups/grid-types`);
  }

  getYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/years`);
  }

  getChartStats(filter: DossierByVoltageGridFilter): Observable<DossierByVoltageGridChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByVoltageGridChartStat[]>(`${this.baseUrl}/dossier-by-voltage-grid/chart-stats`, { params });
  }

  getRatioStats(filter: DossierByVoltageGridFilter): Observable<DossierByVoltageGridRatioStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByVoltageGridRatioStat[]>(`${this.baseUrl}/dossier-by-voltage-grid/ratio-stats`, { params });
  }

  exportExcel(filter: DossierByVoltageGridFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-voltage-grid/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByVoltageGridFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId != null && filter.unitId > 0) {
      params = params.set('unitId', filter.unitId.toString());
    }
    if (filter.objectType != null) {
      params = params.set('objectType', filter.objectType.toString());
    }
    if (filter.gridTypeId != null && filter.gridTypeId > 0) {
      params = params.set('gridTypeId', filter.gridTypeId.toString());
    }
    if (filter.year != null && filter.year > 0) {
      params = params.set('year', filter.year.toString());
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
