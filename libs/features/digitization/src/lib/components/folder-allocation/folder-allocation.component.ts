import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { MenuItem, MessageService } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { finalize } from 'rxjs';
import { FolderAllocationService, FolderAllocationItem } from '../../data-access/folder-allocation.service';
import { FolderAllocationDialogComponent } from './folder-allocation-dialog.component';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-folder-allocation',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    ToastModule,
    DialogModule,
    MenuModule,
    FolderAllocationDialogComponent,
    WfBreadcrumbComponent,
  ],
  providers: [MessageService],
  templateUrl: './folder-allocation.component.html',
  styleUrl: './folder-allocation.component.scss'
})
export class FolderAllocationComponent implements OnInit {
  private service = inject(FolderAllocationService);
  private messageService = inject(MessageService);
  public authService = inject(AuthService);

  allocations = signal<FolderAllocationItem[]>([]);
  loading = signal<boolean>(false);

  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<FolderAllocationItem | null>(null);
  deleting = signal<boolean>(false);

  showRevokeConfirm = signal<boolean>(false);
  revokeTarget = signal<FolderAllocationItem | null>(null);
  revoking = signal<boolean>(false);

  showReactivateConfirm = signal<boolean>(false);
  reactivateTarget = signal<FolderAllocationItem | null>(null);
  reactivating = signal<boolean>(false);

  currentPage = signal<number>(1);
  pageSize = signal<number>(10);
  totalCount = signal<number>(0);
  searchKeyword = signal<string>('');
  selectedStatus = signal<string>('');

  dialogVisible = signal<boolean>(false);
  editingId = signal<string | null>(null);

  actionMenuItems: MenuItem[] = [];
  selectedActionItem: FolderAllocationItem | null = null;

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

  openActionMenu(item: FolderAllocationItem, event: MouseEvent, menu: Menu): void {
    this.selectedActionItem = item;
    const canManage = this.authService.hasPermission('FOLDER_ALLOCATION_EDIT')
      || this.authService.hasPermission('FOLDER_ALLOCATION_MANAGE');
    const items: MenuItem[] = [];

    if (canManage) {
      items.push({ label: 'Chỉnh sửa phân bổ', title: 'Chỉnh sửa phân bổ', icon: 'pi pi-pencil color-blue', command: () => this.openEdit(item) });
    }
    if (item.status === 'Active' && canManage) {
      items.push({ label: 'Thu hồi quyền phân bổ', title: 'Thu hồi quyền phân bổ', icon: 'pi pi-ban color-teal', command: () => this.onRevoke(item) });
    }
    if (item.status !== 'Active' && canManage) {
      items.push({ label: 'Phân bổ thư mục', title: 'Phân bổ thư mục', icon: 'pi pi-check-circle color-teal', command: () => this.onReactivate(item) });
    }
    if (this.authService.hasPermission('FOLDER_ALLOCATION_DELETE')
      || this.authService.hasPermission('FOLDER_ALLOCATION_MANAGE')) {
      items.push({ label: 'Xóa phân bổ', title: 'Xóa phân bổ', icon: 'pi pi-trash color-red', command: () => this.onDelete(item) });
    }

    this.actionMenuItems = items;
    menu.toggle(event);
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

  onReactivate(item: FolderAllocationItem): void {
    this.reactivateTarget.set(item);
    this.showReactivateConfirm.set(true);
  }

  onConfirmReactivate(): void {
    const item = this.reactivateTarget();
    if (!item) return;

    this.reactivating.set(true);
    this.service.reactivate(item.id).pipe(
      finalize(() => this.reactivating.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: 'Phân bổ thư mục thành công.'
        });
        this.showReactivateConfirm.set(false);
        this.reactivateTarget.set(null);
        this.loadAllocations();
      },
      error: (err) => {
        this.showReactivateConfirm.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể phân bổ thư mục.'
        });
      }
    });
  }

  onCancelReactivate(): void {
    this.showReactivateConfirm.set(false);
    this.reactivateTarget.set(null);
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
