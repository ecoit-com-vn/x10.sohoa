import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export interface PmisApiEndpointConfig {
  id: string;
  apiCode: string;
  displayName: string;
  url: string | null;
  httpMethod: string;
  timeoutSeconds: number | null;
  isActive: boolean;
  rowVersion: number;
  headerCount: number;
}

export interface PmisApiEndpointHeader {
  id: string;
  headerKey: string;
  headerValue: string | null;
  isSecret: boolean;
}

export interface UpdatePmisApiEndpointConfigRequest {
  url: string | null;
  timeoutSeconds: number | null;
  isActive: boolean;
  rowVersion: number;
}

@Injectable({ providedIn: 'root' })
export class PmisEndpointConfigService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/sync/endpoint-config`;

  getAll(): Observable<PmisApiEndpointConfig[]> {
    return this.http.get<PmisApiEndpointConfig[]>(this.apiUrl);
  }

  getHeaders(apiCode: string): Observable<PmisApiEndpointHeader[]> {
    return this.http.get<PmisApiEndpointHeader[]>(`${this.apiUrl}/${apiCode}/headers`);
  }

  update(apiCode: string, request: UpdatePmisApiEndpointConfigRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${apiCode}`, request);
  }

  replaceHeaders(apiCode: string, headers: PmisApiEndpointHeader[]): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${apiCode}/headers`, { headers });
  }
}
