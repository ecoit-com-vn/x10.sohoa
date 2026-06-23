import { Injectable, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, firstValueFrom } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';
import { UPLOAD_SOURCE, UploadSource } from '../constants/upload-source.constants';

export interface ApiErrorBody {
  code?: string;
  message?: string;
}

/** Trích message từ response lỗi API: { code, message } */
export function extractApiErrorMessage(error: unknown, fallback = 'Upload tài liệu thất bại'): string {
  if (!error) return fallback;

  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    if (typeof body === 'string' && body.trim()) return body.trim();
    if (body && typeof body === 'object' && typeof body.message === 'string' && body.message.trim()) {
      return body.message.trim();
    }
  }

  if (typeof error === 'object' && error !== null) {
    const err = error as { error?: ApiErrorBody | string; message?: string };
    const body = err.error;
    if (typeof body === 'string' && body.trim()) return body.trim();
    if (body && typeof body === 'object' && typeof body.message === 'string' && body.message.trim()) {
      return body.message.trim();
    }
    if (typeof err.message === 'string' && err.message.trim() && !err.message.startsWith('Http failure response')) {
      return err.message.trim();
    }
  }

  if (error instanceof Error && error.message && !error.message.startsWith('Http failure response')) {
    return error.message;
  }

  return fallback;
}

export interface UploadProgress {
  uploadId?: string;
  progress: number;      // 0-100
  uploadedBytes: number;
  totalBytes: number;
  status: 'pending' | 'uploading' | 'completed' | 'error';
  error?: string;
}

export interface FileUploadResponse {
  documentVersionId: string;
  documentId: string;
  versionNumber: number;
  status: string;
}

export interface InitiateChunkedUploadResponse {
  uploadId: string;
  chunkSize: number;
  totalChunks: number;
}

export interface UploadChunkResponse {
  chunkNumber: number;
  eTag: string;
}

@Injectable({
  providedIn: 'root'
})
export class FileUploadService {
  private api = inject(ApiService);

  private readonly CHUNK_SIZE = 10 * 1024 * 1024; // 10MB
  private readonly DIRECT_UPLOAD_THRESHOLD = 10 * 1024 * 1024; // 10MB

  private get base() {
    return `/api/v1/files`;
  }

  /**
   * Upload file directly (≤10MB)
   */
  uploadFileDirect(
    file: File,
    folderId: string,
    uploadSource: UploadSource = UPLOAD_SOURCE.WEB,
  ): Observable<FileUploadResponse> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('folderId', folderId);
    formData.append('uploadSource', String(uploadSource));

