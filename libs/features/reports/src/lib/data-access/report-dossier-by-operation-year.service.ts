import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { UnitLookupItem, ObjectTypeLookupItem } from './report-dossier-by-year.service';

export interface DossierByOperationYearFilter {
  unitId?: number | null;
  objectType?: number | null;
  year?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByOperationYearSummaryStats {
  year: number;
  previousYear: number;
  showGrowth: boolean;
  dossierCount: number;
  dossierGrowthPercent: number | null;
  documentCount: number;
  documentGrowthPercent: number | null;
  pageCount: number;
  pageGrowthPercent: number | null;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByOperationYearService {
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

  getOperationYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/operation-years`);
  }

  getSummaryStats(filter: DossierByOperationYearFilter): Observable<DossierByOperationYearSummaryStats> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByOperationYearSummaryStats>(`${this.baseUrl}/dossier-by-operation-year/summary-stats`, { params });
  }

  exportExcel(filter: DossierByOperationYearFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-operation-year/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByOperationYearFilter): HttpParams {
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
