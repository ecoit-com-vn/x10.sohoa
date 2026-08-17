import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { EcoPaginatorComponent, WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { FileUploadHandler, FileUploadZoneComponent } from '@sohoa.frontend/features/equipment';
import { OcrModuleJobListItem, OcrModuleService } from '@sohoa.frontend/features/ocr-module';

/**
 * Màn hình quản trị "Quản lý dữ liệu huấn luyện AI-OCR" — cho phép tải lên 1 file PDF độc lập (không
 * gắn Dossier/Thiết bị), đưa vào OCR ngay, và mở "Phân tích OCR nâng cao" (tái dùng lib-ocr-insights-content)
 * khi đã xử lý xong. Toàn bộ dữ liệu dựa trên OcrModuleJob/OcrModuleRegion đã có sẵn (SourceType=NewUpload) —
 * không đụng tới Dossier/Document/OCR pipeline hiện tại của hồ sơ.
 */
@Component({
  selector: 'app-ai-ocr-training-document-list',
  standalone: true,
  imports: [
    CommonModule,
    DialogModule,
    ToastModule,
    WfBreadcrumbComponent,
    EcoPaginatorComponent,
    FileUploadZoneComponent,
  ],
  providers: [MessageService],
  templateUrl: './ai-ocr-training-document-list.component.html',
  styleUrl: './ai-ocr-training-document-list.component.scss',
})
export class AiOcrTrainingDocumentListComponent implements OnInit, OnDestroy {
  private readonly ocrModuleService = inject(OcrModuleService);
  private readonly messageService = inject(MessageService);

  jobs = signal<OcrModuleJobListItem[]>([]);
  loading = signal(false);
  page = signal(1);
  pageSize = signal(10);
  totalCount = signal(0);

  uploadDialogVisible = signal(false);

  deleteDialogVisible = signal(false);
  deleteTarget = signal<OcrModuleJobListItem | null>(null);
  deleting = signal(false);

  private pollTimer: ReturnType<typeof setInterval> | null = null;

  uploadHandler: FileUploadHandler = (file, onProgress) =>
    new Promise((resolve, reject) => {
      onProgress?.({ progress: 0, uploadedBytes: 0, totalBytes: file.size, status: 'uploading' });
      this.ocrModuleService.uploadTrainingDocument(file).subscribe({
        next: (res) => {
          onProgress?.({ progress: 100, uploadedBytes: file.size, totalBytes: file.size, status: 'completed' });
          resolve({
            documentVersionId: res.jobId,
            documentId: res.jobId,
            versionNumber: 1,
            status: res.state,
          });
        },
        error: (err) => reject(err),
      });
    });

  ngOnInit(): void {
    this.loadJobs();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  loadJobs(): void {
    this.loading.set(true);
    this.ocrModuleService
      .getUploadedJobs(this.page(), this.pageSize())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => {
          this.jobs.set(result.items);
          this.totalCount.set(result.totalCount);
          this.syncPolling();
        },
        error: (err) => this.showError(err, 'Không thể tải danh sách dữ liệu huấn luyện AI-OCR.'),
      });
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadJobs();
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize.set(pageSize);
    this.page.set(1);
    this.loadJobs();
  }

  openUploadDialog(): void {
    this.uploadDialogVisible.set(true);
  }

  closeUploadDialog(): void {
    this.uploadDialogVisible.set(false);
  }

  onFileUploaded(): void {
    // Job vừa tạo ở trạng thái "Đang xử lý" — đóng dialog, tải lại danh sách và bắt đầu theo dõi tiến trình.
    this.closeUploadDialog();
    this.page.set(1);
    this.loadJobs();
  }

  requestDelete(item: OcrModuleJobListItem): void {
    this.deleteTarget.set(item);
    this.deleteDialogVisible.set(true);
  }

  cancelDelete(): void {
    if (this.deleting()) return;
    this.deleteDialogVisible.set(false);
    this.deleteTarget.set(null);
  }

  confirmDelete(): void {
    const item = this.deleteTarget();
    if (!item || this.deleting()) return;

    this.deleting.set(true);
    this.ocrModuleService
      .deleteJob(item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.deleteDialogVisible.set(false);
          this.deleteTarget.set(null);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa dữ liệu huấn luyện.' });
          this.loadJobs();
        },
        error: (err) => this.showError(err, 'Không thể xóa dữ liệu huấn luyện.'),
      });
  }

  canOpenOcrAnalysis(item: OcrModuleJobListItem): boolean {
    return this.isOcrComplete(item);
  }

  openOcrAnalysis(item: OcrModuleJobListItem): void {
    if (!this.canOpenOcrAnalysis(item)) return;
    const url = `/#/administration/trainning-ai-ocr/${item.id}/ocr-analysis?label=${encodeURIComponent(item.fileName)}`;
    window.open(url, '_blank');
  }

  // Cùng cách kiểm tra như cột "OCR" ở tab Tài liệu đính kèm của hồ sơ
  // (dossier-digitization.util.ts: isOcrComplete/isOcrBarFailed/isOcrBarActive/getOcrBarPercent) —
  // hoàn thành/lỗi được xác định theo `state` trả về từ backend, không phải so sánh percent === 100.
  isOcrComplete(item: OcrModuleJobListItem): boolean {
    return item.state === 'Ready';
  }

  isOcrBarFailed(item: OcrModuleJobListItem): boolean {
    return item.state === 'Failed';
  }

  isOcrBarActive(item: OcrModuleJobListItem): boolean {
    return item.state === 'Materializing';
  }

  getOcrBarPercent(item: OcrModuleJobListItem): number {
    if (this.isOcrComplete(item) || this.isOcrBarFailed(item)) return 0;
    if (!item.totalPages) return 0;
    return Math.min(100, Math.max(0, Math.round(((item.currentPage ?? 0) / item.totalPages) * 100)));
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '---';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString('vi-VN');
  }

  private syncPolling(): void {
    const hasActiveJob = this.jobs().some((j) => j.state === 'Materializing');
    if (hasActiveJob) {
      this.startPolling();
    } else {
      this.stopPolling();
    }
  }

  private startPolling(): void {
    if (this.pollTimer) return;
    this.pollTimer = setInterval(() => this.loadJobs(), 5000);
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private showError(err: any, fallback: string): void {
    this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err?.error?.message ?? fallback });
  }
}
