import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom, map, catchError, throwError, of } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';
import {
  extractApiErrorMessage,
  FileUploadResponse,
  InitiateChunkedUploadResponse,
  UploadProgress,
} from '@sohoa.frontend/features/equipment';

export interface DocumentOcrProgress {
  id: string;
  documentId?: string;
  documentVersionId: string;
  action?: string;
  phase: string;
  currentPage: number;
  totalPages: number;
  progress: number;
  status: string;
  processOption?: string;
  createdDate?: string;
  modifiedDate?: string;
}

export interface DocumentExtractionResultSummary {
  id: string;
  documentVersionId: string;
  status: string;
}

export interface DossierDocumentItem {
  id: string;
  name: string;
  folderId?: string | null;
  dossierId?: string | null;
  createdBy?: string;
  createdByName?: string;
  createdDate?: string;
  fileSize?: number;
  mimeType?: string;
  latestVersionId?: string | null;
  documentTypeId?: string | null;
  documentTypeName?: string | null;
  ocrProgress?: DocumentOcrProgress | null;
  extractionResult?: DocumentExtractionResultSummary | null;
}

export interface DossierDocumentListResponse {
  items: DossierDocumentItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

interface DownloadTokenResponse {
  token: string;
  expiresInSeconds: number;
}

export interface MoveFromFolderResponse {
  success: boolean;
  movedCount: number;
  movedNames: string[];
  movedDocuments?: MovedDossierDocumentItem[];
}

export interface MovedDossierDocumentItem {
  documentId: string;
  versionId: string;
  name: string;
}

export interface DocumentTypeLookupItem {
  id: string;
  name: string;
  code?: string;
  formId?: string | null;
  formName?: string | null;
  isActive?: boolean;
}

interface UploadChunkResponse {
  chunkNumber: number;
  eTag: string;
}

export type DigitizationProcessOption = 'OcrAndExtract' | 'ExtractOnly';

export interface SubmitDigitizationRequest {
  processOption?: DigitizationProcessOption;
  extractPrompt?: string;
}

export interface DocumentExtractionResult {
  id: string;
  documentId: string;
  documentVersionId: string;
  status: string;
  resultJson?: string | null;
  resultFilePath?: string | null;
  mergedDataJson?: string | null;
  createdDate?: string;
  modifiedDate?: string;
}

@Injectable({
  providedIn: 'root',
})
export class DossierDocumentService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private readonly CHUNK_SIZE = 10 * 1024 * 1024;
  private readonly DIRECT_UPLOAD_THRESHOLD = 10 * 1024 * 1024;

  private dossierBase(dossierId: string, lookupMode = false): string {
    if (lookupMode) {
      return `${this.config.apiGatewayUrl}/api/v1/dossiers-by-equipment/${dossierId}/documents`;
    }
    return `${this.config.apiGatewayUrl}/api/v1/dossiers/${dossierId}/documents`;
  }

  getDocuments(
    dossierId: string,
    filter: { keyword?: string; page: number; pageSize: number },
    lookupMode = false
  ): Observable<DossierDocumentListResponse> {
    let params = new HttpParams()
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.keyword?.trim()) {
      params = params.set('keyword', filter.keyword.trim());
    }

