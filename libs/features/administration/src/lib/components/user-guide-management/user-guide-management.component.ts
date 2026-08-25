import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import {
  DeleteConfirmDialogComponent,
  WfBreadcrumbComponent,
} from '@sohoa.frontend/shared/layout';
import { UserGuide, UserGuideService } from '../../services/user-guide.service';

const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx'];
const MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB

/** Hàng hiển thị trong bảng — có thể là bản ghi thật (từ API) hoặc tài liệu tĩnh gắn cứng (assetUrl, không qua API). */
export type UserGuideRow = UserGuide & { isStatic?: boolean; assetUrl?: string };

/**
 * Các hướng dẫn sử dụng gắn cứng — file được đặt sẵn trong apps/admin-portal/public/assets/user-guides/
 * và không đi qua API/MinIO. Không hỗ trợ Sửa/Xóa vì không phải bản ghi thật trong DB.
 */
const STATIC_GUIDES: UserGuideRow[] = [
  {
    id: -1,
    roleName: 'Quản trị đơn vị',
    fileName: 'Hướng dẫn sử dụng _ Quản trị đơn vị.docx',
    assetUrl: '/assets/user-guides/huong-dan-su-dung-quan-tri-don-vi.docx',
    isStatic: true,
  },
  {
    id: -2,
    roleName: 'Quản trị hệ thống',
    fileName: 'Hướng dẫn sử dụng _ QTHT.docx',
    assetUrl: '/assets/user-guides/huong-dan-su-dung-qtht.docx',
    isStatic: true,
  },
  {
    id: -3,
    roleName: 'Lãnh đạo, cán bộ',
    fileName: 'Hướng dẫn sử dụng _ Lãnh đạo, cán bộ.docx',
    assetUrl: '/assets/user-guides/huong-dan-su-dung-lanh-dao-can-bo.docx',
    isStatic: true,
  },
];

@Component({
  selector: 'app-user-guide-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ToastModule,
    WfBreadcrumbComponent,
    DeleteConfirmDialogComponent,
  ],
  providers: [MessageService],
  templateUrl: './user-guide-management.component.html',
  styleUrl: './user-guide-management.component.scss',
})
export class UserGuideManagementComponent implements OnInit {
  private userGuideService = inject(UserGuideService);
  private messageService = inject(MessageService);

  guides = signal<UserGuide[]>([]);
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  /** Danh sách hiển thị: tài liệu gắn cứng luôn đứng trước, nối tiếp là dữ liệu thật từ API. */
  displayGuides = computed<UserGuideRow[]>(() => [...STATIC_GUIDES, ...this.guides()]);

  currentView = signal<'list' | 'add' | 'edit'>('list');
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentGuide = signal<{ id?: number; roleName: string }>({ roleName: '' });

  selectedFile = signal<File | null>(null);
  isDragOver = signal<boolean>(false);
  fileError = signal<string>('');

