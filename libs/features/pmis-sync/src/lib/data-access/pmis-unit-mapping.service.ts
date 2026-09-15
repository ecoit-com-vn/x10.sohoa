import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export interface PmisUnitCodeMapping {
  id: string;
  pmisUnitCode: string;
  unitId: number;
  unitName: string | null;
  unitCode: string | null;
  note: string | null;
  createdDate: string;
}

export interface CreatePmisUnitCodeMappingRequest {
  pmisUnitCode: string;
  unitId: number;
  note: string | null;
}

export interface OrganizationUnitOption {
  id: number;
  code: string;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class PmisUnitMappingService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/pmis-unit-code-mapping`;
  private readonly unitsLookupUrl = `${environment.apiGatewayUrl}/api/v1/organization-units/lookup-all-active`;

  getAll(): Observable<PmisUnitCodeMapping[]> {
    return this.http.get<PmisUnitCodeMapping[]>(this.apiUrl);
  }

  create(request: CreatePmisUnitCodeMappingRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.apiUrl, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  getOrganizationUnits(): Observable<OrganizationUnitOption[]> {
    return this.http.get<OrganizationUnitOption[]>(this.unitsLookupUrl);
  }
}