    return this.http.get<DossierDocumentListResponse>(this.dossierBase(dossierId, lookupMode), { params }).pipe(
      map((res) => ({
        ...res,
        items: (res.items ?? []).map((item) => normalizeDossierDocumentItem(item)),
      }))
    );
  }

  deleteDocument(dossierId: string, documentId: string): Observable<void> {
    return this.http.delete<void>(`${this.dossierBase(dossierId)}/${documentId}`);
  }

  moveFromFolder(
    dossierId: string,
    documentIds: string[],
    documentTypeId: string
  ): Observable<MoveFromFolderResponse> {
    return this.http.post<MoveFromFolderResponse>(`${this.dossierBase(dossierId)}/move-from-folder`, {
      documentIds,
      documentTypeId,
    }).pipe(
      map((res) => ({
        ...res,
        movedDocuments: normalizeMovedDocuments(res.movedDocuments ?? res),
      }))
    );
  }

  lookupDocumentTypes(keyword?: string): Observable<DocumentTypeLookupItem[]> {
    let params = new HttpParams();
    if (keyword?.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    return this.http.get<unknown[]>(`${this.config.apiGatewayUrl}/api/catalog/document-type/lookup`, { params }).pipe(
      map((items) => (items ?? []).map((item) => normalizeDocumentTypeLookup(item)))
    );
  }

  uploadFileDirect(
    dossierId: string,
    file: File,
    documentTypeId: string,
    uploadSource = 3
  ): Observable<FileUploadResponse> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('documentTypeId', documentTypeId);
    formData.append('uploadSource', String(uploadSource));
    return this.http.post<FileUploadResponse>(`${this.dossierBase(dossierId)}/upload`, formData);
  }

  initiateChunkedUpload(
    dossierId: string,
    fileName: string,
    fileSize: number
  ): Observable<InitiateChunkedUploadResponse> {
    return this.http.post<InitiateChunkedUploadResponse>(
      `${this.dossierBase(dossierId)}/upload/chunked/initiate`,
      { fileName, fileSize }
    );
  }

  uploadChunk(
    dossierId: string,
    uploadId: string,
    chunkNumber: number,
    chunkData: ArrayBuffer
  ): Observable<UploadChunkResponse> {
    return this.http.put<UploadChunkResponse>(
      `${this.dossierBase(dossierId)}/upload/chunked/${uploadId}/chunks/${chunkNumber}`,
      chunkData,
      { headers: { 'Content-Type': 'application/octet-stream' } }
    );
  }

  completeChunkedUpload(
    dossierId: string,
    uploadId: string,
    parts: Array<{ chunkNumber: number; eTag: string }>,
    documentTypeId: string
  ): Observable<FileUploadResponse> {
    return this.http.post<FileUploadResponse>(
      `${this.dossierBase(dossierId)}/upload/chunked/${uploadId}/complete`,
      { uploadId, parts, documentTypeId }
    );
  }

  async getPreviewBlobUrl(dossierId: string, versionId: string, lookupMode = false): Promise<string> {
    const tokenResponse = await firstValueFrom(this.getDownloadToken(dossierId, versionId, lookupMode));
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

  revokePreviewBlobUrl(objectUrl: string): void {
    window.URL.revokeObjectURL(objectUrl);
  }

  async uploadFile(
    dossierId: string,
    file: File,
    documentTypeId: string,
    uploadSource = 3,
    onProgress?: (progress: UploadProgress) => void
  ): Promise<FileUploadResponse> {
    const uploadId = `upload_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

    try {
      onProgress?.({
        uploadId,
        progress: 0,
        uploadedBytes: 0,
        totalBytes: file.size,
        status: 'pending',
      });

      if (file.size <= this.DIRECT_UPLOAD_THRESHOLD) {
        const result = await firstValueFrom(
          this.uploadFileDirect(dossierId, file, documentTypeId, uploadSource)
        );
        onProgress?.({
          uploadId,
          progress: 100,
          uploadedBytes: file.size,
          totalBytes: file.size,
          status: 'completed',
        });
        return result;
      }

      return await this.uploadChunked(dossierId, file, uploadId, documentTypeId, uploadSource, onProgress);
    } catch (error: unknown) {
      const errorMsg = extractApiErrorMessage(error);
      onProgress?.({
        uploadId,
        progress: 0,
        uploadedBytes: 0,
        totalBytes: file.size,
        status: 'error',
        error: errorMsg,
      });
      throw new Error(errorMsg);
    }
  }

  private async uploadChunked(
    dossierId: string,
    file: File,
    uploadId: string,
    documentTypeId: string,
    uploadSource: number,
    onProgress?: (progress: UploadProgress) => void
  ): Promise<FileUploadResponse> {
    const initResponse = await firstValueFrom(
      this.initiateChunkedUpload(dossierId, file.name, file.size)
    );

    const uploadSessionId = initResponse.uploadId;
    const chunkSize = initResponse.chunkSize;
    const totalChunks = initResponse.totalChunks;
    const parts: Array<{ chunkNumber: number; eTag: string }> = [];
    let uploadedBytes = 0;

    for (let i = 1; i <= totalChunks; i++) {
      const start = (i - 1) * chunkSize;
      const end = Math.min(start + chunkSize, file.size);
      const chunk = file.slice(start, end);
      const chunkArrayBuffer = await this.fileToArrayBuffer(chunk);

      const response = await firstValueFrom(
        this.uploadChunk(dossierId, uploadSessionId, i, chunkArrayBuffer)
      );

      parts.push({ chunkNumber: i, eTag: response.eTag });
      uploadedBytes += chunk.size;

      onProgress?.({
        uploadId: uploadSessionId,
        progress: Math.round((uploadedBytes / file.size) * 100),
        uploadedBytes,
        totalBytes: file.size,
        status: 'uploading',
      });
    }

    const result = await firstValueFrom(
      this.completeChunkedUpload(dossierId, uploadSessionId, parts, documentTypeId)
    );

    onProgress?.({
      uploadId: uploadSessionId,
      progress: 100,
      uploadedBytes: file.size,
      totalBytes: file.size,
      status: 'completed',
    });

    return result;
  }

  private fileToArrayBuffer(file: Blob): Promise<ArrayBuffer> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as ArrayBuffer);
      reader.onerror = () => reject(reader.error);
      reader.readAsArrayBuffer(file);
    });
  }

  private getDownloadToken(dossierId: string, versionId: string, lookupMode = false): Observable<DownloadTokenResponse> {
    return this.http.get<DownloadTokenResponse>(
      `${this.dossierBase(dossierId, lookupMode)}/${versionId}/download-url`
    );
  }

  private getStreamUrl(token: string): string {
    return `${this.config.apiGatewayUrl}/api/v1/files/download?token=${encodeURIComponent(token)}`;
  }

  async downloadFile(dossierId: string, versionId: string, fileName?: string, lookupMode = false): Promise<void> {
    const tokenResponse = await firstValueFrom(this.getDownloadToken(dossierId, versionId, lookupMode));
    if (!tokenResponse?.token) {
      throw new Error('Không thể tạo link tải file');
    }

    const url = this.getStreamUrl(tokenResponse.token);
    const response = await fetch(url, { method: 'GET', credentials: 'include' });

    if (!response.ok) {
      let message = 'Không thể tải file';
      try {
        const body = await response.json();
        message = body?.message || message;
      } catch {
        // ignore
      }
      throw new Error(message);
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

  submitDigitization(
    dossierId: string,
    versionId: string,
    body: SubmitDigitizationRequest = {}
  ): Observable<DocumentOcrProgress> {
    return this.http.post<DocumentOcrProgress>(
      `${this.dossierBase(dossierId)}/${versionId}/digitization`,
      {
        processOption: body.processOption ?? 'OcrAndExtract',
        extractPrompt: body.extractPrompt,
      }
    );
  }

  retryDigitization(
    dossierId: string,
    versionId: string,
    processOption: DigitizationProcessOption = 'OcrAndExtract'
  ): Observable<DocumentOcrProgress> {
    return this.submitDigitization(dossierId, versionId, { processOption });
  }

  reExtractDigitization(dossierId: string, versionId: string): Observable<DocumentOcrProgress> {
    return this.http.post<DocumentOcrProgress>(
      `${this.dossierBase(dossierId)}/${versionId}/digitization/re-extract`,
      {}
    );
  }

  getDigitizationProgress(dossierId: string, versionId: string, lookupMode = false): Observable<DocumentOcrProgress> {
    return this.http.get<DocumentOcrProgress>(
      `${this.dossierBase(dossierId, lookupMode)}/${versionId}/digitization/progress`
    );
  }

  getDigitizationResult(dossierId: string, versionId: string, lookupMode = false): Observable<DocumentExtractionResult> {
    return this.http.get<DocumentExtractionResult>(
      `${this.dossierBase(dossierId, lookupMode)}/${versionId}/digitization/result`
    );
  }

  /** 404 = chưa có kết quả bóc tách (null); lỗi khác ném lại để caller hiển thị message API. */
  getDigitizationResultOrNull(dossierId: string, versionId: string, lookupMode = false): Observable<DocumentExtractionResult | null> {
    return this.getDigitizationResult(dossierId, versionId, lookupMode).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          return of(null);
        }
        return throwError(() => error);
      })
    );
  }

  saveDocumentExtractionData(
    dossierId: string,
    versionId: string,
    mergedDataJson: string
  ): Observable<DocumentExtractionResult> {
    return this.http.put<DocumentExtractionResult>(
      `${this.dossierBase(dossierId)}/${versionId}/digitization/result`,
      { mergedDataJson }
    );
  }

  digitizationResultErrorMessage(error: unknown, fallback = 'Không tải được kết quả bóc tách'): string {
    return extractApiErrorMessage(error, fallback);
  }
}

function readField<T>(obj: Record<string, unknown>, ...keys: string[]): T | undefined {
  for (const key of keys) {
    if (obj[key] !== undefined && obj[key] !== null) {
      return obj[key] as T;
    }
  }
  return undefined;
}

function normalizeOcrProgress(raw: unknown): DocumentOcrProgress | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const id = readField<string>(o, 'id', 'Id');
  if (!id) return null;
  return {
    id: String(id),
    documentId: readField<string>(o, 'documentId', 'DocumentId'),
    documentVersionId: String(readField<string>(o, 'documentVersionId', 'DocumentVersionId') ?? ''),
    action: readField<string>(o, 'action', 'Action'),
    phase: String(readField<string>(o, 'phase', 'Phase') ?? 'ocr'),
    currentPage: Number(readField<number>(o, 'currentPage', 'CurrentPage') ?? 0),
    totalPages: Number(readField<number>(o, 'totalPages', 'TotalPages') ?? 0),
    progress: Number(readField<number>(o, 'progress', 'Progress') ?? 0),
    status: String(readField<string>(o, 'status', 'Status') ?? ''),
    processOption: readField<string>(o, 'processOption', 'ProcessOption'),
    createdDate: readField<string>(o, 'createdDate', 'CreatedDate'),
    modifiedDate: readField<string>(o, 'modifiedDate', 'ModifiedDate'),
  };
}

function normalizeExtractionSummary(raw: unknown): DocumentExtractionResultSummary | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const id = readField<string>(o, 'id', 'Id');
  if (!id) return null;
  return {
    id: String(id),
    documentVersionId: String(readField<string>(o, 'documentVersionId', 'DocumentVersionId') ?? ''),
    status: String(readField<string>(o, 'status', 'Status') ?? ''),
  };
}

function normalizeDocumentTypeLookup(raw: unknown): DocumentTypeLookupItem {
  const o = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  return {
    id: String(readField<string>(o, 'id', 'Id') ?? ''),
    name: String(readField<string>(o, 'name', 'Name') ?? ''),
    code: readField<string>(o, 'code', 'Code'),
    formId: readField<string>(o, 'formId', 'FormId') ?? null,
    formName: readField<string>(o, 'formName', 'FormName') ?? null,
    isActive: readField<boolean>(o, 'isActive', 'IsActive'),
  };
}

function normalizeMovedDocuments(raw: unknown): MovedDossierDocumentItem[] {
  const list = Array.isArray(raw)
    ? raw
    : readField<unknown[]>(raw as Record<string, unknown>, 'movedDocuments', 'MovedDocuments') ?? [];
  return list.map((item) => {
    const o = (item && typeof item === 'object' ? item : {}) as Record<string, unknown>;
    return {
      documentId: String(readField<string>(o, 'documentId', 'DocumentId') ?? ''),
      versionId: String(readField<string>(o, 'versionId', 'VersionId') ?? ''),
      name: String(readField<string>(o, 'name', 'Name') ?? ''),
    };
  });
}
function normalizeDossierDocumentItem(raw: unknown): DossierDocumentItem {
  const o = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  return {
    id: String(readField<string>(o, 'id', 'Id') ?? ''),
    name: String(readField<string>(o, 'name', 'Name') ?? ''),
    folderId: readField<string>(o, 'folderId', 'FolderId') ?? null,
    dossierId: readField<string>(o, 'dossierId', 'DossierId') ?? null,
    createdBy: readField<string>(o, 'createdBy', 'CreatedBy'),
    createdByName: readField<string>(o, 'createdByName', 'CreatedByName'),
    createdDate: readField<string>(o, 'createdDate', 'CreatedDate'),
    fileSize: readField<number>(o, 'fileSize', 'FileSize'),
    mimeType: readField<string>(o, 'mimeType', 'MimeType'),
    latestVersionId: readField<string>(o, 'latestVersionId', 'LatestVersionId') ?? null,
    documentTypeId: readField<string>(o, 'documentTypeId', 'DocumentTypeId') ?? null,
    documentTypeName: readField<string>(o, 'documentTypeName', 'DocumentTypeName') ?? null,
    ocrProgress: normalizeOcrProgress(readField(o, 'ocrProgress', 'OcrProgress')),
    extractionResult: normalizeExtractionSummary(readField(o, 'extractionResult', 'ExtractionResult')),
  };
}
