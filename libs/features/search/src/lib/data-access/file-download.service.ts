import { Injectable, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiService, APP_CONFIG } from '@sohoa.frontend/shared/core';

export interface DownloadTokenResponse {
  token: string;
  expiresInSeconds: number;
}

export interface DocumentVersionInfo {
  id: string;
  documentId: string;
  versionNumber: number;
  fileName: string;
  fileSize: number;
  mimeType: string;
  status: string;
  createdDate: string;
  createdBy?: string;
  chunksCount: number;
}

export interface ApiErrorBody {
  code?: string;
  message?: string;
}

export function extractApiErrorMessage(error: unknown, fallback = 'Tải tài liệu thất bại'): string {
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

@Injectable({
  providedIn: 'root'
})
export class FileDownloadService {
  private api = inject(ApiService);
  private config = inject(APP_CONFIG);

  private get base() {
    return `/api/v1/files`;
  }

  getDownloadToken(versionId: string): Observable<DownloadTokenResponse> {
    return this.api.get<DownloadTokenResponse>(
      `${this.base}/${versionId}/download-url`
    );
  }

  getDownloadUrl(token: string): string {
    return `${this.config.apiGatewayUrl}${this.base}/download?token=${encodeURIComponent(token)}`;
  }

  async getDownloadUrlForVersion(versionId: string): Promise<string> {
    const tokenResponse = await firstValueFrom(this.getDownloadToken(versionId));
    if (!tokenResponse?.token) {
      throw new Error('Không thể tạo link tải file');
    }
    return this.getDownloadUrl(tokenResponse.token);
  }

  async downloadFile(versionId: string, fileName?: string): Promise<void> {
    try {
      const url = await this.getDownloadUrlForVersion(versionId);
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
      link.download = fileName || 'download';
      link.rel = 'noopener';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(objectUrl);
    } catch (error: unknown) {
      throw new Error(extractApiErrorMessage(error, 'Không thể tải file'));
    }
  }
}
