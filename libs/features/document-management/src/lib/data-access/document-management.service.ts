import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import {
  FolderNode,
  Document,
  CreateFolderRequest,
  UpdateFolderRequest,
  CreateDocumentRequest,
  UpdateDocumentRequest,
  DocumentFilter,
  PaginatedResponse,
  DocumentVersion,
} from '../models/document.models';

@Injectable({ providedIn: 'root' })
export class DocumentManagementService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get apiUrl(): string {
    return `${this.config.apiGatewayUrl}/api/v1/documents`;
  }

  // ===== FOLDER OPERATIONS =====

  getFolderTree() {
    return this.http.get<FolderNode[]>(`${this.apiUrl}/folders/tree`);
  }

  getFolderById(id: string) {
    return this.http.get<FolderNode>(`${this.apiUrl}/folders/${id}`);
  }

  createFolder(req: CreateFolderRequest) {
    return this.http.post<{ id: string }>(`${this.apiUrl}/folders`, req);
  }

  updateFolder(id: string, req: UpdateFolderRequest) {
    return this.http.put(`${this.apiUrl}/folders/${id}`, req);
  }

  deleteFolder(id: string) {
    return this.http.delete(`${this.apiUrl}/folders/${id}`);
  }

  // ===== DOCUMENT OPERATIONS =====

  getDocuments(filter: DocumentFilter) {
    const params = new URLSearchParams();
    if (filter.folderId) params.append('folderId', filter.folderId);
    if (filter.keyword) params.append('keyword', filter.keyword);
    if (filter.page) params.append('page', filter.page.toString());
    if (filter.pageSize) params.append('pageSize', filter.pageSize.toString());

    const queryString = params.toString();
    const url = queryString ? `${this.apiUrl}/list?${queryString}` : `${this.apiUrl}/list`;
    return this.http.get<PaginatedResponse<Document>>(url);
  }

  getDocumentById(id: string) {
    return this.http.get<Document>(`${this.apiUrl}/${id}`);
  }

  createDocument(req: CreateDocumentRequest) {
    return this.http.post<{ id: string }>(`${this.apiUrl}`, req);
  }

  updateDocument(id: string, req: UpdateDocumentRequest) {
    return this.http.put(`${this.apiUrl}/${id}`, req);
  }

  deleteDocument(id: string) {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }

  // ===== DOCUMENT VERSION OPERATIONS =====

  getDocumentVersions(documentId: string) {
    return this.http.get<DocumentVersion[]>(`${this.apiUrl}/${documentId}/versions`);
  }
}
