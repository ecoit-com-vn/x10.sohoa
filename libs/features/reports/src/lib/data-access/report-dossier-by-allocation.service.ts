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

export interface InputUserLookupItem {
  id: string;
  name: string;
}

export interface DossierByAllocationFilter {
  unitId?: number | null;
  objectType?: number | null;
  year?: number | null;
  createdBy?: string | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByAllocationChartStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

export interface DossierByAllocationRatioStat {
  groupName: string;
  groupCode: string;
  dossierCount: number;
  percentage: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByAllocationService {
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

  getInputUsersLookup(segment?: string): Observable<InputUserLookupItem[]> {
    const url = segment
      ? `${this.baseUrl}/${segment}/input-users`
      : `${this.baseUrl}/lookups/input-users`;
    return this.http.get<InputUserLookupItem[]>(url);
  }

  getChartStats(filter: DossierByAllocationFilter, segment = 'dossier-by-allocation'): Observable<DossierByAllocationChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByAllocationChartStat[]>(`${this.baseUrl}/${segment}/chart-stats`, { params });
  }

  getRatioStats(filter: DossierByAllocationFilter, segment = 'dossier-by-allocation'): Observable<DossierByAllocationRatioStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByAllocationRatioStat[]>(`${this.baseUrl}/${segment}/ratio-stats`, { params });
  }

  exportExcel(filter: DossierByAllocationFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-allocation/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByAllocationFilter): HttpParams {
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
    if (filter.createdBy != null && filter.createdBy.trim() !== '') {
      params = params.set('createdBy', filter.createdBy.trim());
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
