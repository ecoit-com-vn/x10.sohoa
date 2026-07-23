import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

export type LookupViewEntityType = 'DOSSIER' | 'DOCUMENT';

/**
 * Cộng dồn lượt tra cứu hồ sơ/tài liệu theo ngày — dùng cho báo cáo REPORT_DOSSIER_MOST_VIEWED.
 * Gọi "fire-and-forget": không chặn UI, lỗi chỉ log console, không hiển thị cho user.
 */
@Injectable({
  providedIn: 'root'
})
export class LookupTrackingService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  recordView(entityType: LookupViewEntityType, dossierId: string): void {
    if (!dossierId) return;

    this.http
      .post(`${this.config.apiGatewayUrl}/api/v1/notification/lookup-tracking`, {
        entityType,
        dossierId
      })
      .subscribe({
        error: (err) => console.error('Lỗi ghi log lượt tra cứu:', err)
      });
  }
}
