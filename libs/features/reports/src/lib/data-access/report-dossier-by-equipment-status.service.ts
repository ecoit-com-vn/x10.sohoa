import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { UnitLookupItem } from './report-dossier-by-year.service';
import { StationLookupItem } from './report-dossier-by-station.service';
import { LineLookupItem } from './report-dossier-by-line.service';

export interface EquipmentStatusLookupItem {
  id: string;
  name: string;
  code?: string;
}

export interface DossierByEquipmentStatusFilter {
  unitId?: number | null;
  stationIds?: string[] | null;
  equipmentStatusIds?: string[] | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByEquipmentStatusChartStat {
  equipmentTypeCode: string;
  equipmentTypeName: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByEquipmentStatusService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/reports/statistics`;
  }

  getUnitsLookup(): Observable<UnitLookupItem[]> {
    return this.http.get<UnitLookupItem[]>(`${this.baseUrl}/lookups/units`);
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

  getEquipmentStatusesLookup(): Observable<EquipmentStatusLookupItem[]> {
    return this.http.get<EquipmentStatusLookupItem[]>(`${this.baseUrl}/lookups/equipment-statuses`);
  }

  getChartStats(filter: DossierByEquipmentStatusFilter): Observable<DossierByEquipmentStatusChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByEquipmentStatusChartStat[]>(`${this.baseUrl}/dossier-by-equipment-status/chart-stats`, { params });
  }

  exportExcel(filter: DossierByEquipmentStatusFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-equipment-status/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByEquipmentStatusFilter): HttpParams {
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
    if (filter.equipmentStatusIds?.length) {
      const ids = filter.equipmentStatusIds
        .map((id) => id?.trim())
        .filter((id): id is string => !!id);
      if (ids.length) {
        params = params.set('equipmentStatusIds', ids.join(','));
      }
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
