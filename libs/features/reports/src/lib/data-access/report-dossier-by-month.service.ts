import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { ObjectTypeLookupItem, UnitLookupItem } from './report-dossier-by-year.service';

export interface ReportMonthLookupItem {
  year: number;
  month: number;
  label: string;
}

export interface MonthYearGroup {
  label: string;
  items: ReportMonthLookupItem[];
}

export interface DossierByMonthFilter {
  unitId?: number | null;
  objectType?: number | null;
  year?: number | null;
  month?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByMonthChartStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

export interface DossierByMonthRatioStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  percentage: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByMonthService {
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

  getMonthsLookup(): Observable<ReportMonthLookupItem[]> {
    return this.http.get<ReportMonthLookupItem[]>(`${this.baseUrl}/lookups/months`);
  }

  groupMonthsByYear(months: ReportMonthLookupItem[]): MonthYearGroup[] {
    const map = new Map<number, ReportMonthLookupItem[]>();
    for (const item of months) {
      const list = map.get(item.year) ?? [];
      list.push(item);
      map.set(item.year, list);
    }

    return Array.from(map.entries())
      .sort(([a], [b]) => b - a)
      .map(([year, items]) => ({
        label: String(year),
        items: items.sort((a, b) => b.month - a.month)
      }));
  }

  getChartStats(filter: DossierByMonthFilter): Observable<DossierByMonthChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByMonthChartStat[]>(`${this.baseUrl}/dossier-by-month/chart-stats`, { params });
  }

  getRatioStats(filter: DossierByMonthFilter): Observable<DossierByMonthRatioStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByMonthRatioStat[]>(`${this.baseUrl}/dossier-by-month/ratio-stats`, { params });
  }

  exportExcel(filter: DossierByMonthFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-month/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByMonthFilter): HttpParams {
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
    if (filter.month != null && filter.month > 0) {
      params = params.set('month', filter.month.toString());
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
