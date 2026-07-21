import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { UnitLookupItem } from './report-dossier-by-year.service';

export interface BoxLookupItem {
  id: string;
  name: string;
  code?: string;
}

export interface DossierByBoxFilter {
  unitId?: number | null;
  boxIds?: string[] | null;
  year?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByBoxChartStat {
  boxCode: string;
  boxName: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByBoxService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/reports/statistics`;
  }

  getUnitsLookup(): Observable<UnitLookupItem[]> {
    return this.http.get<UnitLookupItem[]>(`${this.baseUrl}/lookups/units`);
  }

  getBoxesLookup(unitId?: number | null): Observable<BoxLookupItem[]> {
    let params = new HttpParams();
    if (unitId != null && unitId > 0) {
      params = params.set('unitId', unitId.toString());
    }
    return this.http.get<BoxLookupItem[]>(`${this.baseUrl}/lookups/boxes`, { params });
  }

  getYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/years`);
  }

  getChartStats(filter: DossierByBoxFilter): Observable<DossierByBoxChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByBoxChartStat[]>(`${this.baseUrl}/dossier-by-box/chart-stats`, { params });
  }

  exportExcel(filter: DossierByBoxFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-box/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByBoxFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId != null && filter.unitId > 0) {
      params = params.set('unitId', filter.unitId.toString());
    }
    if (filter.boxIds?.length) {
      const ids = filter.boxIds
        .map((id) => id?.trim())
        .filter((id): id is string => !!id);
      if (ids.length) {
        params = params.set('boxIds', ids.join(','));
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
