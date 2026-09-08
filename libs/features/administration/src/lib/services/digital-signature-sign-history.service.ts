import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export interface DigitalSignatureSignHistoryItem {
  id: string;
  documentId: string;
  documentName: string | null;
  dossierId: string | null;
  signedFileName: string | null;
  signerName: string | null;
  serialNumber: string | null;
  signedAt: string | null;
  /** "Success" | "Failed" */
  status: string;
  errorMessage: string | null;
  createdDate: string;
}

export interface DigitalSignatureSignHistoryResponse {
  items: DigitalSignatureSignHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface DigitalSignatureSignHistoryFilter {
  page: number;
  pageSize: number;
  keyword?: string | null;
  status?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}

@Injectable({ providedIn: 'root' })
export class DigitalSignatureSignHistoryService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/digital-signature/sign-history`;

  getAll(filter: DigitalSignatureSignHistoryFilter): Observable<DigitalSignatureSignHistoryResponse> {
    let params = new HttpParams()
      .set('page', filter.page)
      .set('pageSize', filter.pageSize);

    if (filter.keyword) params = params.set('keyword', filter.keyword);
    if (filter.status) params = params.set('status', filter.status);
    if (filter.fromDate) params = params.set('fromDate', filter.fromDate);
    if (filter.toDate) params = params.set('toDate', filter.toDate);

    return this.http.get<DigitalSignatureSignHistoryResponse>(this.apiUrl, { params });
  }
}
