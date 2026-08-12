import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { UnitLookupItem } from './report-dossier-by-year.service';

export interface DossierTypeLookupItem {
  id: string;
  name: string;
  code?: string;
}

export interface DossierByDossierTypeFilter {
  unitId?: number | null;
  dossierTypeIds?: string[] | null;
  year?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByDossierTypeChartStat {
  dossierTypeCode: string;
  dossierTypeName: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByDossierTypeService {
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

  getDossierTypesLookup(): Observable<DossierTypeLookupItem[]> {
    return this.http.get<DossierTypeLookupItem[]>(`${this.baseUrl}/lookups/dossier-types`);
  }

  getYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/years`);
  }

  getChartStats(filter: DossierByDossierTypeFilter): Observable<DossierByDossierTypeChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByDossierTypeChartStat[]>(`${this.baseUrl}/dossier-by-dossier-type/chart-stats`, { params });
  }

  exportExcel(filter: DossierByDossierTypeFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-dossier-type/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByDossierTypeFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId != null && filter.unitId > 0) {
      params = params.set('unitId', filter.unitId.toString());
    }
    if (filter.dossierTypeIds?.length) {
      const ids = filter.dossierTypeIds
        .map((id) => id?.trim())
        .filter((id): id is string => !!id);
      if (ids.length) {
        params = params.set('dossierTypeIds', ids.join(','));
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
