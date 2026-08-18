import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { UnitLookupItem } from './report-dossier-by-year.service';
import { StationLookupItem } from './report-dossier-by-station.service';
import { LineLookupItem } from './report-dossier-by-line.service';

export interface DossierByManufactureYearFilter {
  unitId?: number | null;
  stationIds?: string[] | null;
  manufactureYear?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByManufactureYearChartStat {
  equipmentTypeCode: string;
  equipmentTypeName: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByManufactureYearService {
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

  getLinesLookup(unitId?: number | null): Observable<LineLookupItem[]> {
    let params = new HttpParams();
    if (unitId != null && unitId > 0) {
      params = params.set('unitId', unitId.toString());
    }
    return this.http.get<LineLookupItem[]>(`${this.baseUrl}/lookups/lines`, { params });
  }

  getManufactureYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/manufacture-years`);
  }

  getChartStats(filter: DossierByManufactureYearFilter): Observable<DossierByManufactureYearChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByManufactureYearChartStat[]>(`${this.baseUrl}/dossier-by-manufacture-year/chart-stats`, { params });
  }

  exportExcel(filter: DossierByManufactureYearFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-manufacture-year/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByManufactureYearFilter): HttpParams {
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
    if (filter.manufactureYear != null && filter.manufactureYear > 0) {
      params = params.set('manufactureYear', filter.manufactureYear.toString());
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
