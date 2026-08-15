import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config.token';

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

  getNotifications(page = 1, pageSize = 20, onlyUnread = false): Observable<NotificationListResponse> {
    return this.http.get<NotificationListResponse>(
      `${this.base}?page=${page}&pageSize=${pageSize}&onlyUnread=${onlyUnread}`
    );
  }

  getDossierLookup(page = 1, pageSize = 1000): Observable<{ items: DossierLookupItem[] }> {
    return this.http.get<{ items: DossierLookupItem[] }>(
      `${this.config.apiGatewayUrl}/api/v1/dossiers?page=${page}&pageSize=${pageSize}`
    );
  }

  getDossierById(id: string): Observable<DossierLookupItem> {
    return this.http.get<DossierLookupItem>(
      `${this.config.apiGatewayUrl}/api/v1/dossiers/${encodeURIComponent(id)}`
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
