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
import { MenuItem, MessageService } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { AuthService } from '@sohoa.frontend/shared/core';
import { EquipmentTypeService } from '../../data-access/equipment-type.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-equipment-type',
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
  templateUrl: './equipment-type.component.html',
  styleUrls: ['./equipment-type.component.css']
})
export class EquipmentTypeComponent implements OnInit {
  private equipmentTypeService = inject(EquipmentTypeService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);

  // States
  items = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  searchCode = signal<string>('');
  searchName = signal<string>('');
  searchGridTypeId = signal<string>(''); // dropdown string or number
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
    if (this.formSubmitted() && !this.currentItem().code) return 'Mã loại thiết bị là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });

  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().name) return 'Tên loại thiết bị là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  gridTypeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().gridTypeId) return 'Loại lưới điện là bắt buộc';
    return this.serverErrors().gridTypeId || this.serverErrors().GridTypeId || '';
  });

  // Delete Confirmation Dialog Signals
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);
  // Chuẩn hóa tên loại thiết bị hiển thị trong popup xóa dùng chung.
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.name ?? '');
  actionMenuItems: MenuItem[] = [];

  openActionMenu(item: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    const active = item.isActive === 1 || item.isActive === true;
    this.actionMenuItems = [
      ...(this.canEdit() ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(item) }] : []),
      ...(this.canManage() ? [{
        label: active ? 'Khóa loại thiết bị' : 'Mở khóa loại thiết bị',
        title: active ? 'Khóa loại thiết bị' : 'Mở khóa loại thiết bị',
        icon: active ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal',
        command: () => this.onToggleStatus(item)
      }] : []),
      ...(this.canDelete() ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(item) }] : []),
    ];
    menu.toggle(event);
  }

  // Lock/Unlock Confirmation Dialog Signals
  showStatusConfirm = signal<boolean>(false);
  statusTarget = signal<any>(null);
  togglingStatus = signal<boolean>(false);

  // Pagination Computeds
  paginatedItems = computed(() => {
    return this.items();
  });

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

  // Permission Computeds
  canCreate = computed(() => this.authService.hasPermission('EQUIPMENT_TYPE_CREATE') || this.authService.hasPermission('SUPER_ADMIN'));
  canEdit = computed(() => this.authService.hasPermission('EQUIPMENT_TYPE_EDIT') || this.authService.hasPermission('SUPER_ADMIN'));
  canDelete = computed(() => this.authService.hasPermission('EQUIPMENT_TYPE_DELETE') || this.authService.hasPermission('SUPER_ADMIN'));
  canManage = computed(() => this.authService.hasPermission('EQUIPMENT_TYPE_MANAGE') || this.authService.hasPermission('SUPER_ADMIN'));
  canSaveCurrentView = computed(() =>
    (this.currentView() === 'add' && this.canCreate()) ||
    (this.currentView() === 'edit' && this.canEdit())
  );

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
    this.loadGridTypes();
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

  loadGridTypes() {
    this.equipmentTypeService.getGridTypesLookup().subscribe({
      next: (data) => {
        this.gridTypes.set(Array.isArray(data) ? data : []);
      },
      error: () => {
        console.error('Không thể tải danh sách lưới điện');
      }
    });
  }

  loadItems() {
    const gridTypeId = this.searchGridTypeId() ? Number(this.searchGridTypeId()) : undefined;
    const isActive = this.searchStatus() !== '' ? this.searchStatus() === '1' : undefined;

    this.equipmentTypeService.getEquipmentTypes(
      this.currentPage(),
      this.pageSize(),
      this.searchCode(),
      this.searchName(),
      gridTypeId,
      isActive
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
          detail: 'Không thể tải danh sách loại thiết bị'
        });
      }
    });
  }  onSearch() {
    this.currentPage.set(1);
    this.loadItems();
  }

  onResetSearch() {
    this.searchCode.set('');
    this.searchName.set('');
    this.searchGridTypeId.set('');
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
      gridTypeId: null,
      sortOrder: 1
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

    if (!item.code || !item.name || !item.gridTypeId) {
      return;
    }

    this.isSaving.set(true);

    const payload = {
      id: item.id,
      code: item.code.trim(),
      name: item.name.trim(),
      description: item.description ? item.description.trim() : '',
      gridTypeId: Number(item.gridTypeId),
      sortOrder: item.sortOrder || 1,
      isActive: item.isActive === true || item.isActive === 1
    };

    const request$ = this.currentView() === 'add'
      ? this.equipmentTypeService.create(payload)
      : this.equipmentTypeService.update(item.id, payload);

    request$.pipe(
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: this.currentView() === 'add' ? 'Đã thêm mới loại thiết bị thành công!' : 'Đã cập nhật loại thiết bị thành công!'
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
    this.statusTarget.set(item);
    this.showStatusConfirm.set(true);
  }

  onConfirmToggleStatus() {
    const item = this.statusTarget();
    if (!item) return;

    const isLocking = item.isActive === 1 || item.isActive === true;
    this.togglingStatus.set(true);
    this.equipmentTypeService.toggleStatus(item.id, isLocking)
      .pipe(finalize(() => this.togglingStatus.set(false)))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: res.message || (isLocking ? 'Khóa loại thiết bị thành công!' : 'Mở khóa loại thiết bị thành công!')
          });
          this.showStatusConfirm.set(false);
          this.statusTarget.set(null);
          this.loadItems();
        },
        error: (err) => {
          this.showStatusConfirm.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể cập nhật trạng thái loại thiết bị.'
          });
        }
      });
  }

  onCancelToggleStatus() {
    this.showStatusConfirm.set(false);
    this.statusTarget.set(null);
  }

  // Custom Delete Confirm Logic
  onDelete(item: any) {
    this.deleteTarget.set(item);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete() {
    const item = this.deleteTarget();
    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!item || this.deleting()) return;

    this.deleting.set(true);
    this.equipmentTypeService.delete(item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: `Đã xóa loại thiết bị "${item.name}" thành công!`
          });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadItems();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể xóa loại thiết bị.'
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

  goBack() {
    this.currentView.set('list');
  }
}
