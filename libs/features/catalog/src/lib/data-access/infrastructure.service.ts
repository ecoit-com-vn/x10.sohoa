import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';

@Injectable({
  providedIn: 'root'
})
export class InfrastructureService {
  private api = inject(ApiService);

  private getBaseUrl(infraTypeId: number) {
    const segment = infraTypeId === 1 ? 'substation' : 'transmission-line';
    return `/api/catalog/${segment}`;
  }

  private get unitsBase() {
    return `/api/v1/organization-units`;
  }

  getInfrastructures(
    infraTypeId: number,
    page: number,
    pageSize: number,
    keyword?: string,
    status?: string
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

    return this.api.get<any>(this.getBaseUrl(infraTypeId), { params });
  }

  getInfrastructureById(infraTypeId: number, id: string): Observable<any> {
    return this.api.get<any>(`${this.getBaseUrl(infraTypeId)}/${id}`);
  }

  createInfrastructure(infraTypeId: number, item: any): Observable<any> {
    return this.api.post<any>(this.getBaseUrl(infraTypeId), item);
  }

  updateInfrastructure(infraTypeId: number, id: string, item: any): Observable<any> {
    return this.api.put<any>(`${this.getBaseUrl(infraTypeId)}/${id}`, item);
  }

  deleteInfrastructure(infraTypeId: number, id: string): Observable<any> {
    return this.api.delete<any>(`${this.getBaseUrl(infraTypeId)}/${id}`);
  }

  toggleStatus(infraTypeId: number, id: string, isLocking: boolean): Observable<any> {
    const action = isLocking ? 'lock' : 'unlock';
    return this.api.post<any>(`${this.getBaseUrl(infraTypeId)}/${id}/${action}`, {});
  }

  getOrganizationUnits(): Observable<any[]> {
    return this.api.get<any[]>(this.unitsBase);
  }
}
