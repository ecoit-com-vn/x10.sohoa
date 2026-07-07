import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import {
  FolderNode,
  Document,
  DocumentFilter,
  PaginatedResponse,
  DocumentVersion,
} from '../models/document.models';

@Injectable({ providedIn: 'root' })
export class DocumentManagementService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get apiUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/dossiers/search`;
  }

  // ===== FOLDER OPERATIONS =====

  getFolderTree(unitId?: number | null) {
    const url = unitId ? `${this.apiUrl}/tree?unitId=${unitId}` : `${this.apiUrl}/tree`;
    return this.http.get<FolderNode[]>(url);
  }

  getFolderById(id: string) {
    return this.http.get<FolderNode>(`${this.apiUrl}/folders/${id}`);
  }

  // ===== DOCUMENT OPERATIONS =====

  getDocuments(filter: DocumentFilter, unitId?: number | null) {
    const params = new URLSearchParams();
    if (filter.folderId) params.append('folderId', filter.folderId);
    if (filter.keyword) params.append('keyword', filter.keyword);
    if (filter.page) params.append('page', filter.page.toString());
    if (filter.pageSize) params.append('pageSize', filter.pageSize.toString());
    if (unitId) params.append('unitId', unitId.toString());

    const queryString = params.toString();
    const url = queryString ? `${this.apiUrl}/documents?${queryString}` : `${this.apiUrl}/documents`;
    return this.http.get<PaginatedResponse<Document>>(url);
  }

  getDocumentById(id: string) {
    return this.http.get<Document>(`${this.apiUrl}/${id}`);
  }

  // ===== DOCUMENT VERSION OPERATIONS =====

  getDocumentVersions(documentId: string) {
    return this.http.get<DocumentVersion[]>(`${this.apiUrl}/${documentId}/versions`);
  }

  getRelatedDossiers(dossierId: string, filter: { keyword?: string; dossierTypeId?: string; page?: number; pageSize?: number }) {
    const params = new URLSearchParams();
    if (filter.keyword) params.append('keyword', filter.keyword);
    if (filter.dossierTypeId) params.append('dossierTypeId', filter.dossierTypeId);
    if (filter.page) params.append('page', filter.page.toString());
    if (filter.pageSize) params.append('pageSize', filter.pageSize.toString());

    const queryString = params.toString();
    const url = queryString ? `${this.apiUrl}/${dossierId}/related?${queryString}` : `${this.apiUrl}/${dossierId}/related`;
    return this.http.get<any>(url);
  }
}
