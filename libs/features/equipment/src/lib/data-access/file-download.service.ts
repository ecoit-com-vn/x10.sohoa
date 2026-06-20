import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiService, APP_CONFIG } from '@sohoa.frontend/shared/core';
import { extractApiErrorMessage } from './file-upload.service';

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
