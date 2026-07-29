import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { UnitLookupItem } from './report-dossier-by-year.service';

export interface LineLookupItem {
  id: string;
  name: string;
  code?: string;
}

export interface DossierByLineFilter {
  unitId?: number | null;
  lineIds?: string[] | null;
  year?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByLineSummaryStats {
  year: number;
  referenceMonth: number;
  previousMonth: number;
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
export class ReportDossierByLineService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/reports/statistics`;
  }

  getUnitsLookup(): Observable<UnitLookupItem[]> {
    return this.http.get<UnitLookupItem[]>(`${this.baseUrl}/lookups/units`);
  }

  getLinesLookup(unitId?: number | null): Observable<LineLookupItem[]> {
    let params = new HttpParams();
    if (unitId != null && unitId > 0) {
      params = params.set('unitId', unitId.toString());
    }
    return this.http.get<LineLookupItem[]>(`${this.baseUrl}/lookups/lines`, { params });
  }

  getYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/years`);
  }

  getSummaryStats(filter: DossierByLineFilter): Observable<DossierByLineSummaryStats> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByLineSummaryStats>(`${this.baseUrl}/dossier-by-line/summary-stats`, { params });
  }

  exportExcel(filter: DossierByLineFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-line/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByLineFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId != null && filter.unitId > 0) {
      params = params.set('unitId', filter.unitId.toString());
    }
    if (filter.lineIds?.length) {
      const ids = filter.lineIds
        .map((id) => id?.trim())
        .filter((id): id is string => !!id);
      if (ids.length) {
        params = params.set('lineIds', ids.join(','));
      }
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
