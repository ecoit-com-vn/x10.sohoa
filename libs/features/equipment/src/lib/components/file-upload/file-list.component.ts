import {
  Component,
  OnInit,
  signal,
  computed,
  inject,
  input,
  effect,
  OnDestroy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { TooltipModule } from 'primeng/tooltip';
import { FileDownloadService, DocumentVersionInfo } from '../../data-access/file-download.service';
import { FileService } from '../../data-access/file.service';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { DeleteConfirmDialogComponent } from '@sohoa.frontend/shared/layout';

@Component({
  selector: 'app-file-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    TableModule,
    InputTextModule,
    PaginatorModule,
    ToastModule,
    TooltipModule,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './file-list.component.html',
  styleUrl: './file-list.component.scss'
})
export class FileListComponent implements OnInit, OnDestroy {
  private fileDownloadService = inject(FileDownloadService);
  private fileService = inject(FileService);
  private messageService = inject(MessageService);
  private destroy$ = new Subject<void>();

  // Inputs
  folderId = input.required<string>();

  // State
  files = signal<DocumentVersionInfo[]>([]);
  totalCount = signal<number>(0);
  page = signal<number>(1);
  pageSize = signal<number>(10);
  searchKeyword = signal<string>('');
  isLoading = signal<boolean>(false);
  isDownloading = signal<Set<string>>(new Set());
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<DocumentVersionInfo | null>(null);
  deleteLoading = signal<boolean>(false);

  // Computed
  filteredFiles = computed(() => {
    const keyword = this.searchKeyword().toLowerCase().trim();
    if (!keyword) return this.files();
    return this.files().filter(f => f.fileName.toLowerCase().includes(keyword));
  });
  // Giữ cả tên tệp và số phiên bản trong popup xóa dùng chung.
  readonly deleteTargetLabel = computed(() => {
    const file = this.deleteTarget();

    return file ? `${file.fileName} (phiên bản ${file.versionNumber})` : '';
  });

  ngOnInit() {
    // Auto-load files when folderId changes
    effect(() => {
      const folderId = this.folderId();
      if (folderId) {
        this.loadFiles(1, this.pageSize());
      }
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Load files for folder
   */
  loadFiles(page: number, pageSize: number) {
    this.isLoading.set(true);
    const folderId = this.folderId();

    this.fileService.getFilesByFolder(folderId, page, pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.files.set(response.items);
          this.totalCount.set(response.totalCount);
          this.page.set(page);
          this.pageSize.set(pageSize);
          this.isLoading.set(false);
        },
        error: (error) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi tải tệp',
            detail: error?.message || 'Không thể tải danh sách tệp'
          });
          this.isLoading.set(false);
        }
      });
  }

  /**
   * Handle pagination change
   */
  onPageChange(event: any) {
    const pageNum = event.page + 1; // PrimeNG uses 0-based
    const pageSize = event.rows;
    this.page.set(pageNum);
    this.pageSize.set(pageSize);
    this.loadFiles(pageNum, pageSize);
  }

  /**
   * Download file
   */
  async downloadFile(file: DocumentVersionInfo) {
    const downloadingSet = new Set(this.isDownloading());
    downloadingSet.add(file.id);
    this.isDownloading.set(downloadingSet);

    try {
      await this.fileDownloadService.downloadFile(file.id, file.fileName);

      this.messageService.add({
        severity: 'success',
        summary: 'Tải về thành công',
        detail: file.fileName
      });
    } catch (error: any) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi tải về',
        detail: error?.message || 'Không thể tải tệp'
      });
    } finally {
      downloadingSet.delete(file.id);
      this.isDownloading.set(new Set(downloadingSet));
    }
  }

  /**
   * Delete file version
   */
  deleteFile(file: DocumentVersionInfo) {
    this.deleteTarget.set(file);
    this.showDeleteConfirm.set(true);
  }

  onCancelDelete(): void {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.deleteLoading()) return;

    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  onConfirmDelete(): void {
    const file = this.deleteTarget();

    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!file || this.deleteLoading()) return;

    this.deleteLoading.set(true);
    this.fileService.deleteFileVersion(file.id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.deleteLoading.set(false))
      )
      .subscribe({
        next: () => {
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: file.fileName
          });
          // Reload list
          this.loadFiles(this.page(), this.pageSize());
        },
        error: (error) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi xóa tệp',
            detail: error?.error?.message || error?.message || 'Không thể xóa tệp'
          });
        }
      });
  }

  /**
   * Format file size
   */
  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  }

  /**
   * Format date
   */
  formatDate(dateString: string): string {
    try {
      return new Intl.DateTimeFormat('vi-VN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      }).format(new Date(dateString));
    } catch {
      return dateString;
    }
  }

  /**
   * Get file icon based on type
   */
  getFileIcon(fileName: string): string {
    const ext = fileName.substring(fileName.lastIndexOf('.') + 1).toLowerCase();
    const iconMap: { [key: string]: string } = {
      'pdf': 'pi-file-pdf',
      'doc': 'pi-file-word',
      'docx': 'pi-file-word',
      'xls': 'pi-file-excel',
      'xlsx': 'pi-file-excel',
      'ppt': 'pi-file-powerpoint',
      'pptx': 'pi-file-powerpoint',
      'jpg': 'pi-image',
      'jpeg': 'pi-image',
      'png': 'pi-image',
      'gif': 'pi-image',
      'zip': 'pi-file-archive',
      'rar': 'pi-file-archive',
      '7z': 'pi-file-archive',
      'txt': 'pi-file',
      'csv': 'pi-file'
    };
    return iconMap[ext] || 'pi-file';
  }

  /**
   * Check if file is downloading
   */
  isDownloadingFile(fileId: string): boolean {
    return this.isDownloading().has(fileId);
  }
}
