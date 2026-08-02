import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface OcrJobListItem {
  progressId: string;
  documentId: string;
  documentVersionId: string;
  documentName: string;
  documentTypeName?: string;
  dossierId?: string;
  dossierInfrastructureName?: string;
  dossierInfrastructureCode?: string;
  equipmentId?: string;
  equipmentName?: string;
  phase: string;
  status: string;
  progress: number;
  currentPage: number;
  totalPages: number;
  errorMessage?: string;
  createdDate: string;
  modifiedDate?: string;
}

export interface OcrJobListResponse {
  items: OcrJobListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface OcrJobListQueryParams {
  page: number;
  pageSize: number;
  status?: string;
  phase?: string;
  keyword?: string;
  fromDate?: string;
  toDate?: string;
}

/**
 * Màn hình giám sát job OCR/bóc tách toàn hệ thống — chỉ đọc. Hành động "Chạy lại" gọi lại
 * đúng các endpoint submit-digitization/rerun-extraction đã có sẵn (DossierDocumentService/
 * EquipmentService), không có logic retry riêng ở đây.
 */
@Injectable({ providedIn: 'root' })
export class OcrJobsMonitorService {
  private api = inject(ApiService);
  private readonly base = '/api/v1/ocr-jobs';

  getJobs(params: OcrJobListQueryParams): Observable<OcrJobListResponse> {
    const query: Record<string, string | number> = {
      page: params.page,
      pageSize: params.pageSize,
    };
    if (params.status) query['status'] = params.status;
    if (params.phase) query['phase'] = params.phase;
    if (params.keyword) query['keyword'] = params.keyword;
    if (params.fromDate) query['fromDate'] = params.fromDate;
    if (params.toDate) query['toDate'] = params.toDate;

    return this.api.get<OcrJobListResponse>(this.base, { params: query });
  }
}
