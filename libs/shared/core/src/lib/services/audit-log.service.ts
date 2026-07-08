import { Injectable, inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface AuditLogItem {
  id: string;
  action: string;
  userName: string;
  timestamp: string;
  details?: string;
  resourceType?: string;
  resourceId?: string;
  resourceName?: string;
  serviceName?: string;
  statusCode?: number;
  httpMethod?: string;
  requestPath?: string;
}

export interface AuditLogListResponse {
  items: AuditLogItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditLogRecentResponse {
  logs?: AuditLogItem[];
  Logs?: AuditLogItem[];
}

export interface AuditLogDeleteResponse {
  message?: string;
  count?: number;
}

export interface AuditLogQueryParams {
  page?: number;
  pageSize?: number;
  keyword?: string;
  action?: string;
  resourceType?: string;
  serviceName?: string;
  userName?: string;
  fromDate?: string;
  toDate?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuditLogService {
  private api = inject(ApiService);

  private readonly base = '/api/v1/audit-logs';

  getAuditLogs(params: AuditLogQueryParams): Observable<AuditLogListResponse> {
    return this.api.get<AuditLogListResponse>(this.base, {
      params: this.toParams(params, true)
    });
  }

  getRecent(count = 5): Observable<AuditLogRecentResponse> {
    return this.api.get<AuditLogRecentResponse>(`${this.base}/recent`);
  }

  exportExcel(params: AuditLogQueryParams): Observable<HttpResponse<Blob>> {
    return this.api.getBlobResponse(`${this.base}/export`, {
      params: this.toParams(params, false)
    });
  }

  deleteByDateRange(fromDate: string, toDate: string): Observable<AuditLogDeleteResponse> {
    return this.api.delete<AuditLogDeleteResponse>(this.base, {
      params: { fromDate, toDate }
    });
  }

  deleteByIds(ids: string[]): Observable<AuditLogDeleteResponse> {
    return this.api.post<AuditLogDeleteResponse>(`${this.base}/bulk-delete`, { ids });
  }

  private toParams(
    params: AuditLogQueryParams,
    includePagination: boolean
  ): Record<string, string> {
    const result: Record<string, string> = {};

    if (includePagination) {
      result['page'] = String(params.page ?? 1);
      result['pageSize'] = String(params.pageSize ?? 10);
    }
    if (params.keyword) result['keyword'] = params.keyword;
    if (params.action) result['action'] = params.action;
    if (params.resourceType) result['resourceType'] = params.resourceType;
    if (params.serviceName) result['serviceName'] = params.serviceName;
    if (params.userName) result['userName'] = params.userName;
    if (params.fromDate) result['fromDate'] = params.fromDate;
    if (params.toDate) result['toDate'] = params.toDate;

    return result;
  }
}
