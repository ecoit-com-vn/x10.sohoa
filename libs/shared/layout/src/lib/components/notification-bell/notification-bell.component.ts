import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { PopoverModule } from 'primeng/popover';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { SignalRService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [CommonModule, BadgeModule, ButtonModule, PopoverModule, ToastModule],
  providers: [MessageService],
  templateUrl: './notification-bell.component.html',
  styleUrl: './notification-bell.component.scss'
})
export class NotificationBellComponent implements OnInit {
  notifications: any[] = [];
  unreadCount = 0;

  private signalRService = inject(SignalRService);
  private messageService = inject(MessageService);

  ngOnInit() {
    this.signalRService.startConnection();
    
    this.signalRService.notifications$.subscribe(notif => {
      this.notifications.unshift(notif);
      this.unreadCount++;
      
      this.messageService.add({
        severity: notif.severity,
        summary: notif.summary,
        detail: notif.detail,
        life: 5000
      });
    });
  }

  clearNotifications() {
    this.notifications = [];
    this.unreadCount = 0;
  }
}
