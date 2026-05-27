import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { PopoverModule } from 'primeng/popover';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { SignalRService } from '../core/services/signalr.service';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [CommonModule, BadgeModule, ButtonModule, PopoverModule, ToastModule],
  providers: [MessageService],
  template: `
    <p-toast></p-toast>
    <div class="notification-container">
      <p-button 
        icon="pi pi-bell" 
        [rounded]="true" 
        [text]="true" 
        severity="secondary" 
        (onClick)="op.toggle($event)">
        <p-badge *ngIf="unreadCount > 0" [value]="unreadCount.toString()" severity="danger" styleClass="notification-badge"></p-badge>
      </p-button>

      <p-popover #op>
        <div class="p-3" style="width: 300px;">
          <h4 class="mb-3">Notifications</h4>
          <div *ngIf="notifications.length === 0" class="text-gray-500">
            No new notifications.
          </div>
          <div *ngFor="let notif of notifications" class="notification-item p-2 mb-2 border-round surface-100">
            <strong>{{ notif.summary }}</strong>
            <p class="m-0 text-sm">{{ notif.detail }}</p>
          </div>
          <div *ngIf="notifications.length > 0" class="mt-3 text-right">
            <p-button label="Clear All" [text]="true" size="small" (onClick)="clearNotifications()"></p-button>
          </div>
        </div>
      </p-popover>
    </div>
  `,
  styles: [`
    .notification-container {
      position: relative;
      display: inline-block;
    }
    ::ng-deep .notification-badge {
      position: absolute !important;
      top: -5px;
      right: -5px;
    }
    .notification-item {
      border-bottom: 1px solid #dee2e6;
    }
    .notification-item:last-child {
      border-bottom: none;
    }
  `]
})
export class NotificationBellComponent implements OnInit {
  notifications: any[] = [];
  unreadCount: number = 0;

  constructor(
    private signalRService: SignalRService,
    private messageService: MessageService
  ) {}

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
