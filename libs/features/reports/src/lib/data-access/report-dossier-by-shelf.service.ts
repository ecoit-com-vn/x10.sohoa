import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { UnitLookupItem } from './report-dossier-by-year.service';

export interface ShelfLookupItem {
  id: string;
  name: string;
  code?: string;
}

export interface DossierByShelfFilter {
  unitId?: number | null;
  shelfIds?: string[] | null;
  year?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByShelfChartStat {
  shelfCode: string;
  shelfName: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByShelfService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/reports/statistics`;
  }

  getUnitsLookup(): Observable<UnitLookupItem[]> {
    return this.http.get<UnitLookupItem[]>(`${this.baseUrl}/lookups/units`);
  }

  getShelvesLookup(unitId?: number | null): Observable<ShelfLookupItem[]> {
    let params = new HttpParams();
    if (unitId != null && unitId > 0) {
      params = params.set('unitId', unitId.toString());
    }
    return this.http.get<ShelfLookupItem[]>(`${this.baseUrl}/lookups/shelves`, { params });
  }

  getYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/years`);
  }

  getChartStats(filter: DossierByShelfFilter): Observable<DossierByShelfChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByShelfChartStat[]>(`${this.baseUrl}/dossier-by-shelf/chart-stats`, { params });
  }

  exportExcel(filter: DossierByShelfFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-shelf/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByShelfFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId != null && filter.unitId > 0) {
      params = params.set('unitId', filter.unitId.toString());
    }
    if (filter.shelfIds?.length) {
      const ids = filter.shelfIds
        .map((id) => id?.trim())
        .filter((id): id is string => !!id);
      if (ids.length) {
        params = params.set('shelfIds', ids.join(','));
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
