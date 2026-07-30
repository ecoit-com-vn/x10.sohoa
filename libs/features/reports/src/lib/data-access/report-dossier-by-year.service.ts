import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

export interface UnitLookupItem {
  id: string;
  name: string;
  code?: string;
  parentId?: number | null;
}

export interface ObjectTypeLookupItem {
  id: number;
  code: string;
  name: string;
}

export interface DossierByYearFilter {
  unitId?: number | null;
  objectType?: number | null;
  year?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByYearChartStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

export interface DossierByYearRatioStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  percentage: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByYearService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/reports/statistics`;
  }
  
  getUnitsLookup(isactive?: number): Observable<UnitLookupItem[]> {
    let params = new HttpParams();

    if (isactive !== undefined && isactive !== null) {
      params = params.set('isactive', isactive);
    }

    return this.http.get<UnitLookupItem[]>(
      `${this.baseUrl}/lookups/units`,
      { params }
    );
  }

  getObjectTypesLookup(): Observable<ObjectTypeLookupItem[]> {
    return this.http.get<ObjectTypeLookupItem[]>(`${this.baseUrl}/lookups/object-types`);
  }

  getYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/years`);
  }

  getChartStats(filter: DossierByYearFilter): Observable<DossierByYearChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByYearChartStat[]>(`${this.baseUrl}/dossier-by-year/chart-stats`, { params });
  }

  getRatioStats(filter: DossierByYearFilter): Observable<DossierByYearRatioStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByYearRatioStat[]>(`${this.baseUrl}/dossier-by-year/ratio-stats`, { params });
  }

  exportExcel(filter: DossierByYearFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-year/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByYearFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId != null && filter.unitId > 0) {
      params = params.set('unitId', filter.unitId.toString());
    }
    if (filter.objectType != null) {
      params = params.set('objectType', filter.objectType.toString());
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
