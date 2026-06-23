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

  private normalizeDossierId(dossierId: string): string {
    return dossierId?.trim().toLowerCase() ?? '';
  }

  public isConnected(): boolean {
    return this.isBrowser && this.hubConnection?.state === signalR.HubConnectionState.Connected;
  }

  public async ensureConnection(): Promise<void> {
    if (!this.isBrowser) return;

    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (this.startPromise) {
      await this.startPromise;
      return;
    }

    this.startConnection();
    if (this.startPromise) {
      await this.startPromise;
    }
  }

  public startConnection(): void {
    if (!this.isBrowser) return;

    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (
      this.hubConnection &&
      this.hubConnection.state === signalR.HubConnectionState.Connecting
    ) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.config.apiGatewayUrl}/hubs/notifications`, {
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.registerListeners();

    this.startPromise = this.zone.run(async () => {
      try {
        await this.hubConnection!.start();
        console.log('[SignalR] Connected to /hubs/notifications');
        await this.rejoinDossierGroups();
      } catch (err) {
        console.error('[SignalR] Connection failed:', err);
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
        console.debug('[SignalR] ReceiveDigitizationProgress', event);
        this.zone.run(() => this.digitizationProgressSubject.next(event));
      } else {
        console.warn('[SignalR] Invalid digitization progress payload', payload);
      }
    });

    this.hubConnection.onreconnected(async () => {
      console.log('[SignalR] Reconnected — rejoin dossier groups');
      await this.rejoinDossierGroups();
    });

    this.hubConnection.onclose((err) => {
      if (err) {
        console.warn('[SignalR] Connection closed:', err);
      }
      this.listenersRegistered = false;
    });
  }

  public async joinDossierGroup(dossierId: string): Promise<boolean> {
    const normalizedId = this.normalizeDossierId(dossierId);
    if (!this.isBrowser || !normalizedId) return false;

    await this.ensureConnection();
    if (!this.hubConnection || !this.isConnected()) {
      console.warn('[SignalR] Cannot join dossier group — hub not connected');
      return false;
    }

    if (this.joinedDossierGroups.has(normalizedId)) return true;

    for (let attempt = 1; attempt <= 3; attempt++) {
      try {
        await this.hubConnection.invoke('JoinDossier', normalizedId);
        this.joinedDossierGroups.add(normalizedId);
        console.log('[SignalR] Joined dossier group', normalizedId);
        return true;
      } catch (err) {
        console.warn(`[SignalR] JoinDossier attempt ${attempt} failed:`, err);
        if (attempt < 3) {
          await new Promise((r) => setTimeout(r, 500 * attempt));
          await this.ensureConnection();
        }
      }
    }

    return false;
  }

  private async rejoinDossierGroups(): Promise<void> {
    const groups = [...this.joinedDossierGroups];
    this.joinedDossierGroups.clear();
    for (const id of groups) {
      await this.joinDossierGroup(id);
    }
  }

  public async leaveDossierGroup(dossierId: string): Promise<void> {
    const normalizedId = this.normalizeDossierId(dossierId);
    if (!this.isBrowser || !normalizedId || !this.hubConnection) return;
    if (!this.joinedDossierGroups.has(normalizedId)) return;
    try {
      if (this.hubConnection.state === signalR.HubConnectionState.Connected) {
        await this.hubConnection.invoke('LeaveDossier', normalizedId);
      }
    } catch (err) {
      console.warn('[SignalR] LeaveDossier failed:', err);
    } finally {
      this.joinedDossierGroups.delete(normalizedId);
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
      dossierId: dossierId.toLowerCase(),
      documentId: read('documentId', 'DocumentId') ?? '',
      documentVersionId: documentVersionId.toLowerCase(),
      phase: read('phase', 'Phase') ?? 'ocr',
      status: read('status', 'Status') ?? '',
      progress: Number(read('progress', 'Progress') ?? 0),
      currentPage: Number(read('currentPage', 'CurrentPage') ?? 0),
      totalPages: Number(read('totalPages', 'TotalPages') ?? 0),
      extractionStatus: read('extractionStatus', 'ExtractionStatus') ?? null,
    };
  }
}
