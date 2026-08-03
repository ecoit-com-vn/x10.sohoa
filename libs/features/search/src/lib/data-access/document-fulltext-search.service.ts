import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

export type DocumentFulltextSort = 'newest' | 'oldest' | 'relevance';

export interface DocumentFulltextSearchFilter {
  keyword?: string;
  sort?: DocumentFulltextSort;
  page?: number;
  pageSize?: number;
}

export interface DocumentFulltextSearchItem {
  documentVersionId: string;
  documentId: string;
  documentName: string;
  highlight?: string | null;
  mimeType?: string | null;
  dossierId?: string | null;
  dossierTitle?: string | null;
  infrastructureName?: string | null;
  dossierTypeName?: string | null;
  documentTypeName?: string | null;
  equipmentNames?: string[];
  indexedAt?: string | null;
}

export interface DocumentFulltextSearchResponse {
  items: DocumentFulltextSearchItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  keyword?: string | null;
}

export interface DocumentFulltextDetail {
  documentVersionId: string;
  documentId: string;
  documentName: string;
  mimeType?: string | null;
  dossierId?: string | null;
  dossierTitle?: string | null;
  infrastructureId?: string | null;
  infrastructureName?: string | null;
  infrastructureCode?: string | null;
  dossierTypeId?: string | null;
  dossierTypeName?: string | null;
  documentTypeId?: string | null;
  documentTypeName?: string | null;
  equipmentNames?: string[];
  extractionSummary?: string | null;
  mergedDataJson?: string | null;
  ocrCompletedAt?: string | null;
  indexedAt?: string | null;
}

interface DownloadTokenResponse {
  token: string;
  expiresInSeconds: number;
}

@Injectable({ providedIn: 'root' })
export class DocumentFulltextSearchService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get apiBase() {
    return `${this.config.apiGatewayUrl}/api/v1/document-fulltext-search`;
  }

  private get documentsBase() {
    return `${this.apiBase}/documents`;
  }

  search(filter: DocumentFulltextSearchFilter): Observable<DocumentFulltextSearchResponse> {
    let params = new HttpParams();
    if (filter.keyword?.trim()) {
      params = params.set('keyword', filter.keyword.trim());
    }
    if (filter.sort) {
      params = params.set('sort', filter.sort);
    }
    if (filter.page) {
      params = params.set('page', filter.page);
    }
    if (filter.pageSize) {
      params = params.set('pageSize', filter.pageSize);
    }
    return this.http.get<DocumentFulltextSearchResponse>(this.documentsBase, { params });
  }

  getDetail(versionId: string): Observable<DocumentFulltextDetail> {
    return this.http.get<DocumentFulltextDetail>(`${this.documentsBase}/${versionId}`);
  }

  getDossier(dossierId: string): Observable<unknown> {
    return this.http.get<unknown>(`${this.apiBase}/dossiers/${dossierId}`);
  }

  getDossierDocuments(
    dossierId: string,
    filter: { keyword?: string; page: number; pageSize: number }
  ): Observable<{ items: unknown[]; totalCount: number; page: number; pageSize: number }> {
    let params = new HttpParams()
      .set('page', String(filter.page))
      .set('pageSize', String(filter.pageSize));
    if (filter.keyword?.trim()) {
      params = params.set('keyword', filter.keyword.trim());
    }
    return this.http.get<{ items: unknown[]; totalCount: number; page: number; pageSize: number }>(
      `${this.apiBase}/dossiers/${dossierId}/documents`,
      { params }
    );
  }

  getRelatedDossiers(
    dossierId: string,
    filter: { keyword?: string; dossierTypeId?: string; page?: number; pageSize?: number }
  ): Observable<{ items: unknown[]; totalCount: number; page: number; pageSize: number }> {
    let params = new HttpParams()
      .set('page', String(filter.page ?? 1))
      .set('pageSize', String(filter.pageSize ?? 10));
    if (filter.keyword?.trim()) {
      params = params.set('keyword', filter.keyword.trim());
    }
    if (filter.dossierTypeId) {
      params = params.set('dossierTypeId', filter.dossierTypeId);
    }
    return this.http.get<{ items: unknown[]; totalCount: number; page: number; pageSize: number }>(
      `${this.apiBase}/dossiers/${dossierId}/related`,
      { params }
    );
  }

  getDocumentFormTemplate(dossierId: string, versionId: string): Observable<unknown> {
    return this.http.get<unknown>(
      `${this.apiBase}/dossiers/${dossierId}/documents/${versionId}/form-template`
    );
  }

  getFormTemplate(formId: string): Observable<unknown> {
    return this.http.get<unknown>(`${this.apiBase}/form-templates/${formId}/get-form`);
  }

  getDossierTypeLookup(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.apiBase}/dossier-types/lookup`);
  }

  getDownloadToken(
    dossierId: string,
    versionId: string,
    purpose: 'DOWNLOAD' | 'PREVIEW' = 'DOWNLOAD'
  ): Observable<DownloadTokenResponse> {
    return this.http.get<DownloadTokenResponse>(
      `${this.apiBase}/dossiers/${dossierId}/documents/${versionId}/download-url`,
      { params: { purpose } }
    );
  }

  private getStreamUrl(token: string): string {
    return `${this.config.apiGatewayUrl}/api/v1/files/download?token=${encodeURIComponent(token)}`;
  }

  async getPreviewBlobUrl(dossierId: string, versionId: string): Promise<string> {
    const tokenResponse = await firstValueFrom(this.getDownloadToken(dossierId, versionId, 'PREVIEW'));
    if (!tokenResponse?.token) {
      throw new Error('Không thể tạo link xem trước');
    }
    const url = this.getStreamUrl(tokenResponse.token);
    const response = await fetch(url, { method: 'GET', credentials: 'include' });
    if (!response.ok) {
      throw new Error('Không thể tải file xem trước');
    }
    const blob = await response.blob();
    return window.URL.createObjectURL(blob);
  }

  async downloadFile(dossierId: string, versionId: string, fileName?: string): Promise<void> {
    const tokenResponse = await firstValueFrom(this.getDownloadToken(dossierId, versionId));
    if (!tokenResponse?.token) {
      throw new Error('Không thể tạo link tải file');
    }
    const url = this.getStreamUrl(tokenResponse.token);
    const response = await fetch(url, { method: 'GET', credentials: 'include' });
    if (!response.ok) {
      throw new Error('Không thể tải file');
    }
    const blob = await response.blob();
    const objectUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = fileName || 'download';
    link.rel = 'noopener';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(objectUrl);
  }
}
