import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiService, APP_CONFIG } from '@sohoa.frontend/shared/core';
import { PmisCatalogDocumentsResponse, PmisCatalogNode } from '../models/pmis-catalog.models';

interface DownloadTokenResponse {
  token: string;
  expiresInSeconds: number;
}

@Injectable({ providedIn: 'root' })
export class PmisDocumentCatalogService {
  private api = inject(ApiService);
  private config = inject(APP_CONFIG);
  private readonly base = '/api/v1/pmis-documents';

  getCatalogTree(): Observable<PmisCatalogNode[]> {
    return this.api.get<PmisCatalogNode[]>(`${this.base}/catalog/tree`);
  }

  getCatalogDocuments(folderId: string, keyword: string | null, page: number, pageSize: number): Observable<PmisCatalogDocumentsResponse> {
    const params: Record<string, string> = { folderId, page: String(page), pageSize: String(pageSize) };
    if (keyword) params['keyword'] = keyword;
    return this.api.get<PmisCatalogDocumentsResponse>(`${this.base}/catalog/documents`, { params });
  }

  private getDownloadToken(documentId: string): Observable<DownloadTokenResponse> {
    return this.api.get<DownloadTokenResponse>(`${this.base}/${documentId}/download-url`);
  }

  /** Tải file qua token 1 lần (giống hệt cơ chế FileDownloadService của kho thư mục thiết bị). */
  async downloadDocument(documentId: string, fileName?: string): Promise<void> {
    const tokenResponse = await firstValueFrom(this.getDownloadToken(documentId));
    if (!tokenResponse?.token) throw new Error('Không thể tạo link tải file');

    const url = `${this.config.apiGatewayUrl}/api/v1/files/download?token=${encodeURIComponent(tokenResponse.token)}`;
    const response = await fetch(url, { method: 'GET', credentials: 'include' });
    if (!response.ok) {
      let message = 'Không thể tải file';
      try {
        const body = await response.json();
        message = body?.message || message;
      } catch {
        // ignore parse errors
      }
      throw new Error(message);
    }

    const blob = await response.blob();
    const objectUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = fileName || 'tai-lieu-pmis';
    link.rel = 'noopener';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(objectUrl);
  }
}
