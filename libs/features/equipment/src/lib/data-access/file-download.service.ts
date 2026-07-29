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

  private get equipmentBase() {
    return `/api/v1/equipment`;
  }

  getDownloadToken(versionId: string): Observable<DownloadTokenResponse> {
    return this.api.get<DownloadTokenResponse>(
      `${this.base}/${versionId}/download-url`
    );
  }

  /** Download token cho tài liệu lý lịch thuộc hồ sơ liên quan thiết bị. */
  getEquipmentProfileDownloadToken(equipmentId: string, versionId: string): Observable<DownloadTokenResponse> {
    return this.api.get<DownloadTokenResponse>(
      `${this.equipmentBase}/${equipmentId}/documents/${versionId}/download-url`
    );
  }

  getDownloadUrl(token: string): string {
    return `${this.config.apiGatewayUrl}${this.base}/download?token=${encodeURIComponent(token)}`;
  }

  async getDownloadUrlForVersion(versionId: string, equipmentId?: string): Promise<string> {
    const tokenResponse = await firstValueFrom(
      equipmentId
        ? this.getEquipmentProfileDownloadToken(equipmentId, versionId)
        : this.getDownloadToken(versionId)
    );
    if (!tokenResponse?.token) {
      throw new Error('Không thể tạo link tải file');
    }
    return this.getDownloadUrl(tokenResponse.token);
  }

  /** Fetch file qua one-time token, trả blob URL để preview inline (không gắn thẳng token URL vào iframe). */
  async getPreviewBlobUrl(versionId: string, equipmentId?: string): Promise<string> {
    const url = await this.getDownloadUrlForVersion(versionId, equipmentId);
    const response = await fetch(url, { method: 'GET', credentials: 'include' });
    if (!response.ok) {
      let message = 'Không thể tải file xem trước';
      try {
        const body = await response.json();
        message = body?.message || message;
      } catch {
        // ignore parse errors
      }
      throw new Error(message);
    }
    const blob = await response.blob();
    return window.URL.createObjectURL(blob);
  }

  revokePreviewBlobUrl(objectUrl: string): void {
    window.URL.revokeObjectURL(objectUrl);
  }

  async downloadFile(versionId: string, fileName?: string, equipmentId?: string): Promise<void> {
    try {
      const url = await this.getDownloadUrlForVersion(versionId, equipmentId);
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
