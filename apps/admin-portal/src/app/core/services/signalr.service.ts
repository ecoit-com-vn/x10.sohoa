import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private notificationSubject = new Subject<{ severity: string, summary: string, detail: string }>();
  public notifications$ = this.notificationSubject.asObservable();
  
  private hubConnection: signalR.HubConnection | undefined;

  constructor() { }

  public startConnection(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/hubs/notifications')
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('SignalR connected to /hubs/notifications'))
      .catch(err => console.error('Error while starting SignalR connection: ' + err));

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