    return this.api.post<FileUploadResponse>(
      `${this.base}/upload`,
      formData
    );
  }

  /**
   * Initiate chunked upload (>10MB)
   */
  initiateChunkedUpload(
    fileName: string,
    fileSize: number,
    folderId: string
  ): Observable<InitiateChunkedUploadResponse> {
    const body = {
      fileName,
      fileSize,
      folderId
    };

    return this.api.post<InitiateChunkedUploadResponse>(
      `${this.base}/initiate-chunked`,
      body
    );
  }

  /**
   * Upload single chunk
   */
  uploadChunk(
    uploadId: string,
    chunkNumber: number,
    chunkData: ArrayBuffer
  ): Observable<UploadChunkResponse> {
    const url = `${this.base}/${uploadId}/chunks/${chunkNumber}`;

    return this.api.put<UploadChunkResponse>(url, chunkData, {
      headers: { 'Content-Type': 'application/octet-stream' }
    });
  }

  /**
   * Complete chunked upload
   */
  completeChunkedUpload(
    uploadId: string,
    parts: Array<{ chunkNumber: number; eTag: string }>
  ): Observable<FileUploadResponse> {
    const url = `${this.base}/${uploadId}/complete`;
    const body = { uploadId, parts };

    return this.api.post<FileUploadResponse>(url, body);
  }

  /**
   * Main upload method - decides between direct and chunked
   */
  async uploadFile(
    file: File,
    folderId: string,
    onProgress?: (progress: UploadProgress) => void,
    uploadSource: UploadSource = UPLOAD_SOURCE.WEB,
  ): Promise<FileUploadResponse> {
    const uploadId = this.generateUploadId();
    const progress$ = new Subject<UploadProgress>();

    if (onProgress) {
      progress$.subscribe(onProgress);
    }

    try {
      // Emit: starting
      progress$.next({
        uploadId,
        progress: 0,
        uploadedBytes: 0,
        totalBytes: file.size,
        status: 'pending'
      });

      // Choose strategy
      if (file.size <= this.DIRECT_UPLOAD_THRESHOLD) {
        return await firstValueFrom(this.uploadFileDirect(file, folderId, uploadSource));
      } else {
        return await this.uploadChunked(file, folderId, uploadId, progress$, uploadSource);
      }
    } catch (error: unknown) {
      const errorMsg = extractApiErrorMessage(error);
      progress$.next({
        uploadId,
        progress: 0,
        uploadedBytes: 0,
        totalBytes: file.size,
        status: 'error',
        error: errorMsg
      });
      throw new Error(errorMsg);
    } finally {
      progress$.complete();
    }
  }

  /**
   * Chunked upload implementation
   */
  private async uploadChunked(
    file: File,
    folderId: string,
    uploadId: string,
    progress$: Subject<UploadProgress>,
    uploadSource: UploadSource = UPLOAD_SOURCE.WEB,
  ): Promise<FileUploadResponse> {
    // Phase 1: Initiate
    const initResponse = await firstValueFrom(
      this.initiateChunkedUpload(file.name, file.size, folderId)
    );

    const uploadSessionId = initResponse.uploadId;
    const chunkSize = initResponse.chunkSize;
    const totalChunks = initResponse.totalChunks;

    // Phase 2: Upload chunks sequentially
    const parts: Array<{ chunkNumber: number; eTag: string }> = [];
    let uploadedBytes = 0;

    for (let i = 1; i <= totalChunks; i++) {
      const start = (i - 1) * chunkSize;
      const end = Math.min(start + chunkSize, file.size);
      const chunk = file.slice(start, end);
      const chunkArrayBuffer = await this.fileToArrayBuffer(chunk);

      try {
        const response = await firstValueFrom(
          this.uploadChunk(uploadSessionId, i, chunkArrayBuffer)
        );

        parts.push({ chunkNumber: i, eTag: response.eTag });
        uploadedBytes += chunk.size;

        // Emit progress
        const percent = Math.round((uploadedBytes / file.size) * 100);
        progress$.next({
          uploadId: uploadSessionId,
          progress: percent,
          uploadedBytes,
          totalBytes: file.size,
          status: 'uploading'
        });
      } catch (error: unknown) {
        throw new Error(extractApiErrorMessage(error, `Không thể upload chunk ${i}`));
      }
    }

    // Phase 3: Complete
    const result = await firstValueFrom(
      this.completeChunkedUpload(uploadSessionId, parts)
    );

    progress$.next({
      uploadId: uploadSessionId,
      progress: 100,
      uploadedBytes: file.size,
      totalBytes: file.size,
      status: 'completed'
    });

    return result;
  }

  /**
   * Helper: Convert File to ArrayBuffer
   */
  private fileToArrayBuffer(file: Blob): Promise<ArrayBuffer> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as ArrayBuffer);
      reader.onerror = () => reject(reader.error);
      reader.readAsArrayBuffer(file);
    });
  }

  /**
   * Helper: Generate unique upload ID
   */
  private generateUploadId(): string {
    return `upload_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  /**
   * Get current upload progress (for monitoring)
   */
  getUploadProgress(): Observable<Map<string, UploadProgress>> {
    // Return empty observable - progress is tracked locally in component
    return new Observable(observer => {
      observer.next(new Map());
      observer.complete();
    });
  }
}
