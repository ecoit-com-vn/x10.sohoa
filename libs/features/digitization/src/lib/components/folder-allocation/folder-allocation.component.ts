import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { FolderAllocationService, FolderAllocationItem } from '../../data-access/folder-allocation.service';
import { FolderAllocationDialogComponent } from './folder-allocation-dialog.component';

@Component({
  selector: 'app-folder-allocation',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    ToastModule,
    DialogModule,
    FolderAllocationDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './folder-allocation.component.html',
  styleUrl: './folder-allocation.component.scss'
})
export class FolderAllocationComponent implements OnInit {
  private service = inject(FolderAllocationService);
  private messageService = inject(MessageService);

  allocations = signal<FolderAllocationItem[]>([]);
  loading = signal<boolean>(false);

  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<FolderAllocationItem | null>(null);
  deleting = signal<boolean>(false);

  showRevokeConfirm = signal<boolean>(false);
  revokeTarget = signal<FolderAllocationItem | null>(null);
  revoking = signal<boolean>(false);

  currentPage = signal<number>(1);
  pageSize = signal<number>(10);
  totalCount = signal<number>(0);
  searchKeyword = signal<string>('');
  selectedStatus = signal<string>('');

  dialogVisible = signal<boolean>(false);
  editingId = signal<string | null>(null);

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  constructor() {
    // Tự động load lại khi thay đổi keyword hoặc status
    effect(() => {
      this.searchKeyword();
      this.selectedStatus();
      this.currentPage.set(1);
    }, { allowSignalWrites: true });

    // Load dữ liệu khi thay đổi các thông số phân trang hoặc bộ lọc
    effect(() => {
      this.loadAllocations();
    });
  }

  ngOnInit(): void {
    // effect sẽ tự động kích hoạt load lần đầu tiên
  }

  loadAllocations(): void {
    this.loading.set(true);
    this.service.getPaged(
      this.currentPage(),
      this.pageSize(),
      this.searchKeyword(),
      this.selectedStatus()
    ).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: (res) => {
        this.allocations.set(res.items || []);
        this.totalCount.set(res.total_count || 0);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách phân bổ nhập liệu từ máy chủ.'
        });
        this.allocations.set([]);
        this.totalCount.set(0);
      }
    });
  }

  openCreate(): void {
    this.editingId.set(null);
    this.dialogVisible.set(true);
  }

  openEdit(item: FolderAllocationItem): void {
    this.editingId.set(item.id);
    this.dialogVisible.set(true);
  }

  onRevoke(item: FolderAllocationItem): void {
    this.revokeTarget.set(item);
    this.showRevokeConfirm.set(true);
  }

  onConfirmRevoke(): void {
    const item = this.revokeTarget();
    if (!item) return;

    this.revoking.set(true);
    this.service.revoke(item.id).pipe(
      finalize(() => this.revoking.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: 'Thu hồi quyền nhập liệu thành công.'
        });
        this.showRevokeConfirm.set(false);
        this.revokeTarget.set(null);
        this.loadAllocations();
      },
      error: (err) => {
        this.showRevokeConfirm.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể thu hồi quyền phân bổ.'
        });
      }
    });
  }

  onCancelRevoke(): void {
    this.showRevokeConfirm.set(false);
    this.revokeTarget.set(null);
  }

  onDelete(item: FolderAllocationItem): void {
    this.deleteTarget.set(item);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete(): void {
    const item = this.deleteTarget();
    if (!item) return;

    this.deleting.set(true);
    this.service.delete(item.id).pipe(
      finalize(() => this.deleting.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Xóa thành công',
          detail: `Đã xóa phân bổ thư mục "${item.folder_name}" thành công!`
        });
        this.showDeleteConfirm.set(false);
        this.deleteTarget.set(null);
        this.loadAllocations();
      },
      error: (err) => {
        this.showDeleteConfirm.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể xóa phân bổ.'
        });
      }
    });
  }

  onCancelDelete(): void {
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${day}/${month}/${year}`;
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(p => p + 1);
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
    }
  }

  goToPage(page: any): void {
    const p = Number(page);
    if (p >= 1 && p <= this.totalPages()) {
      this.currentPage.set(p);
    }
  }

  onPageSizeChange(event: Event): void {
    const target = event.target as HTMLSelectElement;
    this.pageSize.set(Number(target.value));
    this.currentPage.set(1);
  }
}
