import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import { DossierListTab, DossierTabCounts } from '../utils/dossier-status.util';

@Injectable({
  providedIn: 'root'
})
export class DossierPublishService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get searchBase() {
    return `${this.config.apiGatewayUrl}/api/v1/search-publish`;
  }

  private get mutationBase() {
    return `${this.config.apiGatewayUrl}/api/v1/dossier-publish`;
  }

  getPaged(filter: {
    tab?: DossierListTab;
    keyword?: string;
    infrastructureId?: string;
    dossierTypeId?: string;
    equipmentId?: string;
    unitId?: number;
    page: number;
    pageSize: number;
  }): Observable<{ items: unknown[]; totalCount: number; page: number; pageSize: number }> {
    let params = new HttpParams()
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.tab) params = params.set('tab', filter.tab);
    if (filter.keyword?.trim()) params = params.set('keyword', filter.keyword.trim());
    if (filter.infrastructureId) params = params.set('infrastructureId', filter.infrastructureId);
    if (filter.dossierTypeId) params = params.set('dossierTypeId', filter.dossierTypeId);
    if (filter.equipmentId) params = params.set('equipmentId', filter.equipmentId);
    if (filter.unitId != null) params = params.set('unitId', filter.unitId.toString());

    return this.http.get<{ items: unknown[]; totalCount: number; page: number; pageSize: number }>(this.searchBase, { params });
  }

  getDetail(id: string): Observable<unknown> {
    return this.http.get<unknown>(`${this.mutationBase}/${id}`);
  }

  create(dto: unknown): Observable<unknown> {
    return this.http.post<unknown>(this.mutationBase, dto);
  }

  update(id: string, dto: unknown): Observable<unknown> {
    return this.http.put<unknown>(`${this.mutationBase}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.mutationBase}/${id}`);
  }

  getTabCounts(filter: {
    keyword?: string;
    infrastructureId?: string;
    dossierTypeId?: string;
    equipmentId?: string;
    unitId?: number;
  }): Observable<DossierTabCounts> {
    let params = new HttpParams();

    if (filter.keyword?.trim()) params = params.set('keyword', filter.keyword.trim());
    if (filter.infrastructureId) params = params.set('infrastructureId', filter.infrastructureId);
    if (filter.dossierTypeId) params = params.set('dossierTypeId', filter.dossierTypeId);
    if (filter.equipmentId) params = params.set('equipmentId', filter.equipmentId);
    if (filter.unitId != null) params = params.set('unitId', filter.unitId.toString());

    return this.http.get<DossierTabCounts>(`${this.searchBase}/tab-counts`, { params });
  }

  publish(id: string): Observable<{ success: boolean; publishStatusId: number }> {
    return this.http.put<{ success: boolean; publishStatusId: number }>(`${this.mutationBase}/${id}/publish`, {});
  }

  unpublish(id: string): Observable<{ success: boolean; publishStatusId: number }> {
    return this.http.put<{ success: boolean; publishStatusId: number }>(`${this.mutationBase}/${id}/unpublish`, {});
  }

  republish(id: string): Observable<{ success: boolean; publishStatusId: number }> {
    return this.http.put<{ success: boolean; publishStatusId: number }>(`${this.mutationBase}/${id}/republish`, {});
  }
}