  formSubmitted = signal<boolean>(false);
  roleNameError = computed(() => {
    if (this.formSubmitted() && !this.currentGuide().roleName?.trim()) {
      return 'Tên vai trò là bắt buộc';
    }
    return '';
  });

  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<UserGuide | null>(null);
  deleteLoading = signal<boolean>(false);
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.roleName ?? '');

  downloadingId = signal<number | null>(null);

  ngOnInit(): void {
    this.loadGuides();
  }

  loadGuides(): void {
    this.loading.set(true);
    this.userGuideService
      .getGuides()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const raw = Array.isArray(res) ? res : [];
          this.guides.set(raw);
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi tải dữ liệu',
            detail: 'Không thể tải danh sách hướng dẫn sử dụng.',
          });
        },
      });
  }

  onAddNew(): void {
    this.isEdit.set(false);
    this.currentGuide.set({ roleName: '' });
    this.selectedFile.set(null);
    this.fileError.set('');
    this.formSubmitted.set(false);
    this.dialogHeader.set('Thêm hướng dẫn sử dụng');
    this.currentView.set('add');
  }

  onEdit(guide: UserGuide): void {
    this.isEdit.set(true);
    this.currentGuide.set({ id: guide.id, roleName: guide.roleName });
    this.selectedFile.set(null);
    this.fileError.set('');
    this.formSubmitted.set(false);
    this.dialogHeader.set('Chỉnh sửa hướng dẫn sử dụng');
    this.currentView.set('edit');
  }

  onCloseDialog(): void {
    this.currentView.set('list');
  }

  onRoleNameChange(value: string): void {
    this.currentGuide.update((g) => ({ ...g, roleName: value }));
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.handleFile(file);
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.handleFile(file);
    }
    input.value = '';
  }

  removeSelectedFile(): void {
    this.selectedFile.set(null);
    this.fileError.set('');
  }

  private handleFile(file: File): void {
    const ext = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      this.fileError.set(`Loại tệp không được hỗ trợ: ${ext}`);
      return;
    }
    if (file.size > MAX_FILE_SIZE) {
      this.fileError.set(`Kích thước tệp vượt quá giới hạn ${this.formatFileSize(MAX_FILE_SIZE)}`);
      return;
    }
    this.fileError.set('');
    this.selectedFile.set(file);
  }

  formatFileSize(bytes?: number): string {
    if (!bytes) return '';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${Math.round((bytes / Math.pow(k, i)) * 100) / 100} ${sizes[i]}`;
  }

  onSaveGuide(): void {
    this.formSubmitted.set(true);
    if (this.roleNameError()) {
      return;
    }
    if (!this.isEdit() && !this.selectedFile()) {
      this.fileError.set('Vui lòng tải lên file hướng dẫn');
      return;
    }

    const formData = new FormData();
    formData.append('roleName', this.currentGuide().roleName.trim());
    if (this.selectedFile()) {
      formData.append('file', this.selectedFile() as File);
    }

    this.saving.set(true);
    const request$ = this.isEdit()
      ? this.userGuideService.updateGuide(this.currentGuide().id as number, formData)
      : this.userGuideService.createGuide(formData);

    request$.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: this.isEdit() ? 'Cập nhật' : 'Thêm mới',
          detail: this.isEdit() ? 'Cập nhật hướng dẫn sử dụng thành công!' : 'Thêm mới hướng dẫn sử dụng thành công!',
        });
        this.loadGuides();
        this.currentView.set('list');
      },
      error: (err) => {
        const detailMsg =
          err?.error?.message || err?.message || (this.isEdit() ? 'Không thể cập nhật hướng dẫn.' : 'Không thể thêm mới hướng dẫn.');
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
      },
    });
  }

  onDelete(guide: UserGuide): void {
    this.deleteTarget.set(guide);
    this.showDeleteConfirm.set(true);
  }

  onCancelDelete(): void {
    if (this.deleteLoading()) return;
    this.closeDeleteDialog();
  }

  onConfirmDelete(): void {
    const guide = this.deleteTarget();
    if (!guide || this.deleteLoading()) return;

    this.deleteLoading.set(true);
    this.userGuideService
      .deleteGuide(guide.id)
      .pipe(finalize(() => this.deleteLoading.set(false)))
      .subscribe({
        next: () => {
          this.closeDeleteDialog();
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: 'Đã xóa hướng dẫn sử dụng thành công!',
          });
          this.loadGuides();
        },
        error: (err) => {
          const detailMsg = err?.error?.message || err?.message || 'Xóa hướng dẫn sử dụng thất bại.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
        },
      });
  }

  private closeDeleteDialog(): void {
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  onDownload(guide: UserGuideRow): void {
    if (guide.isStatic && guide.assetUrl) {
      const a = document.createElement('a');
      a.href = guide.assetUrl;
      a.download = guide.fileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      return;
    }

    this.downloadingId.set(guide.id);
    this.userGuideService
      .downloadGuide(guide.id)
      .pipe(finalize(() => this.downloadingId.set(null)))
      .subscribe({
        next: (res) => {
          const blob = res.body;
          if (!blob) return;
          const fileName = this.extractFileName(res.headers.get('Content-Disposition')) || guide.fileName;
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = fileName;
          document.body.appendChild(a);
          a.click();
          a.remove();
          window.URL.revokeObjectURL(url);
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải xuống file hướng dẫn.',
          });
        },
      });
  }

  private extractFileName(contentDisposition: string | null): string | null {
    if (!contentDisposition) return null;
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(contentDisposition);
    return match ? decodeURIComponent(match[1]) : null;
  }
}
