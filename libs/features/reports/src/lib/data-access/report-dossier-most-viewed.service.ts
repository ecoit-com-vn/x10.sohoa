import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { ObjectTypeLookupItem, UnitLookupItem } from './report-dossier-by-year.service';

export interface DossierMostViewedFilter {
  unitId?: number | null;
  objectType?: number | null;
  fromDate?: string | null;
  toDate?: string | null;
  page?: number;
  pageSize?: number;
}

export interface DossierMostViewedSummaryStats {
  stationViewCount: number;
  stationGrowthPercent: number | null;
  lineViewCount: number;
  lineGrowthPercent: number | null;
  documentViewCount: number;
  documentGrowthPercent: number | null;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierMostViewedService {
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

  getSummaryStats(filter: DossierMostViewedFilter): Observable<DossierMostViewedSummaryStats> {
    const params = this.buildParams(filter);
    return this.http.get<DossierMostViewedSummaryStats>(`${this.baseUrl}/dossier-most-viewed/summary-stats`, { params });
  }

  exportExcel(filter: DossierMostViewedFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-most-viewed/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierMostViewedFilter): HttpParams {
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
