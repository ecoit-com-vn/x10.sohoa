import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { PopoverModule } from 'primeng/popover';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { catchError, forkJoin, of } from 'rxjs';
import {
  SignalRService,
  AuthService,
  NotificationApiService,
  NotificationItem
} from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [CommonModule, BadgeModule, ButtonModule, PopoverModule, ToastModule],
  providers: [MessageService],
  templateUrl: './notification-bell.component.html',
  styleUrl: './notification-bell.component.scss'
})
export class NotificationBellComponent implements OnInit {
  notifications = signal<NotificationItem[]>([]);
  dossierTitles = signal<Record<string, string>>({});
  unreadCount = computed(() => this.notifications().filter(n => !n.isRead).length);

  private signalRService = inject(SignalRService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private notificationApi = inject(NotificationApiService);

  ngOnInit() {
    this.loadNotifications();

    this.signalRService.startConnection();

    const userId = this.authService.getUserId();
    if (userId) {
      this.signalRService.joinUserGroup(userId);
    }

    this.signalRService.notifications$.subscribe(notif => {
      this.messageService.add({
        severity: notif.severity,
        summary: notif.summary,
        detail: notif.detail,
        life: 5000
      });
    });

    this.signalRService.notificationCreated$.subscribe(evt => {
      if (!evt.id) return;
      this.notifications.update(list => [
        {
          id: evt.id as string,
          notificationType: evt.notificationType,
          title: evt.title,
          body: evt.body,
          relatedEntityType: evt.relatedEntityType,
          relatedEntityId: evt.relatedEntityId,
          createdAt: new Date().toISOString(),
          isRead: false
        },
        ...list
      ]);
      this.loadDossierTitles([{
        id: evt.id as string,
        notificationType: evt.notificationType,
        title: evt.title,
        body: evt.body,
        relatedEntityType: evt.relatedEntityType,
        relatedEntityId: evt.relatedEntityId,
        createdAt: new Date().toISOString(),
        isRead: false
      }]);

      this.messageService.add({
        severity: 'info',
        summary: evt.title,
        detail: evt.body ?? '',
        life: 5000
      });
    });
  }

  private loadNotifications() {
    this.notificationApi
      .getNotifications(1, 20)
      .pipe(catchError(() => of({ items: [], totalCount: 0, unreadCount: 0, page: 1, pageSize: 20 })))
      .subscribe((res) => {
        this.notifications.set(res.items);
        this.loadDossierTitles(res.items);
      });
  }

  private loadDossierTitles(items: NotificationItem[]) {
    const ids = [...new Set(items
      .filter(item => item.relatedEntityType?.toUpperCase() === 'DOSSIER' && item.relatedEntityId)
      .map(item => item.relatedEntityId!.trim()))];
    if (ids.length === 0) {
      return;
    }

    forkJoin(ids.map(id => this.notificationApi.getDossierById(id).pipe(catchError(() => of(null))))).subscribe(itemsById => {
      const titles = { ...this.dossierTitles() };
      itemsById.forEach((item, index) => {
        const displayName = item?.title?.trim()
          || [item?.dossierTypeName, item?.infrastructureName, item?.dossierSetName]
            .filter(value => value?.trim())
            .map(value => value!.trim())
            .join(' - ');
        if (displayName) titles[ids[index].toLowerCase()] = displayName;
      });
      this.dossierTitles.set(titles);
    });
  }

  notificationTitle(notif: NotificationItem): string {
    const title = notif.title?.trim() || 'Thông báo';
    return title
      .replace(/^Quản lý hồ sơ:\s*/i, '')
      .replace(/^Thông báo:\s*/i, '')
      .trim() || 'Thông báo';
  }

  notificationBody(notif: NotificationItem): string {
    let body = (notif.body ?? '').trim();
    const entityId = notif.relatedEntityId?.trim();
    const dossierTitle = entityId ? this.dossierTitles()[entityId.toLowerCase()] : undefined;

    if (notif.relatedEntityType?.toUpperCase() === 'DOSSIER' && entityId) {
      body = body.split(entityId).join(dossierTitle || 'hồ sơ');
    }

    body = body
      .replace(/^Hồ sơ\s+/i, '')
      .replace(/\s+Vui lòng kiểm tra và thực hiện công việc được giao\.?$/i, '')
      .replace(/\s+/g, ' ')
      .trim();

    return body.length > 120 ? `${body.slice(0, 117).trimEnd()}...` : body;
  }

  onOpenNotification(notif: NotificationItem) {
    if (notif.isRead) return;
    this.notificationApi.markAsRead(notif.id).subscribe(() => {
      this.notifications.update(list =>
        list.map(n => (n.id === notif.id ? { ...n, isRead: true } : n))
      );
    });
  }

  deleteNotification(notif: NotificationItem, event: Event) {
    event.stopPropagation();
    this.notificationApi.deleteNotification(notif.id).subscribe(() => {
      this.notifications.update(list => list.filter(n => n.id !== notif.id));
    });
  }

  clearNotifications() {
    this.notificationApi.deleteAllNotifications().subscribe(() => {
      this.notifications.set([]);
    });
  }
}
