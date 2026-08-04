import { Component, OnInit, signal, computed, inject, effect } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { AuthService } from '@sohoa.frontend/shared/core';
import { DocumentTypeService } from '../../data-access/document-type.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-document-type',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    MenuModule,
    SelectModule,
    DialogModule,
    WfBreadcrumbComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './document-type.component.html',
  styleUrl: './document-type.component.scss'
})
export class DocumentTypeComponent implements OnInit {
  private documentTypeService = inject(DocumentTypeService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);

  items = signal<any[]>([]);
  formTemplates = signal<any[]>([]);
  searchKeyword = signal<string>('');
  searchStatus = signal<string>('');
  totalCount = signal<number>(0);

  currentView = signal<'list' | 'add' | 'edit'>('list');
  currentItem = signal<any>({});
  isSaving = signal<boolean>(false);

  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});

  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);
  // Chuẩn hóa tên loại văn bản hiển thị trong popup xóa dùng chung.
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.name ?? '');

  showToggleConfirm = signal<boolean>(false);
  toggleTarget = signal<any>(null);
  toggling = signal<boolean>(false);

  codeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().code) return 'Mã loại văn bản là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });

  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().name) return 'Tên loại văn bản là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  paginatedItems = computed(() => this.items());

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  canCreate = computed(() => this.authService.hasPermission('DOCUMENT_TYPE_CREATE') || this.authService.hasPermission('SUPER_ADMIN'));
  canEdit = computed(() => this.authService.hasPermission('DOCUMENT_TYPE_EDIT') || this.authService.hasPermission('SUPER_ADMIN'));
  canDelete = computed(() => this.authService.hasPermission('DOCUMENT_TYPE_DELETE') || this.authService.hasPermission('SUPER_ADMIN'));
  canManage = computed(() => this.authService.hasPermission('DOCUMENT_TYPE_MANAGE') || this.authService.hasPermission('SUPER_ADMIN'));
  actionMenuItems: MenuItem[] = [];

  openActionMenu(item: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    const active = item.isActive === 1 || item.isActive === true;
    this.actionMenuItems = [
      ...(this.canManage() ? [{
        label: active ? 'Khóa loại văn bản' : 'Mở khóa loại văn bản',
        title: active ? 'Khóa loại văn bản' : 'Mở khóa loại văn bản',
        icon: active ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal',
        command: () => this.onToggleStatus(item)
      }] : []),
      ...(this.canEdit() ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(item) }] : []),
      ...(this.canDelete() ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(item) }] : []),
    ];
    menu.toggle(event);
  }

  constructor() {
    effect(() => {
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
    this.currentItem.update(item => ({ ...item }));
    this.serverErrors.update(errs => {
      const copy = { ...errs };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }

  loadFormTemplates() {
    this.documentTypeService.getEavFormTemplatesLookup().subscribe({
      next: (data) => {
        this.formTemplates.set(Array.isArray(data) ? data : []);
      },
      error: () => {
        this.formTemplates.set([]);
      }
    });
  }

  loadItems() {
    this.documentTypeService.getDocumentTypes(
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
          detail: 'Không thể tải danh sách loại văn bản'
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
      isEquipmentProfile: false,
      isFactoryAcceptanceReport: false,
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
    this.serverErrors.set({});
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
      isActive: item.isActive,
      isEquipmentProfile: item.isEquipmentProfile || false,
      isFactoryAcceptanceReport: item.isFactoryAcceptanceReport || false
    };

    const request$ = this.currentView() === 'add'
      ? this.documentTypeService.createDocumentType(payload)
      : this.documentTypeService.updateDocumentType(item.id, payload);

    request$.pipe(
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: this.currentView() === 'add' ? 'Đã thêm mới loại văn bản thành công!' : 'Đã cập nhật loại văn bản thành công!'
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
            } catch {
              // ignore parse error
            }
          }
        } else if (err?.errors) {
          errorsObj = err.errors;
        }
        this.serverErrors.set(errorsObj);

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
    this.toggleTarget.set(item);
    this.showToggleConfirm.set(true);
  }

  onConfirmToggle() {
    const item = this.toggleTarget();
    if (!item) return;
    const isLocking = item.isActive === 1 || item.isActive === true;
    this.toggling.set(true);
    this.documentTypeService.toggleStatus(item.id, isLocking)
      .pipe(finalize(() => this.toggling.set(false)))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: res.message || (isLocking ? 'Khóa thành công!' : 'Mở khóa thành công!')
          });
          this.showToggleConfirm.set(false);
          this.toggleTarget.set(null);
          this.loadItems();
        },
        error: (err) => {
          this.showToggleConfirm.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể cập nhật trạng thái loại văn bản.'
          });
        }
      });
  }

  onCancelToggle() {
    this.showToggleConfirm.set(false);
    this.toggleTarget.set(null);
  }

  onDelete(item: any) {
    this.deleteTarget.set(item);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete() {
    const item = this.deleteTarget();
    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!item || this.deleting()) return;

    this.deleting.set(true);
    this.documentTypeService.deleteDocumentType(item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: `Đã xóa loại văn bản "${item.name}" thành công!`
          });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadItems();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể xóa loại văn bản.'
          });
        }
      });
  }

  onCancelDelete() {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.deleting()) return;

    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }
}
