import { Injectable, inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface AuditLogItem {
  id: string;
  action: string;
  actionName?: string;
  userName: string;
  timestamp: string;
  details?: string;
  resourceType?: string;
  resourceTypeName?: string;
  resourceId?: string;
  resourceName?: string;
  serviceName?: string;
  statusCode?: number;
  status?: string;
  correlationId?: string;
  httpMethod?: string;
  requestPath?: string;
  logGroup?: string;
  actorUnitId?: string;
  actorUnitName?: string;
  actorFullName?: string;
  fullName?: string;
}

export interface AuditLogLookupItem {
  code: string;
  label: string;
}

export interface AuditLogLookups {
  actions: AuditLogLookupItem[];
  resourceTypes: AuditLogLookupItem[];
  logGroups: AuditLogLookupItem[];
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

export interface AuditLogRetentionIndex {
  indexName: string;
  logDate: string;
  documentCount: number;
  sizeBytes: number;
  estimatedDeleteAtUtc: string;
  remainingDays: number;
  status: string;
}

export interface AuditLogRetentionStatusResponse {
  retentionDays: number;
  nextCleanupAtUtc: string;
  totalIndices: number;
  totalDocuments: number;
  totalSizeBytes: number;
  items: AuditLogRetentionIndex[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface AuditLogRetentionIndexDeleteResponse {
  message?: string;
  deletedDocuments?: number;
  deletedIndices?: number;
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
  logGroup?: string;
  unitIds?: string[];
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

  getRetentionStatus(pageNumber: number, pageSize: number): Observable<AuditLogRetentionStatusResponse> {
    return this.api.get<AuditLogRetentionStatusResponse>(`${this.base}/retention-status`, {
      params: { pageNumber: String(pageNumber), pageSize: String(pageSize) }
    });
  }

  deleteRetentionIndex(logDate: string): Observable<AuditLogRetentionIndexDeleteResponse> {
    return this.api.delete<AuditLogRetentionIndexDeleteResponse>(`${this.base}/retention-index/${encodeURIComponent(logDate)}`);
  }

  deleteAllRetentionIndices(): Observable<AuditLogRetentionIndexDeleteResponse> {
    return this.api.delete<AuditLogRetentionIndexDeleteResponse>(`${this.base}/retention-indices`);
  }

  getLookups(logGroup?: string): Observable<AuditLogLookups> {
    return this.api.get<AuditLogLookups>(`${this.base}/lookups`, {
      params: logGroup ? { logGroup } : {}
    });
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
  ): Record<string, string | string[]> {
    const result: Record<string, string | string[]> = {};

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
    if (params.logGroup) result['logGroup'] = params.logGroup;
    if (params.unitIds?.length) result['unitIds'] = params.unitIds;

    return result;
  }
}
