import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { ObjectTypeLookupItem, UnitLookupItem } from './report-dossier-by-year.service';

export interface EquipmentTypeLookupItem {
  id: string;
  name: string;
  code?: string;
}

export interface DossierByEquipmentTypeFilter {
  unitId?: number | null;
  objectType?: number | null;
  equipmentTypeIds?: string[] | null;
  year?: number | null;
  page?: number;
  pageSize?: number;
}

export interface DossierByEquipmentTypeChartStat {
  equipmentTypeCode: string;
  equipmentTypeName: string;
  dossierCount: number;
  documentCount: number;
  pageCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReportDossierByEquipmentTypeService {
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

  getEquipmentTypesLookup(): Observable<EquipmentTypeLookupItem[]> {
    return this.http.get<EquipmentTypeLookupItem[]>(`${this.baseUrl}/lookups/equipment-types`);
  }

  getYearsLookup(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/lookups/years`);
  }

  getChartStats(filter: DossierByEquipmentTypeFilter): Observable<DossierByEquipmentTypeChartStat[]> {
    const params = this.buildParams(filter);
    return this.http.get<DossierByEquipmentTypeChartStat[]>(`${this.baseUrl}/dossier-by-equipment-type/chart-stats`, { params });
  }

  exportExcel(filter: DossierByEquipmentTypeFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.baseUrl}/dossier-by-equipment-type/export`, {
      params,
      responseType: 'blob'
    });
  }

  private buildParams(filter: DossierByEquipmentTypeFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId != null && filter.unitId > 0) {
      params = params.set('unitId', filter.unitId.toString());
    }
    if (filter.objectType != null) {
      params = params.set('objectType', filter.objectType.toString());
    }
    if (filter.equipmentTypeIds?.length) {
      const ids = filter.equipmentTypeIds
        .map((id) => id?.trim())
        .filter((id): id is string => !!id);
      if (ids.length) {
        params = params.set('equipmentTypeIds', ids.join(','));
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
