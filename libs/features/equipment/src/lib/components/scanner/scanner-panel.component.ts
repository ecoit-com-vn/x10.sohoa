import {
  Component,
  OnDestroy,
  inject,
  input,
  output,
  signal,
  NgZone,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import {
  EcoScannerService,
  ScanFormat,
  ScanMode,
  ScanProgress,
} from '../../data-access/eco-scanner.service';

@Component({
  selector: 'app-scanner-panel',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  templateUrl: './scanner-panel.component.html',
  styleUrl: './scanner-panel.component.scss',
})
export class ScannerPanelComponent implements OnDestroy {
  private ecoScanner = inject(EcoScannerService);
  private messageService = inject(MessageService);
  private sanitizer = inject(DomSanitizer);
  private ngZone = inject(NgZone);

  disabled = input(false);
  format = input<ScanFormat>('pdf');
  mode = input<ScanMode>('s');

  /** Phát ra từng file sau khi quét xong (mode 'm' có thể nhiều lần). */
  fileReady = output<File>();
  scanStateChange = output<ScanProgress>();
  scanningChange = output<boolean>();

  scanning = signal(false);
  statusMessage = signal('Sẵn sàng quét tài liệu');
  previewUrl = signal<string | null>(null);
  safePreviewUrl = signal<SafeResourceUrl | null>(null);
  previewKind = signal<'pdf' | 'image' | null>(null);

  ngOnDestroy(): void {
    this.revokePreview();
    if (this.scanning()) {
      this.ecoScanner.cancelActiveScan();
    }
  }

  isScanning(): boolean {
    return this.scanning();
  }

  async startScan(): Promise<void> {
    if (this.disabled() || this.scanning()) return;

    this.scanning.set(true);
    this.scanningChange.emit(true);
    this.revokePreview();
    this.statusMessage.set('Đang kết nối tới dịch vụ scan...');

    try {
      const files = await this.ecoScanner.scan(
        { format: this.format(), mode: this.mode() },
        (progress) => this.scheduleProgressUpdate(progress)
      );

      for (const file of files) {
        this.showPreview(file);
        this.fileReady.emit(file);
      }

      this.scheduleToast({
        severity: 'success',
        summary: 'Quét thành công',
        detail: files.length === 1 ? files[0].name : `Đã quét ${files.length} file`,
      });
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Quét tài liệu thất bại';
      this.scheduleProgressUpdate({ phase: 'error', message });
      if (!message.includes('Đã hủy')) {
        this.scheduleToast({
          severity: 'error',
          summary: 'Lỗi quét',
          detail: message,
        });
      }
    } finally {
      this.scanning.set(false);
      this.scanningChange.emit(false);
    }
  }

  cancelScan(): void {
    this.ecoScanner.cancelActiveScan();
    this.scanning.set(false);
    this.scanningChange.emit(false);
    this.statusMessage.set('Đã hủy quét');
  }

  private scheduleProgressUpdate(progress: ScanProgress): void {
    this.ngZone.run(() => {
      if (progress.message) {
        this.statusMessage.set(progress.message);
      }
      this.scanStateChange.emit(progress);
    });
  }

  private scheduleToast(message: { severity: 'success' | 'error'; summary: string; detail: string }): void {
    queueMicrotask(() => {
      this.ngZone.run(() => this.messageService.add(message));
    });
  }

  private showPreview(file: File): void {
    this.revokePreview();
    const url = URL.createObjectURL(file);
    this.previewUrl.set(url);
    this.safePreviewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
    this.previewKind.set(file.type === 'application/pdf' ? 'pdf' : 'image');
  }

  private revokePreview(): void {
    const url = this.previewUrl();
    if (url) {
      URL.revokeObjectURL(url);
    }
    this.previewUrl.set(null);
    this.safePreviewUrl.set(null);
    this.previewKind.set(null);
  }
}
