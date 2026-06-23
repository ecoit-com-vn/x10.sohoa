import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';
import { DocumentVersionInfo } from './file-download.service';

export interface FileListResponse {
  items: DocumentVersionInfo[];
  totalCount: number;
}

export interface FileDeleteResponse {
  success: boolean;
  message?: string;
}

@Injectable({
  providedIn: 'root'
})
export class FileService {
  private api = inject(ApiService);

  private get base() {
    return `/api/v1/files`;
  }

  /**
   * Get files in folder with pagination
   */
  getFilesByFolder(
    folderId: string,
    page: number = 1,
    pageSize: number = 10
  ): Observable<FileListResponse> {
    return this.api.get<FileListResponse>(
      `${this.base}/folder/${folderId}`,
      {
        params: {
          page: page.toString(),
          pageSize: pageSize.toString()
        }
      }
    );
  }

  /**
   * Get file version by ID
   */
  getFileById(versionId: string): Observable<DocumentVersionInfo> {
    return this.api.get<DocumentVersionInfo>(
      `${this.base}/${versionId}`
    );
  }

  /**
   * Get all versions of a document
   */
  getDocumentVersions(documentId: string): Observable<DocumentVersionInfo[]> {
    return this.api.get<DocumentVersionInfo[]>(
      `${this.base}/document/${documentId}/versions`
    );
  }

  /**
   * Delete file version (soft delete)
   */
  deleteFileVersion(versionId: string): Observable<FileDeleteResponse> {
    return this.api.delete<FileDeleteResponse>(
      `${this.base}/${versionId}`
    );
  }

  /**
   * Delete all versions of a document (soft delete)
   */
  deleteDocument(documentId: string): Observable<FileDeleteResponse> {
    return this.api.delete<FileDeleteResponse>(
      `${this.base}/document/${documentId}`
    );
  }

  /**
   * Search files by keyword
   */
  searchFiles(
    folderId: string,
    keyword: string,
    page: number = 1,
    pageSize: number = 10
  ): Observable<FileListResponse> {
    return this.api.get<FileListResponse>(
      `${this.base}/folder/${folderId}/search`,
      {
        params: {
          q: keyword,
          page: page.toString(),
          pageSize: pageSize.toString()
        }
      }
    );
  }

  /**
   * Get upload config (allowed MIME types, max size, etc.)
   */
  getUploadConfig(): Observable<any> {
    return this.api.get<any>(`/api/v1/upload-config`);
  }
}
