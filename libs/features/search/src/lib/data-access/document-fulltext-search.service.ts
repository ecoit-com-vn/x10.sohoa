import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
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

export interface DocumentFulltextSearchDetail {
  documentVersionId: string;
  documentId: string;
  documentName: string;
  mimeType?: string | null;
  filePath?: string | null;
  bucketName?: string | null;
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

@Injectable({ providedIn: 'root' })
export class DocumentFulltextSearchService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get base() {
    return `${this.config.apiGatewayUrl}/api/v1/search/documents`;
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
    return this.http.get<DocumentFulltextSearchResponse>(this.base, { params });
  }

  getDetail(versionId: string): Observable<DocumentFulltextSearchDetail> {
    return this.http.get<DocumentFulltextSearchDetail>(`${this.base}/${versionId}`);
  }
}
