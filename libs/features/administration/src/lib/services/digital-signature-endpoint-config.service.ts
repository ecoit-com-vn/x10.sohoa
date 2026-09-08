import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export interface DigitalSignatureEndpointConfig {
  id: string;
  apiCode: string;
  displayName: string;
  url: string | null;
  isActive: boolean;
  rowVersion: number;
}

export interface UpdateDigitalSignatureEndpointConfigRequest {
  url: string | null;
  isActive: boolean;
  rowVersion: number;
}

@Injectable({ providedIn: 'root' })
export class DigitalSignatureEndpointConfigService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/digital-signature/endpoint-config`;

  getAll(): Observable<DigitalSignatureEndpointConfig[]> {
    return this.http.get<DigitalSignatureEndpointConfig[]>(this.apiUrl);
  }

  update(apiCode: string, request: UpdateDigitalSignatureEndpointConfigRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${apiCode}`, request);
  }
}
