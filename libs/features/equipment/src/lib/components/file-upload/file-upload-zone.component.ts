import {
  Component,
  OnInit,
  signal,
  computed,
  inject,
  input,
  output,
  OnDestroy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { ProgressBarModule } from 'primeng/progressbar';
import { MessageService } from 'primeng/api';
import { FileUploadService, UploadProgress, FileUploadResponse, extractApiErrorMessage } from '../../data-access/file-upload.service';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

interface UploadItem {
  id: string;
  file: File;
  progress: UploadProgress;
  error?: string;
}

export type FileUploadHandler = (
  file: File,
  onProgress?: (progress: UploadProgress) => void
) => Promise<FileUploadResponse>;

@Component({
  selector: 'app-file-upload-zone',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, ProgressBarModule],
  templateUrl: './file-upload-zone.component.html',
  styleUrl: './file-upload-zone.component.scss'
})
export class FileUploadZoneComponent implements OnInit, OnDestroy {
  private fileUploadService = inject(FileUploadService);
  private messageService = inject(MessageService);
  private destroy$ = new Subject<void>();

  // Inputs
  folderId = input<string>('');
  /** Khi set, bỏ qua folderId và gọi handler tùy chỉnh (vd. upload hồ sơ). */
  uploadHandler = input<FileUploadHandler | null>(null);
  maxFileSize = input<number>(500 * 1024 * 1024); // 500MB default
  allowedExtensions = input<string[]>([
    '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx',
    '.jpg', '.jpeg', '.png', '.gif', '.tiff', '.dwg',
    '.zip', '.rar', '.7z', '.txt', '.csv'
  ]);

  // Outputs
  fileUploaded = output<{ documentVersionId: string; fileName: string }>();
  uploadError = output<{ fileName: string; error: string }>();

  // State
  isDragOver = signal<boolean>(false);
  uploads = signal<Map<string, UploadItem>>(new Map());
  isUploading = computed(() => Array.from(this.uploads().values()).some(u => u.progress.status === 'uploading'));

  private destroy = new Subject<void>();

  ngOnInit() {
    // Subscribe to upload progress
    this.fileUploadService.getUploadProgress()
      .pipe(takeUntil(this.destroy$))
      .subscribe(progressMap => {
        // Update local upload items with progress
      });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Prevent default drag behavior
   */
  onDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
  }

  /**
   * Handle drop event
   */
  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);

    const files = event.dataTransfer?.files;
    if (files) {
      this.handleFiles(files);
    }
  }

  /**
   * Handle file input from button click
   */
  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files) {
      this.handleFiles(input.files);
      // Reset input so same file can be selected again
      input.value = '';
    }
  }

  /**
   * Process selected/dropped files
   */
  private handleFiles(fileList: FileList) {
    const currentUploads = this.uploads();
    const handler = this.uploadHandler();
    const folderId = this.folderId();

    if (!handler && !folderId) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi cấu hình',
        detail: 'Thiếu folderId hoặc uploadHandler',
      });
      return;
    }

    for (let i = 0; i < fileList.length; i++) {
      const file = fileList[i];

      // Validate file
      const validation = this.validateFile(file);
      if (!validation.valid) {
        this.messageService.add({
          severity: 'error',
          summary: 'Tệp không hợp lệ',
          detail: `${file.name}: ${validation.error}`
        });
        this.uploadError.emit({
          fileName: file.name,
          error: validation.error || 'File validation failed'
        });
        continue;
      }

      // Create upload item
      const uploadId = `upload_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
      const uploadItem: UploadItem = {
        id: uploadId,
        file,
        progress: {
          uploadId,
          progress: 0,
          uploadedBytes: 0,
          totalBytes: file.size,
          status: 'pending'
        }
      };

      currentUploads.set(uploadId, uploadItem);
      this.uploads.set(new Map(currentUploads));

      // Start upload
      this.startUpload(uploadId, file, folderId, handler);
    }
  }

  /**
   * Validate file
   */
  private validateFile(file: File): { valid: boolean; error?: string } {
    // Check file size
    if (file.size > this.maxFileSize()) {
      return {
        valid: false,
        error: `Kích thước tệp ${this.formatFileSize(file.size)} vượt quá giới hạn ${this.formatFileSize(this.maxFileSize())}`
      };
    }

    // Check file extension
    const ext = this.getFileExtension(file.name).toLowerCase();
    if (!this.allowedExtensions().includes(ext)) {
      return {
        valid: false,
        error: `Loại tệp không được hỗ trợ: ${ext}`
      };
    }

    return { valid: true };
  }

  /**
   * Start file upload
   */
  private async startUpload(
    uploadId: string,
    file: File,
    folderId: string,
    handler: FileUploadHandler | null
  ) {
    try {
      const onProgress = (progress: UploadProgress) => this.updateProgress(uploadId, progress);

      const result = handler
        ? await handler(file, onProgress)
        : await this.fileUploadService.uploadFile(file, folderId, onProgress);

      // Success
      this.updateProgress(uploadId, {
        uploadId,
        progress: 100,
        uploadedBytes: file.size,
        totalBytes: file.size,
        status: 'completed'
      });

      this.messageService.add({
        severity: 'success',
        summary: 'Tải lên thành công',
        detail: file.name
      });

      this.fileUploaded.emit({
        documentVersionId: result.documentVersionId,
        fileName: file.name
      });

      // Auto-remove after 3 seconds
      setTimeout(() => {
        const current = this.uploads();
        current.delete(uploadId);
        this.uploads.set(new Map(current));
      }, 3000);
    } catch (error: unknown) {
      const errorMsg = extractApiErrorMessage(error);
      this.updateProgress(uploadId, {
        uploadId,
        progress: 0,
        uploadedBytes: 0,
        totalBytes: file.size,
        status: 'error',
        error: errorMsg
      });

      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi tải lên',
        detail: `${file.name}: ${errorMsg}`
      });

      this.uploadError.emit({
        fileName: file.name,
        error: errorMsg
      });
    }
  }

  /**
   * Update upload progress
   */
  private updateProgress(uploadId: string, progress: UploadProgress) {
    const current = this.uploads();
    const item = current.get(uploadId);
    if (item) {
      item.progress = progress;
      current.set(uploadId, item);
      this.uploads.set(new Map(current));
    }
  }

  /**
   * Cancel upload
   */
  cancelUpload(uploadId: string) {
    const current = this.uploads();
    current.delete(uploadId);
    this.uploads.set(new Map(current));
  }

  /**
   * Get file extension
   */
  private getFileExtension(fileName: string): string {
    const lastDot = fileName.lastIndexOf('.');
    return lastDot === -1 ? '' : fileName.substring(lastDot);
  }

  /**
   * Format bytes to human readable
   */
  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  }

  /**
   * Get display text for upload status
   */
  getStatusText(progress: UploadProgress): string {
    switch (progress.status) {
      case 'pending':
        return 'Chờ xử lý';
      case 'uploading':
        return `Đang tải lên... ${progress.progress}%`;
      case 'completed':
        return 'Hoàn thành';
      case 'error':
        return `Lỗi: ${progress.error}`;
      default:
        return '';
    }
  }

  /**
   * Get upload items sorted by newest first
   */
  getUploadItems() {
    return Array.from(this.uploads().values()).reverse();
  }
}
