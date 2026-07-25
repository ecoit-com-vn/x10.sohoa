import { Injectable, inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';
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
  private api = inject(ApiService);

  private get base(): string {
    return '/api/v1/documents';
  }

  private buildDocumentListParams(filter: DocumentFilter): Record<string, string> {
    const params: Record<string, string> = {};
    if (filter.folderId) params['folderId'] = filter.folderId;
    if (filter.keyword) params['keyword'] = filter.keyword;
    if (filter.createdBy) params['creator'] = filter.createdBy;
    if (filter.startDate) params['startDate'] = filter.startDate;
    if (filter.endDate) params['endDate'] = filter.endDate;
    if (filter.sortField) params['sortField'] = filter.sortField;
    if (filter.sortOrder) params['sortOrder'] = filter.sortOrder;
    if (filter.page) params['page'] = filter.page.toString();
    if (filter.pageSize) params['pageSize'] = filter.pageSize.toString();
    return params;
  }

  // ===== FOLDER OPERATIONS =====

  getFolderTree(): Observable<FolderNode[]> {
    return this.api.get<FolderNode[]>(`${this.base}/folders/tree`);
  }

  getFolderById(id: string): Observable<FolderNode> {
    return this.api.get<FolderNode>(`${this.base}/folders/${id}`);
  }

  createFolder(req: CreateFolderRequest): Observable<{ id: string }> {
    return this.api.post<{ id: string }>(`${this.base}/folders`, req);
  }

  updateFolder(id: string, req: UpdateFolderRequest): Observable<void> {
    return this.api.put<void>(`${this.base}/folders/${id}`, req);
  }

  deleteFolder(id: string): Observable<void> {
    return this.api.delete<void>(`${this.base}/folders/${id}`);
  }

  downloadFolderZip(folderId: string): Observable<HttpResponse<Blob>> {
    return this.api.getBlobResponse(`${this.base}/folders/${folderId}/download-zip`);
  }

  // ===== DOCUMENT OPERATIONS =====

  getDocuments(filter: DocumentFilter): Observable<PaginatedResponse<Document>> {
    return this.api.get<PaginatedResponse<Document>>(`${this.base}/list`, {
      params: this.buildDocumentListParams(filter),
    });
  }

  getDocumentById(id: string): Observable<Document> {
    return this.api.get<Document>(`${this.base}/${id}`);
  }

  createDocument(req: CreateDocumentRequest): Observable<{ id: string }> {
    return this.api.post<{ id: string }>(this.base, req);
  }

  updateDocument(id: string, req: UpdateDocumentRequest): Observable<void> {
    return this.api.put<void>(`${this.base}/${id}`, req);
  }

  deleteDocument(id: string): Observable<void> {
    return this.api.delete<void>(`${this.base}/${id}`);
  }

  // ===== DOCUMENT VERSION OPERATIONS =====

  getDocumentVersions(documentId: string): Observable<DocumentVersion[]> {
    return this.api.get<DocumentVersion[]>(`${this.base}/${documentId}/versions`);
  }

  uploadNewVersion(
    documentId: string,
    file: File,
    folderId: string,
    uploadSource = 3
  ): Observable<unknown> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('folderId', folderId);
    formData.append('uploadSource', String(uploadSource));
    return this.api.post<unknown>(`${this.base}/${documentId}/new-versions`, formData);
  }

  initiateNewVersionChunkedUpload(
    documentId: string,
    fileName: string,
    fileSize: number
  ): Observable<{ uploadId: string; chunkSize: number; totalChunks: number }> {
    return this.api.post<{ uploadId: string; chunkSize: number; totalChunks: number }>(
      `${this.base}/${documentId}/new-versions/initiate-chunked`,
      { fileName, fileSize }
    );
  }

  uploadNewVersionChunk(
    documentId: string,
    uploadId: string,
    chunkNumber: number,
    chunk: Blob
  ): Observable<{ chunkNumber: number; eTag: string }> {
    return this.api.put<{ chunkNumber: number; eTag: string }>(
      `${this.base}/${documentId}/new-versions/upload/chunked/${uploadId}/chunks/${chunkNumber}`,
      chunk,
      {
        headers: { 'Content-Type': 'application/octet-stream' }
      }
    );
  }

  completeNewVersionChunkedUpload(
    documentId: string,
    uploadId: string,
    parts: Array<{ chunkNumber: number; eTag: string }>
  ): Observable<unknown> {
    return this.api.post<unknown>(
      `${this.base}/${documentId}/new-versions/upload/chunked/${uploadId}/complete`,
      { uploadId, parts }
    );
  }

  abortNewVersionChunkedUpload(documentId: string, uploadId: string): Observable<void> {
    return this.api.delete<void>(
      `${this.base}/${documentId}/new-versions/upload/chunked/${uploadId}/abort`
    );
  }

  rollbackDocumentVersion(versionId: string): Observable<void> {
    return this.api.post<void>(`${this.base}/versions/${versionId}/rollback`, {});
  }

  deleteDocumentVersion(versionId: string): Observable<void> {
    return this.api.delete<void>(`${this.base}/versions/${versionId}`);
  }
}
