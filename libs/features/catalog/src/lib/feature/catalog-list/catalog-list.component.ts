import { Component, OnInit, signal, computed, inject, effect } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { AuthService } from '@sohoa.frontend/shared/core';
import { CatalogService } from '../../data-access/catalog.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-catalog-list',
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
  templateUrl: './catalog-list.component.html'
})
export class CatalogListComponent implements OnInit {
  private catalogService = inject(CatalogService);
  private route = inject(ActivatedRoute);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);

  // Catalog configuration from route data
  catalogType = signal<string>('');
  catalogTitle = signal<string>('');

  items = signal<any[]>([]);
  parentsList = signal<any[]>([]);
  organizationUnits = signal<any[]>([]);
  searchKeyword = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'
  searchUnitId = signal<number | null>(null);
  totalCount = signal<number>(0);

  currentView = signal<'list' | 'add' | 'edit'>('list');
  currentItem = signal<any>({});
  isPrivate = signal<boolean>(false);
  isSaving = signal<boolean>(false);

  catalogTypes = signal<any[]>([]);
  actionMenuItems: MenuItem[] = [];

  openActionMenu(item: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    const active = item.status === 1;
    this.actionMenuItems = [
      ...(this.canManage() ? [{
        label: active ? 'Khóa danh mục' : 'Mở khóa danh mục',
        title: active ? 'Khóa danh mục' : 'Mở khóa danh mục',
        icon: active ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal',
        command: () => this.onToggleStatus(item)
      }] : []),
      ...(this.canEdit() ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(item) }] : []),
      ...(this.canDelete() ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(item) }] : []),
    ];
    menu.toggle(event);
  }

  // Delete confirmation
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);
  // Ghép tên và mã để giữ đầy đủ ngữ cảnh trong popup xóa dùng chung.
  readonly deleteTargetLabel = computed(() => {
    const target = this.deleteTarget();

    if (!target) return '';

    return target.code ? `${target.name} (${target.code})` : target.name;
  });

  // Lock/Unlock confirmation
  showStatusConfirm = signal<boolean>(false);
  statusTarget = signal<any>(null);
  togglingStatus = signal<boolean>(false);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Form Validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});
  codeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().code) return 'Mã danh mục là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });
  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().name) return 'Tên danh mục là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });
  unitError = computed(() => {
    if (this.isPhongCatalog() && this.formSubmitted() && !this.currentItem().unitId) return 'Đơn vị là bắt buộc';
    return this.serverErrors().unitId || this.serverErrors().UnitId || '';
  });

  onFieldChange(field: string) {
    this.serverErrors.update(errs => {
      const copy = { ...errs };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }

  // Check if this catalog type supports hierarchy (Parent-Child)
  hasParent = computed(() => {
    const type = this.catalogType();
    const typeObj = this.catalogTypes().find(t => t.code === type);
    return typeObj ? typeObj.hasParent === 1 : false;
  });

  isPhongCatalog = computed(() => this.catalogType() === 'PHONG');
  customBreadcrumbItems = computed(() => this.isPhongCatalog()
    ? [
        { label: 'Quản lý danh mục' },
        { label: 'Danh mục phông' }
      ]
    : null);

  // Paginated items
  paginatedItems = computed(() => {
    return this.items();
  });

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

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

  // Map catalogType → permission prefix (mỗi controller riêng = 1 nhóm quyền riêng)
  private readonly PERMISSION_PREFIX_MAP: Record<string, string> = {
    KE: 'SHELF',
    TANG: 'FLOOR',
    HOP: 'BOX',
    CHUC_VU: 'POSITION',
    PROCESSING_CATEGORY: 'PROCESSING_CATEGORY',
    LINH_VUC: 'DOMAIN',
    TINH_TRANG_VAT_LY: 'PHYSICAL_STATUS',
  };

  permissionPrefix = computed(() =>
    this.PERMISSION_PREFIX_MAP[this.catalogType()] ?? 'CATALOG'
  );

  // Fine-grained permission computed signals
  canCreate = computed(() => this.authService.hasPermission(`${this.permissionPrefix()}_CREATE`));
  canEdit = computed(() => this.authService.hasPermission(`${this.permissionPrefix()}_EDIT`));
  canDelete = computed(() => this.authService.hasPermission(`${this.permissionPrefix()}_DELETE`));
  canManage = computed(() => this.catalogType() === 'PROCESSING_CATEGORY'
    ? this.authService.hasPermission('PROCESSING_CATEGORY_EDIT')
    : this.authService.hasPermission(`${this.permissionPrefix()}_MANAGE`));

  constructor() {
    // Listen to changes in route data to reload catalog configurations
    this.route.data.subscribe(data => {
      this.catalogType.set(data['type'] || '');
      this.catalogTitle.set(data['title'] || 'Danh mục');
      this.currentView.set('list');
      this.searchKeyword.set('');
      this.searchStatus.set('');
      this.searchUnitId.set(null);
      this.currentPage.set(1);
      if ((data['type'] || '') === 'PHONG') {
        this.loadOrganizationUnits();
      }
      
      // Load types first if not loaded
      if (this.catalogTypes().length === 0) {
        this.loadCatalogTypes(() => this.loadItems());
      }
    });

    effect(() => {
      const type = this.catalogType();
      const page = this.currentPage();
      const size = this.pageSize();
      if (type) {
        this.loadItems();
      }
    }, { allowSignalWrites: true });
  }

  ngOnInit() {
    this.authService.loadPermissions();
    if (this.catalogTypes().length === 0) {
      this.loadCatalogTypes(() => this.loadItems());
    }
    if (this.isPhongCatalog()) {
      this.loadOrganizationUnits();
    }
  }

  loadCatalogTypes(callback?: () => void) {
    this.catalogService.getCatalogTypes().subscribe({
      next: (types) => {
        this.catalogTypes.set(Array.isArray(types) ? types : (types && Array.isArray((types as any).items) ? (types as any).items : (types && Array.isArray((types as any).value) ? (types as any).value : [])));
        if (callback) callback();
      },
      error: (err) => {
        console.error('Không thể tải danh sách loại danh mục từ BE.', err);
        if (callback) callback();
      }
    });
  }

  loadItems() {
    const type = this.catalogType();
    if (!type) return;

    if (this.isPhongCatalog()) {
      const typeObj = this.catalogTypes().find(t => t.code === type);
      if (!typeObj?.id) return;

      this.catalogService.getItemsByTypeId(
        typeObj.id,
        this.currentPage(),
        this.pageSize(),
        this.searchKeyword(),
        this.searchStatus(),
        this.searchUnitId()
      ).subscribe({
        next: (res) => {
          this.items.set(res?.items || []);
          this.totalCount.set(res?.totalCount || 0);
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi tải dữ liệu',
            detail: 'Không thể tải danh sách danh mục.'
          });
          this.items.set([]);
          this.totalCount.set(0);
        }
      });
      return;
    }

    this.catalogService.getItems(type, this.currentPage(), this.pageSize(), this.searchKeyword(), this.searchStatus()).subscribe({
      next: (res) => {
        const list = res?.items || [];
        this.items.set(list);
        this.totalCount.set(res?.totalCount || 0);
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi tải dữ liệu',
          detail: 'Không thể tải danh sách danh mục.'
        });
        this.items.set([]);
        this.totalCount.set(0);
      }
    });
  }

  loadOrganizationUnits() {
    this.catalogService.getOrganizationUnits().subscribe({
      next: (units) => this.organizationUnits.set(Array.isArray(units) ? units : []),
      error: () => this.organizationUnits.set([])
    });
  }

  getParentName(parentId: number): string {
    const parent = this.items().find(item => item.id === parentId);
    return parent ? parent.name : '';
  }

  loadParentsList(excludeId?: number) {
    const type = this.catalogType();
    // Fetch only active items (status = 1) of the same catalog type using page=1 and pageSize=9999 (all)
    this.catalogService.getItems(type, 1, 9999, undefined, '1').subscribe({
      next: (data) => {
        let list = data?.items || [];
        if (excludeId) {
          list = list.filter((item: any) => item.id !== excludeId);
        }
        this.parentsList.set(list);
      },
      error: () => {
        this.parentsList.set([]);
      }
    });
  }

  onSearch() {
    this.currentPage.set(1);
    this.loadItems();
  }

  onSearchUnitChange(unitId: number | string | null) {
    const normalizedUnitId = unitId === null || unitId === '' ? null : Number(unitId);
    this.searchUnitId.set(normalizedUnitId);
    this.onSearch();
  }

  onResetSearch() {
    this.searchKeyword.set('');
    this.searchStatus.set('');
    this.searchUnitId.set(null);
    this.currentPage.set(1);
    this.loadItems();
  }

  onAddNew() {
    if (!this.canCreate()) return;
    this.isPrivate.set(false);
    this.currentItem.set({
      code: '',
      name: '',
      catalogType: this.catalogType(),
      parentId: null,
      description: '',
      priority: 1,
      status: 1,
      unitId: null,
      catalogTypeId: this.catalogTypes().find(t => t.code === this.catalogType())?.id
    });
    if (this.hasParent()) {
      this.loadParentsList();
    }
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('add');
  }

  onEdit(item: any) {
    if (!this.canEdit()) return;
    this.isPrivate.set(!!item.unitId);
    this.currentItem.set({ ...item });
    if (this.hasParent()) {
      this.loadParentsList(item.id);
    }
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('edit');
  }

  onSaveItem() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});
    if (this.codeError() || this.nameError() || this.unitError()) {
      return;
    }

    const itemDraft = this.currentItem();

    if (itemDraft.priority === undefined || itemDraft.priority === null) {
      itemDraft.priority = 1;
    }

    if (this.isPhongCatalog()) {
      itemDraft.catalogTypeId = this.catalogTypes().find(t => t.code === this.catalogType())?.id;
      itemDraft.unitId = Number(itemDraft.unitId);
    } else {
      itemDraft.unitId = this.isPrivate() ? -1 : null;
    }
    this.isSaving.set(true);

    if (this.currentView() === 'edit') {
      this.catalogService.updateItem(itemDraft.id, itemDraft, this.catalogType()).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: 'Cập nhật',
            detail: 'Cập nhật danh mục thành công!'
          });
          this.loadItems();
          this.currentView.set('list');
        },
        error: (err) => {
          this.isSaving.set(false);
          let errorsObj = {};
          if (err?.error) {
            if (typeof err.error === 'object') {
              errorsObj = err.error.errors || err.error;
            } else if (typeof err.error === 'string') {
              try {
                const parsed = JSON.parse(err.error);
                errorsObj = parsed.errors || parsed;
              } catch (e) {
                // ignore
              }
            }
          } else if (err?.errors) {
            errorsObj = err.errors;
          }
          this.serverErrors.set(errorsObj);
          const errorMsg = err.error?.message || err.message || 'Không thể cập nhật danh mục.';
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi cập nhật',
            detail: errorMsg
          });
        }
      });
    } else {
      this.catalogService.createItem(itemDraft, this.catalogType()).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: 'Thêm mới',
            detail: 'Thêm mới danh mục thành công!'
          });
          this.loadItems();
          this.currentView.set('list');
        },
        error: (err) => {
          this.isSaving.set(false);
          let errorsObj = {};
          if (err?.error) {
            if (typeof err.error === 'object') {
              errorsObj = err.error.errors || err.error;
            } else if (typeof err.error === 'string') {
              try {
                const parsed = JSON.parse(err.error);
                errorsObj = parsed.errors || parsed;
              } catch (e) {
                // ignore
              }
            }
          } else if (err?.errors) {
            errorsObj = err.errors;
          }
          this.serverErrors.set(errorsObj);
          const errorMsg = err.error?.message || err.message || 'Thêm mới danh mục thất bại.';
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi thêm mới',
            detail: errorMsg
          });
        }
      });
    }
  }

  onDelete(item: any) {
    if (!this.canDelete()) return;
    this.deleteTarget.set(item);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete() {
    const item = this.deleteTarget();
    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!item || this.deleting()) return;

    this.deleting.set(true);
    this.catalogService.deleteItem(item.id, this.catalogType())
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
      next: () => {
        this.showDeleteConfirm.set(false);
        this.deleteTarget.set(null);
        this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: `Đã xóa "${item.name}" thành công!` });
        this.loadItems();
      },
      error: (err) => {
        const errorMsg = err.error?.message || 'Xóa danh mục thất bại.';
        this.messageService.add({ severity: 'error', summary: 'Lỗi xóa', detail: errorMsg });
      }
    });
  }

  onCancelDelete() {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.deleting()) return;

    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  onToggleStatus(item: any) {
    if (!this.canManage()) return;
    this.statusTarget.set(item);
    this.showStatusConfirm.set(true);
  }

  onConfirmToggleStatus() {
    const item = this.statusTarget();
    if (!item) return;
    const isLocking = item.status === 1;
    this.togglingStatus.set(true);
    this.catalogService.toggleStatus(item.id, isLocking, this.catalogType()).subscribe({
      next: (res: any) => {
        this.togglingStatus.set(false);
        this.showStatusConfirm.set(false);
        this.statusTarget.set(null);
        this.messageService.add({
          severity: 'success',
          summary: isLocking ? 'Đã khóa' : 'Đã mở khóa',
          detail: res.message || 'Thay đổi trạng thái danh mục thành công!'
        });
        this.loadItems();
      },
      error: (err) => {
        this.togglingStatus.set(false);
        this.showStatusConfirm.set(false);
        const errorMsg = err.error?.message || 'Không thể thay đổi trạng thái danh mục.';
        this.messageService.add({ severity: 'error', summary: 'Lỗi thao tác', detail: errorMsg });
      }
    });
  }

  onCancelToggleStatus() {
    this.showStatusConfirm.set(false);
    this.statusTarget.set(null);
  }

  getUnitLabel(unitId: number | string | null | undefined): string {
    if (!unitId) return '';
    const unit = this.organizationUnits().find(x => Number(x.id) === Number(unitId));
    return unit?.name || '';
  }
}
