import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private notificationSubject = new Subject<{ severity: string, summary: string, detail: string }>();
  public notifications$ = this.notificationSubject.asObservable();
  
  // Real implementation would use:
  // private hubConnection: signalR.HubConnection;

  constructor() { }

  public startConnection(): void {
    console.log('SignalR connected to /hubs/notifications (mocked)');
    
    // Simulate real-time notification
    setTimeout(() => {
      this.receiveMessage('success', 'Sync Completed', 'Equipment data has been synchronized.');
    }, 3000);
  }

  public receiveMessage(severity: string, summary: string, detail: string): void {
    this.notificationSubject.next({ severity, summary, detail });
  }
}
