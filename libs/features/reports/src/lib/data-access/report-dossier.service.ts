import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { BhsCatalogColumn } from '@sohoa.frontend/features/dossier-management';
import { ReportDossierConfig, ReportDossierType } from './report-dossier.config';

export interface ReportDossierLookupItem {
  id: string;
  name: string;
  code?: string;
}

export interface ReportDossierFilter {
  unitId?: string | null;
  gridTypeId?: string | null;
  infrastructureId?: string | null;
  equipmentId?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ReportDossierService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private base(segment: ReportDossierType) {
    return `${this.config.apiGatewayUrl}/api/v1/reports/${segment}`;
  }

  private buildParams(filter: ReportDossierFilter): HttpParams {
    let params = new HttpParams();
    if (filter.unitId) params = params.set('unitId', filter.unitId);
    if (filter.gridTypeId) params = params.set('gridTypeId', filter.gridTypeId);
    if (filter.infrastructureId) params = params.set('infrastructureId', filter.infrastructureId);
    if (filter.equipmentId) params = params.set('equipmentId', filter.equipmentId);
    return params;
  }

  getUnits(cfg: ReportDossierConfig): Observable<ReportDossierLookupItem[]> {
    return this.http.get<ReportDossierLookupItem[]>(`${this.base(cfg.apiSegment)}/lookups/units`);
  }

  getSecondaryLookups(cfg: ReportDossierConfig, filter: ReportDossierFilter): Observable<ReportDossierLookupItem[]> {
    const params = this.buildParams(filter);
    const path =
      cfg.secondaryLookup === 'gridTypes'
        ? 'lookups/grid-types'
        : cfg.secondaryLookup === 'equipments'
          ? 'lookups/equipments'
          : cfg.secondaryLookup === 'stations'
            ? 'lookups/stations'
            : 'lookups/lines';

    return this.http.get<ReportDossierLookupItem[]>(`${this.base(cfg.apiSegment)}/${path}`, { params });
  }

  getBhsColumns(cfg: ReportDossierConfig): Observable<BhsCatalogColumn[]> {
    return this.http.get<BhsCatalogColumn[]>(`${this.base(cfg.apiSegment)}/bhs-columns`);
  }

  search(
    cfg: ReportDossierConfig,
    filter: ReportDossierFilter & { page: number; pageSize: number }
  ): Observable<{ items: unknown[]; totalCount: number; page: number; pageSize: number }> {
    let params = this.buildParams(filter)
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    return this.http.get<{ items: unknown[]; totalCount: number; page: number; pageSize: number }>(
      this.base(cfg.apiSegment),
      { params }
    );
  }

  exportExcel(cfg: ReportDossierConfig, filter: ReportDossierFilter): Observable<Blob> {
    const params = this.buildParams(filter);
    return this.http.get(`${this.base(cfg.apiSegment)}/export`, {
      params,
      responseType: 'blob'
    });
  }

  getDetail(cfg: ReportDossierConfig, id: string): Observable<unknown> {
    return this.http.get<unknown>(`${this.base(cfg.apiSegment)}/${id}`);
  }
}
