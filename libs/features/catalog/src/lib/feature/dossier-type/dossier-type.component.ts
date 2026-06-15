import { Component, OnInit, signal, computed, inject, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { AuthService } from '@sohoa.frontend/shared/core';
import { DossierTypeService } from '../../data-access/dossier-type.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-dossier-type',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, SelectModule, DialogModule],
  providers: [MessageService],
  templateUrl: './dossier-type.component.html'
})
export class DossierTypeComponent implements OnInit {
  private dossierTypeService = inject(DossierTypeService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);

  // States
  items = signal<any[]>([]);
  formTemplates = signal<any[]>([]);
  searchKeyword = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'
  totalCount = signal<number>(0);

  currentView = signal<'list' | 'add' | 'edit'>('list');
  currentItem = signal<any>({});
  isSaving = signal<boolean>(false);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Form Validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});

  codeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().code) return 'Mã loại hồ sơ là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });

  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().name) return 'Tên loại hồ sơ là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  // Delete Confirmation Dialog Signals
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);

  // Pagination Computeds
  paginatedItems = computed(() => {
    return this.items();
  });

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

  // Permission Computeds
  canCreate = computed(() => this.authService.hasPermission('DOSSIER_TYPE_CREATE') || this.authService.hasPermission('SUPER_ADMIN'));
  canEdit = computed(() => this.authService.hasPermission('DOSSIER_TYPE_EDIT') || this.authService.hasPermission('SUPER_ADMIN'));
  canDelete = computed(() => this.authService.hasPermission('DOSSIER_TYPE_DELETE') || this.authService.hasPermission('SUPER_ADMIN'));
  canManage = computed(() => this.authService.hasPermission('DOSSIER_TYPE_MANAGE') || this.authService.hasPermission('SUPER_ADMIN'));

  constructor() {
    effect(() => {
      // Re-trigger load when page or pageSize changes
      this.currentPage();
      this.pageSize();
      this.loadItems();
    }, { allowSignalWrites: true });
  }

  ngOnInit() {
    this.authService.loadPermissions();
    this.loadFormTemplates();
    this.loadItems();
  }

  onFieldChange(field: string) {
    this.serverErrors.update(errs => {
      const copy = { ...errs };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }

  loadFormTemplates() {
    this.dossierTypeService.getEavFormTemplates().subscribe({
      next: (data) => {
        this.formTemplates.set(Array.isArray(data) ? data : []);
      },
      error: () => {
        console.error('Không thể tải danh sách biểu mẫu EAV');
      }
    });
  }

  loadItems() {
    this.dossierTypeService.getDossierTypes(
      this.currentPage(),
      this.pageSize(),
      this.searchKeyword(),
      this.searchStatus()
    ).subscribe({
      next: (res) => {
        if (res) {
          this.items.set(res.items || []);
          this.totalCount.set(res.totalCount || 0);
        }
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách loại hồ sơ'
        });
      }
    });
  }

  onSearch() {
    this.currentPage.set(1);
    this.loadItems();
  }

  onResetSearch() {
    this.searchKeyword.set('');
    this.searchStatus.set('');
    this.currentPage.set(1);
    this.loadItems();
  }

  nextPage() {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(p => p + 1);
    }
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
    }
  }

  goToPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.totalPages()) {
      this.currentPage.set(p);
    }
  }

  onPageSizeChange(event: any) {
    this.pageSize.set(Number(event.target.value));
    this.currentPage.set(1);
  }

  onAddNew() {
    this.currentItem.set({
      isActive: true,
      piority: 1,
      formId: null
    });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('add');
  }

  onEdit(item: any) {
    this.currentItem.set({ ...item });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('edit');
  }

  onSaveItem() {
    this.formSubmitted.set(true);
    const item = this.currentItem();

    if (!item.code || !item.name) {
      return;
    }

    this.isSaving.set(true);

    const payload = {
      id: item.id,
      code: item.code.trim(),
      name: item.name.trim(),
      formId: item.formId || null,
      piority: item.piority || 1,
      isActive: item.isActive
    };

    const request$ = this.currentView() === 'add'
      ? this.dossierTypeService.createDossierType(payload)
      : this.dossierTypeService.updateDossierType(item.id, payload);

    request$.pipe(
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: this.currentView() === 'add' ? 'Đã thêm mới loại hồ sơ thành công!' : 'Đã cập nhật loại hồ sơ thành công!'
        });
        this.currentView.set('list');
        this.loadItems();
      },
      error: (err) => {
        let errorsObj = {};
        if (err?.error) {
          if (typeof err.error === 'object') {
            errorsObj = err.error.errors || err.error;
          } else if (typeof err.error === 'string') {
            try {
              const parsed = JSON.parse(err.error);
              errorsObj = parsed.errors || parsed;
            } catch (e) {
              // Ignore parse error
            }
          }
        } else if (err?.errors) {
          errorsObj = err.errors;
        }
        this.serverErrors.set(errorsObj);
        
        // Show validation/error message
        const errMsg = err?.error?.message || 'Có lỗi xảy ra khi lưu thông tin.';
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: errMsg
        });
      }
    });
  }

  onToggleStatus(item: any) {
    const isLocking = item.isActive === 1 || item.isActive === true;
    this.dossierTypeService.toggleStatus(item.id, isLocking).subscribe({
      next: (res) => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: res.message || (isLocking ? 'Khóa thành công!' : 'Mở khóa thành công!')
        });
        this.loadItems();
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể cập nhật trạng thái loại hồ sơ.'
        });
      }
    });
  }

  // Custom Delete Confirm Logic
  onDelete(item: any) {
    this.deleteTarget.set(item);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete() {
    const item = this.deleteTarget();
    if (!item) return;

    this.deleting.set(true);
    this.dossierTypeService.deleteDossierType(item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: `Đã xóa loại hồ sơ "${item.name}" thành công!`
          });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadItems();
        },
        error: (err) => {
          this.showDeleteConfirm.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể xóa loại hồ sơ.'
          });
        }
      });
  }

  onCancelDelete() {
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }
}
