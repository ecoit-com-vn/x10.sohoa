import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { PopoverModule } from 'primeng/popover';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
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

      this.messageService.add({
        severity: 'info',
        summary: evt.title,
        detail: evt.body ?? '',
        life: 5000
      });
    });
  }

  private loadNotifications() {
    this.notificationApi.getNotifications(1, 20).subscribe(res => {
      this.notifications.set(res.items);
    });
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
