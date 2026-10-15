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
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import {
  EcoScannerService,
  ScanFormat,
  ScanMode,
  ScanProgress,
} from '../../data-access/eco-scanner.service';
import { combineScanFiles, getPdfPageCount } from '../../utils/pdf-merge.util';

@Component({
  selector: 'app-scanner-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, InputTextModule],
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
  fileName = '';

  /** Phát ra file hoàn tất sau khi người dùng bấm Lưu tài liệu. */
  fileReady = output<File>();
  scanStateChange = output<ScanProgress>();
  scanningChange = output<boolean>();

  scanning = signal(false);
  merging = signal(false);
  statusMessage = signal('Sẵn sàng quét tài liệu');
  previewUrl = signal<string | null>(null);
  safePreviewUrl = signal<SafeResourceUrl | null>(null);
  previewKind = signal<'pdf' | 'image' | null>(null);

  /** File hiện tại đã quét được (có thể gồm nhiều đợt quét đã ghép nối). */
  currentFile = signal<File | null>(null);
  pageCount = signal<number>(0);
  isErrorStatus = signal<boolean>(false);

  ngOnDestroy(): void {
    this.revokePreview();
    if (this.scanning()) {
      this.ecoScanner.cancelActiveScan();
    }
  }

  isScanning(): boolean {
    return this.scanning() || this.merging();
  }

  getSafeFileName(): string {
    const raw = (this.fileName || 'TaiLieuQuet').trim();
    return raw.toLowerCase().endsWith('.pdf') ? raw : `${raw}.pdf`;
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }

  async startScan(): Promise<void> {
    if (this.disabled() || this.scanning()) return;

    if (!this.fileName || this.fileName.trim().length === 0) {
      this.scheduleToast({
        severity: 'error',
        summary: 'Thiếu thông tin',
        detail: 'Vui lòng nhập tên file',
      });
      return;
    }

    const validationError = this.validateFileName(this.fileName);
    if (validationError) {
      this.scheduleToast({
        severity: 'error',
        summary: 'Tên file không hợp lệ',
        detail: validationError,
      });
      return;
    }

    this.scanning.set(true);
    this.scanningChange.emit(true);
    this.isErrorStatus.set(false);
    this.revokePreview();
    this.currentFile.set(null);
    this.pageCount.set(0);
    this.statusMessage.set('Đang kết nối tới dịch vụ scan...');

    try {
      const files = await this.ecoScanner.scan(
        { format: this.format(), mode: this.mode(), fileName: this.fileName },
        (progress) => this.scheduleProgressUpdate(progress)
      );

      if (files.length === 0) {
        throw new Error('Không nhận được file quét từ máy scan.');
      }

      // Xử lý file nhận được (nếu nhiều file thì gom lại thành 1 PDF)
      let initialFile = files[0];
      const targetName = this.getSafeFileName();
      for (let i = 1; i < files.length; i++) {
        initialFile = await combineScanFiles(initialFile, files[i], targetName);
      }

      const pages = await getPdfPageCount(initialFile);
      this.currentFile.set(initialFile);
      this.pageCount.set(pages);
      this.showPreview(initialFile);
      this.statusMessage.set(`Đã quét xong (${pages} trang). Bạn có thể bấm "Lưu tài liệu" hoặc "Quét tiếp / Ghép thêm trang".`);

      this.scheduleToast({
        severity: 'success',
        summary: 'Quét thành công',
        detail: `Đã quét được ${pages} trang. Bạn có thể bấm Lưu tài liệu hoặc Quét tiếp để bổ sung trang.`,
      });
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Quét tài liệu thất bại';
      this.isErrorStatus.set(true);
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

  async scanMore(): Promise<void> {
    if (this.disabled() || this.scanning() || this.merging() || !this.currentFile()) return;

    this.scanning.set(true);
    this.scanningChange.emit(true);
    this.isErrorStatus.set(false);
    this.statusMessage.set('Đang kết nối để quét thêm trang bổ sung...');

    try {
      const files = await this.ecoScanner.scan(
        { format: this.format(), mode: this.mode(), fileName: `${this.fileName}_batch` },
        (progress) => this.scheduleProgressUpdate(progress)
      );

      if (files.length === 0) {
        throw new Error('Không có dữ liệu mới từ máy scan.');
      }

      this.merging.set(true);
      this.statusMessage.set('Đang ghép các trang mới vào tài liệu...');

      let mergedFile = this.currentFile()!;
      const targetName = this.getSafeFileName();
      for (const file of files) {
        mergedFile = await combineScanFiles(mergedFile, file, targetName);
      }

      const newPages = await getPdfPageCount(mergedFile);
      const addedPages = newPages - this.pageCount();

      this.currentFile.set(mergedFile);
      this.pageCount.set(newPages);
      this.showPreview(mergedFile);

      this.statusMessage.set(`Đã ghép thêm thành công! Tổng cộng hiện có: ${newPages} trang.`);
      this.scheduleToast({
        severity: 'success',
        summary: 'Ghép trang thành công',
        detail: `Đã bổ sung thêm ${addedPages > 0 ? addedPages : files.length} trang. Tổng cộng: ${newPages} trang.`,
      });
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Quét thêm thất bại';
      this.isErrorStatus.set(true);
      this.scheduleProgressUpdate({ phase: 'error', message });
      if (!message.includes('Đã hủy')) {
        this.scheduleToast({
          severity: 'error',
          summary: 'Lỗi quét thêm',
          detail: `${message} (Các trang đã quét trước đó vẫn được giữ nguyên)`,
        });
      }
    } finally {
      this.merging.set(false);
      this.scanning.set(false);
      this.scanningChange.emit(false);
    }
  }

  saveDocument(): void {
    const file = this.currentFile();
    if (!file) return;

    this.fileReady.emit(file);
    this.scheduleToast({
      severity: 'success',
      summary: 'Đã lưu tài liệu',
      detail: `Tài liệu "${file.name}" (${this.pageCount()} trang) đã được đưa vào hàng đợi tải lên.`,
    });
    this.resetScanner();
  }

  resetScanner(): void {
    this.revokePreview();
    this.currentFile.set(null);
    this.pageCount.set(0);
    this.isErrorStatus.set(false);
    this.statusMessage.set('Sẵn sàng quét tài liệu');
  }

  cancelScan(): void {
    this.ecoScanner.cancelActiveScan();
    this.scanning.set(false);
    this.merging.set(false);
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

  private scheduleToast(message: { severity: 'success' | 'error' | 'warn' | 'info'; summary: string; detail: string }): void {
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

  private validateFileName(name: string): string | null {
    const invalidCharsRegex = /[\\/:*?"<>|]/;
    if (invalidCharsRegex.test(name)) {
      return 'Tên file không được chứa các ký tự đặc biệt: \\ / : * ? " < > |';
    }
    if (name.trim() !== name) {
      return 'Tên file không được có khoảng trắng ở đầu hoặc cuối.';
    }
    const controlCharsRegex = /[\x00-\x1F\x7F]/;
    if (controlCharsRegex.test(name)) {
      return 'Tên file chứa các ký tự điều khiển không hợp lệ.';
    }
    if (name.length > 200) {
      return 'Tên file quá dài (tối đa 200 ký tự).';
    }
    return null;
  }
}
