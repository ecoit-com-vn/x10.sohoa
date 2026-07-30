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
