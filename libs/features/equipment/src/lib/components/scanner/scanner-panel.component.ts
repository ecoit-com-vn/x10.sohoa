import {
  Component,
  OnDestroy,
  inject,
  input,
  output,
  signal,
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

  disabled = input(false);
  format = input<ScanFormat>('pdf');
  mode = input<ScanMode>('s');

  /** Phát ra từng file sau khi quét xong (mode 'm' có thể nhiều lần). */
  fileReady = output<File>();
  scanStateChange = output<ScanProgress>();

  scanning = signal(false);
  statusMessage = signal('Sẵn sàng quét tài liệu');
  previewUrl = signal<string | null>(null);
  safePreviewUrl = signal<SafeResourceUrl | null>(null);
  previewKind = signal<'pdf' | 'image' | null>(null);

  ngOnDestroy(): void {
    this.revokePreview();
    this.ecoScanner.cancelActiveScan();
  }

  async startScan(): Promise<void> {
    if (this.disabled() || this.scanning()) return;

    this.scanning.set(true);
    this.revokePreview();
    this.statusMessage.set('Đang kết nối tới dịch vụ scan...');

    try {
      const files = await this.ecoScanner.scan(
        { format: this.format(), mode: this.mode() },
        (progress) => this.onProgress(progress)
      );

      for (const file of files) {
        this.showPreview(file);
        this.fileReady.emit(file);
      }

      this.messageService.add({
        severity: 'success',
        summary: 'Quét thành công',
        detail: files.length === 1 ? files[0].name : `Đã quét ${files.length} file`,
      });
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Quét tài liệu thất bại';
      this.statusMessage.set(message);
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi quét',
        detail: message,
      });
    } finally {
      this.scanning.set(false);
    }
  }

  cancelScan(): void {
    this.ecoScanner.cancelActiveScan();
    this.scanning.set(false);
    this.statusMessage.set('Đã hủy quét');
  }

  private onProgress(progress: ScanProgress): void {
    if (progress.message) {
      this.statusMessage.set(progress.message);
    }
    this.scanStateChange.emit(progress);
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
