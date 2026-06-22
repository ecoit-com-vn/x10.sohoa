import { Injectable, inject } from '@angular/core';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { APP_CONFIG } from '../config/app-config.token';
import { NgZone } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private config = inject(APP_CONFIG);
  private zone = inject(NgZone);
  private notificationSubject = new Subject<{ severity: string, summary: string, detail: string }>();
  public notifications$ = this.notificationSubject.asObservable();

  private hubConnection: signalR.HubConnection | undefined;

  constructor() { }

  public startConnection(): void {
    if (this.hubConnection && this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      console.log('SignalR connection is already active or connecting. State: ' + this.hubConnection.state);
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.config.apiGatewayUrl}/hubs/notifications`)
      .withAutomaticReconnect()
      .build();

    // Ép luồng bắt đầu kết nối phải nằm trong Angular Zone
    this.zone.run(() => {
      this.hubConnection!
        .start()
        .then(() => console.log('SignalR connected to /hubs/notifications'))
        .catch(err => console.error('Error while starting SignalR connection: ' + err));
    });
    this.addReceiveMessageListener();
  }

  private addReceiveMessageListener(): void {
    if (this.hubConnection) {
      this.hubConnection.on('ReceiveNotification', (severity: string, summary: string, detail: string) => {
        this.receiveMessage(severity, summary, detail);
      });

      this.hubConnection.on('ReceiveMessage', (message: string) => {
        this.receiveMessage('info', 'System Notification', message);
      });
    }
  }

  public receiveMessage(severity: string, summary: string, detail: string): void {
    this.notificationSubject.next({ severity, summary, detail });
  }
}
