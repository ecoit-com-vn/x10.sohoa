import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config.token';
import { SUPPRESS_HTTP_ERROR_TOAST } from '../interceptors/http-error.interceptor';

export interface NotificationItem {
  id: string;
  notificationType: string;
  title: string;
  body?: string | null;
  relatedEntityType?: string | null;
  relatedEntityId?: string | null;
  createdAt: string;
  isRead: boolean;
  readAt?: string | null;
}

export interface NotificationListResponse {
  items: NotificationItem[];
  totalCount: number;
  unreadCount: number;
  page: number;
  pageSize: number;
}

export interface DossierLookupItem {
  id: string;
  title?: string | null;
  dossierTypeName?: string | null;
  infrastructureName?: string | null;
  dossierSetName?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationApiService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get base() {
    return `${this.config.apiGatewayUrl}/api/v1/notifications`;
  }

  getNotifications(page = 1, pageSize = 20, onlyUnread = false, context?: HttpContext): Observable<NotificationListResponse> {
    const defaultContext = context ?? new HttpContext().set(SUPPRESS_HTTP_ERROR_TOAST, true);
    return this.http.get<NotificationListResponse>(
      `${this.base}?page=${page}&pageSize=${pageSize}&onlyUnread=${onlyUnread}`,
      { context: defaultContext }
    );
  }

  getDossierLookup(page = 1, pageSize = 1000): Observable<{ items: DossierLookupItem[] }> {
    return this.http.get<{ items: DossierLookupItem[] }>(
      `${this.config.apiGatewayUrl}/api/v1/dossiers?page=${page}&pageSize=${pageSize}`
    );
  }

  getDossierById(id: string, context?: HttpContext): Observable<DossierLookupItem> {
    const defaultContext = context ?? new HttpContext().set(SUPPRESS_HTTP_ERROR_TOAST, true);
    return this.http.get<DossierLookupItem>(
      `${this.config.apiGatewayUrl}/api/v1/dossiers/${encodeURIComponent(id)}`,
      { context: defaultContext }
    );
  }

  markAsRead(id: string): Observable<any> {
    return this.http.put<any>(`${this.base}/${id}/read`, {});
  }

  markAllAsRead(): Observable<any> {
    return this.http.put<any>(`${this.base}/read-all`, {});
  }

  deleteNotification(id: string): Observable<any> {
    return this.http.delete<any>(`${this.base}/${id}`);
  }

  deleteAllNotifications(): Observable<any> {
    return this.http.delete<any>(`${this.base}`);
  }
}
