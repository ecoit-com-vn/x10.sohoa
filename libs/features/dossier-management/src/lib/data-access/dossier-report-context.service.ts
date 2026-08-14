import { Injectable } from '@angular/core';

/**
 * Ngữ cảnh mở trang hồ sơ từ báo cáo (?from=report).
 *
 * Được đặt giá trị tại DossierManagementComponent khi sync route, để các service
 * (detail, document-types, digitization…) tự động gắn tham số `from=report` vào
 * các API đọc (read-only) và qua đó được DynamicPermissionFilter bỏ qua check quyền
 * DOSSIER_VIEW chỉ khi request thực sự đến từ trang báo cáo.
 */
@Injectable({ providedIn: 'root' })
export class DossierReportContext {
  from: string | null = null;

  setFrom(value: string | null): void {
    this.from = value;
  }
}
