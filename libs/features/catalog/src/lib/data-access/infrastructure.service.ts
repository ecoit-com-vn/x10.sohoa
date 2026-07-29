import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class InfrastructureService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private getBaseUrl(infraTypeId: number) {
    const segment = infraTypeId === 1 ? 'substation' : 'transmission-line';
    return `${this.config.apiGatewayUrl}/api/catalog/${segment}`;
  }

  private get unitsBase() {
    return `${this.config.apiGatewayUrl}/api/v1/organization-units/lookup`;
  }

  getInfrastructures(
    infraTypeId: number,
    page: number,
    pageSize: number,
    keyword?: string,
    status?: string,
    unitId?: number | null,
    personalOnly?: boolean
  ): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (keyword && keyword.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (status !== undefined && status !== null && status !== '') {
      params = params.set('status', status);
    }
    if (unitId !== undefined && unitId !== null && String(unitId) !== 'null') {
      params = params.set('unitId', unitId.toString());
    }
    if (personalOnly) {
      params = params.set('personalOnly', 'true');
    }

    return this.http.get<any>(this.getBaseUrl(infraTypeId), { params });
  }

  getInfrastructureById(infraTypeId: number, id: string): Observable<any> {
    return this.http.get<any>(`${this.getBaseUrl(infraTypeId)}/${id}`);
  }

  createInfrastructure(infraTypeId: number, item: any): Observable<any> {
    return this.http.post<any>(this.getBaseUrl(infraTypeId), item);
  }

  updateInfrastructure(infraTypeId: number, id: string, item: any): Observable<any> {
    return this.http.put<any>(`${this.getBaseUrl(infraTypeId)}/${id}`, item);
  }

  deleteInfrastructure(infraTypeId: number, id: string): Observable<any> {
    return this.http.delete<any>(`${this.getBaseUrl(infraTypeId)}/${id}`);
  }

  toggleStatus(infraTypeId: number, id: string, isLocking: boolean): Observable<any> {
    const action = isLocking ? 'lock' : 'unlock';
    return this.http.post<any>(`${this.getBaseUrl(infraTypeId)}/${id}/${action}`, {});
  }

  getOrganizationUnits(): Observable<any[]> {
    return this.http.get<any[]>(this.unitsBase);
  }

  getGridTypes(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/dossiers/grid-types/lookup`);
  }

  getEquipmentTypes(): Observable<any[]> {
    return this.http.get<any[]>(`${this.config.apiGatewayUrl}/api/v1/equipment/get-equipment-types`);
  }
}
