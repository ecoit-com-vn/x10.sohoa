import { Injectable, inject, PLATFORM_ID, NgZone } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { APP_CONFIG } from '../config/app-config.token';

export interface DigitizationProgressEvent {
  dossierId: string;
  documentId: string;
  documentVersionId: string;
  phase: string;
  status: string;
  progress: number;
  currentPage: number;
  totalPages: number;
  extractionStatus?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private config = inject(APP_CONFIG);
  private zone = inject(NgZone);
  private platformId = inject(PLATFORM_ID);
  private notificationSubject = new Subject<{ severity: string, summary: string, detail: string }>();
  public notifications$ = this.notificationSubject.asObservable();

  private digitizationProgressSubject = new Subject<DigitizationProgressEvent>();
  public digitizationProgress$ = this.digitizationProgressSubject.asObservable();

  private hubConnection: signalR.HubConnection | undefined;
  private startPromise: Promise<void> | null = null;
  private listenersRegistered = false;
  private joinedDossierGroups = new Set<string>();

  private get isBrowser(): boolean {
    return isPlatformBrowser(this.platformId);
  }

  public isConnected(): boolean {
    return this.isBrowser && this.hubConnection?.state === signalR.HubConnectionState.Connected;
  }

  public ensureConnection(): Promise<void> {
    if (!this.isBrowser) {
      return Promise.resolve();
    }
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return Promise.resolve();
    }
    if (this.startPromise) {
      return this.startPromise;
    }
    this.startConnection();
    return this.startPromise ?? Promise.resolve();
  }

  public startConnection(): void {
    if (!this.isBrowser) {
      return;
    }
    if (this.hubConnection && this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.config.apiGatewayUrl}/hubs/notifications`)
      .withAutomaticReconnect()
      .build();

    this.registerListeners();

    this.startPromise = this.zone.run(async () => {
      try {
        await this.hubConnection!.start();
        console.log('SignalR connected to /hubs/notifications');
      } catch (err) {
        console.error('Error while starting SignalR connection: ' + err);
        this.hubConnection = undefined;
        this.listenersRegistered = false;
      } finally {
        this.startPromise = null;
      }
    });
  }

  private registerListeners(): void {
    if (!this.hubConnection || this.listenersRegistered) return;
    this.listenersRegistered = true;

    this.hubConnection.on('ReceiveNotification', (severity: string, summary: string, detail: string) => {
      this.receiveMessage(severity, summary, detail);
    });

    this.hubConnection.on('ReceiveMessage', (message: string) => {
      this.receiveMessage('info', 'System Notification', message);
    });

    this.hubConnection.on('ReceiveDigitizationProgress', (payload: unknown) => {
      const event = this.normalizeDigitizationProgress(payload);
      if (event) {
        this.zone.run(() => this.digitizationProgressSubject.next(event));
      }
    });
  }

  public async joinDossierGroup(dossierId: string): Promise<void> {
    if (!this.isBrowser || !dossierId?.trim()) return;
    await this.ensureConnection();
    if (!this.hubConnection || !this.isConnected() || this.joinedDossierGroups.has(dossierId)) return;
    try {
      await this.hubConnection.invoke('JoinDossier', dossierId);
      this.joinedDossierGroups.add(dossierId);
    } catch (err) {
      console.warn('SignalR JoinDossier failed:', err);
    }
  }

  public async leaveDossierGroup(dossierId: string): Promise<void> {
    if (!this.isBrowser || !dossierId?.trim() || !this.hubConnection) return;
    if (!this.joinedDossierGroups.has(dossierId)) return;
    try {
      if (this.hubConnection.state === signalR.HubConnectionState.Connected) {
        await this.hubConnection.invoke('LeaveDossier', dossierId);
      }
    } catch (err) {
      console.warn('SignalR LeaveDossier failed:', err);
    } finally {
      this.joinedDossierGroups.delete(dossierId);
    }
  }

  public receiveMessage(severity: string, summary: string, detail: string): void {
    this.notificationSubject.next({ severity, summary, detail });
  }

  private normalizeDigitizationProgress(raw: unknown): DigitizationProgressEvent | null {
    if (!raw || typeof raw !== 'object') return null;
    const o = raw as Record<string, unknown>;
    const read = (...keys: string[]): string | undefined => {
      for (const key of keys) {
        const val = o[key];
        if (val !== undefined && val !== null && val !== '') return String(val);
      }
      return undefined;
    };

    const dossierId = read('dossierId', 'DossierId');
    const documentVersionId = read('documentVersionId', 'DocumentVersionId');
    if (!dossierId || !documentVersionId) return null;

    return {
      dossierId,
      documentId: read('documentId', 'DocumentId') ?? '',
      documentVersionId,
      phase: read('phase', 'Phase') ?? 'ocr',
      status: read('status', 'Status') ?? '',
      progress: Number(read('progress', 'Progress') ?? 0),
      currentPage: Number(read('currentPage', 'CurrentPage') ?? 0),
      totalPages: Number(read('totalPages', 'TotalPages') ?? 0),
      extractionStatus: read('extractionStatus', 'ExtractionStatus') ?? null,
    };
  }
}
