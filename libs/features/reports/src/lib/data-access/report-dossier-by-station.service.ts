import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { UnitLookupItem } from './report-dossier-by-year.service';

export interface StationLookupItem {
  id: string;
  name: string;
  code?: string;
}

export interface DossierByStationFilter {
  unitId?: number | null;
  stationIds?: string[] | null;
  year?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByStationSummaryStats {
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
export class ReportDossierByStationService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/reports/statistics`;
  }

  getUnitsLookup(): Observable<UnitLookupItem[]> {
    const params = new HttpParams().set('isactive', 1);
    return this.http.get<UnitLookupItem[]>(`${this.baseUrl}/lookups/units`, { params });
  }

  getStationsLookup(unitId?: number | null): Observable<StationLookupItem[]> {
    let params = new HttpParams();
    if (unitId != null && unitId > 0) {
      params = params.set('unitId', unitId.toString());
    }
    return this.http.get<StationLookupItem[]>(`${this.baseUrl}/lookups/stations`, { params });
  }

  getYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/years`);
  }

  getSummaryStats(filter: DossierByStationFilter): Observable<DossierByStationSummaryStats> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByStationSummaryStats>(`${this.baseUrl}/dossier-by-station/summary-stats`, { params });
  }

  exportExcel(filter: DossierByStationFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-station/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByStationFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId != null && filter.unitId > 0) {
      params = params.set('unitId', filter.unitId.toString());
    }
    if (filter.stationIds?.length) {
      const ids = filter.stationIds
        .map((id) => id?.trim())
        .filter((id): id is string => !!id);
      if (ids.length) {
        params = params.set('stationIds', ids.join(','));
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
