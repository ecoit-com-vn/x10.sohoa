import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { BhsCatalogColumn } from '../../../../dossier-management/src/lib/data-access/dossier-management.service';

export interface DossierByEquipmentFilter {
  keyword?: string;
  publishDateFrom?: string;
  publishDateTo?: string;
  gridTypeId?: number | null;
  infrastructureId?: string | null;
  equipmentTypeId?: string | null;
  equipmentId?: string | null;
  dossierTypeId?: string | null;
}

export interface DossierByEquipmentLookupItem {
  id: string;
  name: string;
  code?: string;
}

@Injectable({ providedIn: 'root' })
export class DossierByEquipmentService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get lookupBase() {
    return `${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment`;
  }

  private get searchBase() {
    return `${this.config.apiGatewayUrl}/api/v1/search-dossiers-by-equipment`;
  }

  private buildFilterParams(filter: DossierByEquipmentFilter): HttpParams {
    let params = new HttpParams();
    if (filter.keyword?.trim()) params = params.set('keyword', filter.keyword.trim());
    if (filter.publishDateFrom) params = params.set('publishDateFrom', new Date(filter.publishDateFrom).toISOString());
    if (filter.publishDateTo) params = params.set('publishDateTo', new Date(filter.publishDateTo).toISOString());
    if (filter.gridTypeId != null) params = params.set('gridTypeId', filter.gridTypeId.toString());
    if (filter.infrastructureId) params = params.set('infrastructureId', filter.infrastructureId);
    if (filter.equipmentTypeId) params = params.set('equipmentTypeId', filter.equipmentTypeId);
    if (filter.equipmentId) params = params.set('equipmentId', filter.equipmentId);
    if (filter.dossierTypeId) params = params.set('dossierTypeId', filter.dossierTypeId);
    return params;
  }

  getInfrastructures(filter: DossierByEquipmentFilter): Observable<DossierByEquipmentLookupItem[]> {
    return this.http.get<DossierByEquipmentLookupItem[]>(`${this.lookupBase}/infrastructures`, {
      params: this.buildFilterParams(filter)
    });
  }

  getEquipmentTypes(filter: DossierByEquipmentFilter): Observable<DossierByEquipmentLookupItem[]> {
    return this.http.get<DossierByEquipmentLookupItem[]>(`${this.lookupBase}/equipment-types`, {
      params: this.buildFilterParams(filter)
    });
  }

  getEquipments(filter: DossierByEquipmentFilter): Observable<DossierByEquipmentLookupItem[]> {
    return this.http.get<DossierByEquipmentLookupItem[]>(`${this.lookupBase}/equipments`, {
      params: this.buildFilterParams(filter)
    });
  }

  getDossierTypes(filter: DossierByEquipmentFilter): Observable<DossierByEquipmentLookupItem[]> {
    return this.http.get<DossierByEquipmentLookupItem[]>(`${this.lookupBase}/dossier-types`, {
      params: this.buildFilterParams(filter)
    });
  }

  getBhsColumns(): Observable<BhsCatalogColumn[]> {
    return this.http.get<BhsCatalogColumn[]>(`${this.lookupBase}/bhs-columns`);
  }

  getGridTypes(): Observable<DossierByEquipmentLookupItem[]> {
    return this.http.get<DossierByEquipmentLookupItem[]>(
      `${this.config.apiGatewayUrl}/api/v1/dossiers/grid-types/lookup`
    );
  }

  search(filter: DossierByEquipmentFilter & { page: number; pageSize: number }): Observable<{
    items: unknown[];
    totalCount: number;
    page: number;
    pageSize: number;
  }> {
    let params = this.buildFilterParams(filter)
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    return this.http.get<{ items: unknown[]; totalCount: number; page: number; pageSize: number }>(
      this.searchBase,
      { params }
    );
  }
}
