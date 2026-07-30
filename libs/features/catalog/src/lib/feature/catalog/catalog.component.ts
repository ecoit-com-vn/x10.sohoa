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
import { MenuItem } from 'primeng/api';
import { MessageService } from 'primeng/api';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '@sohoa.frontend/shared/core';
import { CatalogService } from '../../data-access/catalog.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-catalog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    SelectModule,
    DialogModule,
    MenuModule,
    WfBreadcrumbComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './catalog.component.html',
  styleUrl: './catalog.component.css'
})
export class CatalogComponent implements OnInit {
  private catalogService = inject(CatalogService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private route = inject(ActivatedRoute);

  readonly catalogBreadcrumbItems = (() => {
    const selectedTypeCode = this.route.snapshot.data['selectedTypeCode'];
    if (selectedTypeCode === 'PHONG') {
      return [{ label: 'Quản lý danh mục' }, { label: 'Danh mục phông' }];
    }
    if (selectedTypeCode === 'MUC_LUC') {
      return [{ label: 'Quản lý danh mục' }, { label: 'Danh mục mục lục hồ sơ' }];
    }
    return null;
  })();

  // Mode state
  isPrivate = signal<boolean>(false);

  // Left Panel - Catalog Types State
  types = signal<any[]>([]);
  typeSearchKeyword = signal<string>('');
  
  selectedTypeId = signal<number | null>(null);
  selectedTypeCode = signal<string>('');
  selectedTypeName = signal<string>('');
  selectedTypeHasParent = signal<number>(0);
  actionMenuItems: MenuItem[] = [];

  // Catalog Type Form Dialog
  showTypeDialog = signal<boolean>(false);
  isEditingType = signal<boolean>(false);
  isViewingType = signal<boolean>(false);
  currentTypeItem = signal<any>({});
  typeFormSubmitted = signal<boolean>(false);
  typeServerErrors = signal<any>({});
  typeSaving = signal<boolean>(false);

  // Catalog Type Delete Confirmation
  showTypeDeleteConfirm = signal<boolean>(false);
  typeDeleteTarget = signal<any>(null);
  typeDeleting = signal<boolean>(false);
  // Chuẩn hóa tên loại danh mục hiển thị trong popup xóa dùng chung.
  readonly typeDeleteTargetLabel = computed(() => this.typeDeleteTarget()?.name ?? '');

  // Right Panel - Child Catalogs State
  items = signal<any[]>([]);
  parentsList = signal<any[]>([]);
  organizationUnits = signal<any[]>([]);
  searchKeyword = signal<string>('');
  catalogSearchCode = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'
  catalogSearchUnitId = signal<number | null>(null);
  totalCount = signal<number>(0);

  // Right Panel Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Right Panel Catalog Form Dialog
  currentCatalogView = signal<'list' | 'add' | 'edit' | 'view'>('list');
  currentCatalogItem = signal<any>({});
  catalogFormSubmitted = signal<boolean>(false);
  catalogServerErrors = signal<any>({});
  catalogSaving = signal<boolean>(false);

  // Right Panel Catalog Delete Confirmation
  showCatalogDeleteConfirm = signal<boolean>(false);
  catalogDeleteTarget = signal<any>(null);
  catalogDeleting = signal<boolean>(false);
  // Chuẩn hóa tên danh mục hiển thị trong popup xóa dùng chung.
  readonly catalogDeleteTargetLabel = computed(() => this.catalogDeleteTarget()?.name ?? '');

  // Lock/Unlock Confirmation for Catalog Type
  showTypeLockConfirm = signal<boolean>(false);
  typeLockTarget = signal<any>(null);
  typeLockLoading = signal<boolean>(false);

  // Lock/Unlock Confirmation for Catalog Item
  showCatalogLockConfirm = signal<boolean>(false);
  catalogLockTarget = signal<any>(null);
  catalogLockLoading = signal<boolean>(false);

  // Catalog Type Form Client Validation
  typeCodeError = computed(() => {
    if (this.typeFormSubmitted() && !this.currentTypeItem().code) return 'Mã loại danh mục là bắt buộc';
    return this.typeServerErrors().code || this.typeServerErrors().Code || '';
  });
  typeNameError = computed(() => {
    if (this.typeFormSubmitted() && !this.currentTypeItem().name) return 'Tên loại danh mục là bắt buộc';
    return this.typeServerErrors().name || this.typeServerErrors().Name || '';
  });

  // Catalog Form Client Validation
  catalogCodeError = computed(() => {
    if (this.catalogFormSubmitted() && !this.currentCatalogItem().code) return 'Mã danh mục là bắt buộc';
    return this.catalogServerErrors().code || this.catalogServerErrors().Code || '';
  });
  catalogNameError = computed(() => {
    if (this.catalogFormSubmitted() && !this.currentCatalogItem().name) return 'Tên danh mục là bắt buộc';
    return this.catalogServerErrors().name || this.catalogServerErrors().Name || '';
  });
  catalogUnitError = computed(() => {
    if (this.isUnitScopedSelected() && this.catalogFormSubmitted() && !this.currentCatalogItem().unitId) return 'Đơn vị là bắt buộc';
    return this.catalogServerErrors().unitId || this.catalogServerErrors().UnitId || '';
  });

  // Catalog Type permissions
  canCreateType = computed(() => this.authService.hasPermission(this.isPrivate() ? 'PRIVATE_CATALOG_CREATE' : 'SHARED_CATALOG_CREATE'));
  canEditType = computed(() => this.authService.hasPermission(this.isPrivate() ? 'PRIVATE_CATALOG_EDIT' : 'SHARED_CATALOG_EDIT'));
  canDeleteType = computed(() => this.authService.hasPermission(this.isPrivate() ? 'PRIVATE_CATALOG_DELETE' : 'SHARED_CATALOG_DELETE'));
  canManageType = computed(() => this.authService.hasPermission(this.isPrivate() ? 'PRIVATE_CATALOG_MANAGE' : 'SHARED_CATALOG_MANAGE'));

  // Catalog permissions
  canCreateCatalog = computed(() => this.authService.hasPermission('CATALOG_CREATE'));
  canEditCatalog = computed(() => this.authService.hasPermission('CATALOG_EDIT'));
  canDeleteCatalog = computed(() => this.authService.hasPermission('CATALOG_DELETE'));
  canManageCatalog = computed(() => this.authService.hasPermission('CATALOG_MANAGE'));
  isUnitScopedSelected = computed(() => ['PHONG', 'MUC_LUC'].includes(this.selectedTypeCode()));

  // Pagination computed signals
  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

  constructor() {
    // Reload child catalogs when pagination changes
    effect(() => {
      const typeId = this.selectedTypeId();
      const page = this.currentPage();
      const size = this.pageSize();
      if (typeId) {
        this.loadCatalogs();
      }
    }, { allowSignalWrites: true });
  }

  ngOnInit() {
    this.authService.loadPermissions();
    const isPriv = this.route.snapshot.data['isPrivate'] ?? false;
    this.isPrivate.set(isPriv);
    this.loadCatalogTypes();
    this.loadOrganizationUnits();
  }

  openTypeMenu(type: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      { label: 'Xem chi tiết', title:'Xem chi tiết', icon: 'pi pi-eye color-teal', command: () => this.onViewType(type) },
      ...(this.canEditType() ? [{ label: 'Chỉnh sửa', title:'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEditType(type) }] : []),
      ...(this.canManageType() ? [{ label: type.status === 1 ? 'Khóa' : 'Mở khóa', title: type.status === 1 ? 'Khóa' : 'Mở khóa', icon: type.status === 1 ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal', command: () => this.onToggleTypeStatusRequest(type) }] : []),
      ...(this.canDeleteType() ? [{ label: 'Xóa', title:'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDeleteType(type) }] : []),
    ];
    menu.toggle(event);
  }

  openCatalogMenu(item: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      { label: 'Xem chi tiết', title:'Xem chi tiết', icon: 'pi pi-eye color-teal', command: () => this.onViewCatalog(item) },
      ...(this.canEditCatalog() ? [{ label: 'Chỉnh sửa', title:'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEditCatalog(item) }] : []),
      ...(this.canManageCatalog() ? [{ label: item.status === 1 ? 'Khóa' : 'Mở khóa', title: item.status === 1 ? 'Khóa' : 'Mở khóa', icon: item.status === 1 ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal', command: () => this.onToggleCatalogStatusRequest(item) }] : []),
      ...(this.canDeleteCatalog() ? [{ label: 'Xóa', title:'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDeleteCatalog(item) }] : []),
    ];
    menu.toggle(event);
  }
  // ─── LEFT PANEL: CATALOG TYPES ─────────────────────────────

  loadCatalogTypes(callback?: () => void) {
    this.catalogService.getSharedCatalogTypes(this.typeSearchKeyword(), undefined, this.isPrivate()).subscribe({
      next: (res: any) => {
        const list = Array.isArray(res) ? res : [];
        this.types.set(list);
        
        if (list.length > 0) {
          const currentId = this.selectedTypeId();
          const stillExists = currentId !== null && list.some(t => t.id === currentId);
          if (!stillExists) {
            const selectedTypeCode = this.route.snapshot.data['selectedTypeCode'];
            const defaultType = selectedTypeCode
              ? list.find(t => t.code === selectedTypeCode) || list[0]
              : list[0];
            this.onSelectType(defaultType);
          }
        } else {
          this.selectedTypeId.set(null);
          this.selectedTypeCode.set('');
          this.selectedTypeName.set('');
          this.selectedTypeHasParent.set(0);
          this.items.set([]);
          this.totalCount.set(0);
        }
        if (callback) callback();
      },
      error: (err) => {
        console.error('Không thể tải danh sách loại danh mục.', err);
        this.types.set([]);
        this.selectedTypeId.set(null);
        this.selectedTypeCode.set('');
        this.selectedTypeName.set('');
        this.selectedTypeHasParent.set(0);
        this.items.set([]);
        this.totalCount.set(0);
        if (callback) callback();
      }
    });
  }

  onSelectType(type: any) {
    this.selectedTypeId.set(type.id);
    this.selectedTypeCode.set(type.code);
    this.selectedTypeName.set(type.name);
    this.selectedTypeHasParent.set(type.hasParent);
    this.currentPage.set(1);
    this.searchKeyword.set('');
    this.catalogSearchCode.set('');
    this.searchStatus.set('');
    this.catalogSearchUnitId.set(null);
    if (['PHONG', 'MUC_LUC'].includes(type.code)) {
      this.loadOrganizationUnits();
    }
    this.loadCatalogs();
  }

  onAddNewType() {
    if (!this.canCreateType()) return;
    this.currentTypeItem.set({
      code: '',
      name: '',
      hasParent: 0,
      description: '',
      isPrivate: this.isPrivate(),
      status: 1
    });
    this.isEditingType.set(false);
    this.isViewingType.set(false);
    this.typeFormSubmitted.set(false);
    this.typeServerErrors.set({});
    this.showTypeDialog.set(true);
  }

  onViewType(type: any) {
    this.currentTypeItem.set({ ...type });
    this.isEditingType.set(false);
    this.isViewingType.set(true);
    this.typeFormSubmitted.set(false);
    this.typeServerErrors.set({});
    this.showTypeDialog.set(true);
  }

  onEditType(type: any) {
    if (!this.canEditType()) return;
    this.currentTypeItem.set({ ...type });
    this.isEditingType.set(true);
    this.isViewingType.set(false);
    this.typeFormSubmitted.set(false);
    this.typeServerErrors.set({});
    this.showTypeDialog.set(true);
  }

  onCloseTypeDialog() {
    this.showTypeDialog.set(false);
    this.isViewingType.set(false);
  }

  onTypeFieldChange(field: string) {
    this.typeServerErrors.update(errs => {
      const copy = { ...errs };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }

  onSaveType() {
    this.typeFormSubmitted.set(true);
    this.typeServerErrors.set({});
    if (this.typeCodeError() || this.typeNameError()) {
      return;
    }

    const typeDraft = this.currentTypeItem();
    this.typeSaving.set(true);

    if (this.isEditingType()) {
      this.catalogService.updateCatalogType(typeDraft.id, typeDraft, this.isPrivate()).subscribe({
        next: () => {
          this.typeSaving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: 'Cập nhật',
            detail: 'Cập nhật loại danh mục thành công!'
          });
          this.showTypeDialog.set(false);
          this.loadCatalogTypes(() => {
            if (this.selectedTypeId() === typeDraft.id) {
              this.selectedTypeName.set(typeDraft.name);
              this.selectedTypeHasParent.set(typeDraft.hasParent);
            }
          });
        },
        error: (err) => {
          this.typeSaving.set(false);
          this.parseTypeErrors(err);
        }
      });
    } else {
      this.catalogService.createCatalogType(typeDraft, this.isPrivate()).subscribe({
        next: (created) => {
          this.typeSaving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: 'Thêm mới',
            detail: 'Thêm mới loại danh mục thành công!'
          });
          this.showTypeDialog.set(false);
          this.selectedTypeId.set(null);
          this.loadCatalogTypes();
        },
        error: (err) => {
          this.typeSaving.set(false);
          this.parseTypeErrors(err);
        }
      });
    }
  }

  private parseTypeErrors(err: any) {
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
    this.typeServerErrors.set(errorsObj);
    const errorMsg = err.error?.message || err.message || 'Thao tác loại danh mục thất bại.';
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi',
      detail: errorMsg
    });
  }

  onToggleTypeStatusRequest(type: any) {
    if (!this.canManageType()) return;
    this.typeLockTarget.set(type);
    this.showTypeLockConfirm.set(true);
  }

  onCancelTypeLock() {
    this.showTypeLockConfirm.set(false);
    this.typeLockTarget.set(null);
  }

  onConfirmTypeLock() {
    const type = this.typeLockTarget();
    if (!type) return;

    this.typeLockLoading.set(true);
    const isLocking = type.status === 1;
    this.catalogService.toggleCatalogTypeStatus(type.id, isLocking, this.isPrivate()).subscribe({
      next: (res: any) => {
        this.typeLockLoading.set(false);
        this.showTypeLockConfirm.set(false);
        this.typeLockTarget.set(null);
        this.messageService.add({
          severity: 'success',
          summary: isLocking ? 'Ngừng hoạt động' : 'Kích hoạt',
          detail: res.message || 'Thay đổi trạng thái loại danh mục thành công!'
        });
        this.loadCatalogTypes();
      },
      error: (err) => {
        this.typeLockLoading.set(false);
        this.showTypeLockConfirm.set(false);
        this.typeLockTarget.set(null);
        const errorMsg = err.error?.message || 'Không thể thay đổi trạng thái loại danh mục.';
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi thao tác',
          detail: errorMsg
        });
      }
    });
  }

  onDeleteType(type: any) {
    if (!this.canDeleteType()) return;
    this.typeDeleteTarget.set(type);
    this.showTypeDeleteConfirm.set(true);
  }

  onConfirmDeleteType() {
    const type = this.typeDeleteTarget();
    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!type || this.typeDeleting()) return;

    this.typeDeleting.set(true);
    this.catalogService.deleteCatalogType(type.id, this.isPrivate())
      .pipe(finalize(() => this.typeDeleting.set(false)))
      .subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Xóa thành công',
          detail: `Đã xóa loại danh mục "${type.name}" thành công!`
        });
        this.showTypeDeleteConfirm.set(false);
        this.typeDeleteTarget.set(null);
        if (this.selectedTypeId() === type.id) {
          this.selectedTypeId.set(null);
          this.items.set([]);
          this.totalCount.set(0);
        }
        this.loadCatalogTypes();
      },
      error: (err) => {
        const errorMsg = err.error?.message || 'Xóa loại danh mục thất bại.';
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi xóa',
          detail: errorMsg
        });
      }
    });
  }

  onCancelDeleteType() {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.typeDeleting()) return;

    this.showTypeDeleteConfirm.set(false);
    this.typeDeleteTarget.set(null);
  }

  // ─── RIGHT PANEL: CATALOGS ─────────────────────────────────

  loadCatalogs() {
    const typeId = this.selectedTypeId();
    if (!typeId) return;

    if (this.isUnitScopedSelected() && !this.catalogSearchUnitId()) {
      this.items.set([]);
      this.totalCount.set(0);
      return;
    }

    const queryKeyword = this.searchKeyword() || this.catalogSearchCode();

    this.catalogService.getItemsByTypeId(
      typeId, 
      this.currentPage(), 
      this.pageSize(), 
      queryKeyword, 
      this.searchStatus(),
      this.isUnitScopedSelected() ? this.catalogSearchUnitId() : null
    ).subscribe({
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

  onSearchCatalogs() {
    this.currentPage.set(1);
    this.loadCatalogs();
  }

  onResetSearchCatalogs() {
    this.searchKeyword.set('');
    this.catalogSearchCode.set('');
    this.searchStatus.set('');
    this.catalogSearchUnitId.set(this.isUnitScopedSelected() ? this.getDefaultUnitId() : null);
    this.currentPage.set(1);
    this.loadCatalogs();
  }

  onCatalogSearchUnitChange(unitId: number | string | null) {
    const normalizedUnitId = unitId === null || unitId === '' ? null : Number(unitId);
    this.catalogSearchUnitId.set(normalizedUnitId);
    this.onSearchCatalogs();
  }

  onAddNewCatalog() {
    if (!this.canCreateCatalog()) return;
    this.currentCatalogItem.set({
      code: '',
      name: '',
      catalogTypeId: this.selectedTypeId(),
      parentId: null,
      description: '',
      priority: 1,
      status: 1,
      unitId: this.isUnitScopedSelected() ? this.catalogSearchUnitId() : null
    });
    if (this.selectedTypeHasParent() === 1) {
      this.loadParentsList();
    }
    this.catalogFormSubmitted.set(false);
    this.catalogServerErrors.set({});
    this.currentCatalogView.set('add');
  }

  onViewCatalog(catalog: any) {
    this.currentCatalogItem.set({ ...catalog });
    if (this.selectedTypeHasParent() === 1) {
      this.loadParentsList(catalog.id);
    }
    this.catalogFormSubmitted.set(false);
    this.catalogServerErrors.set({});
    this.currentCatalogView.set('view');
  }

  onEditCatalog(catalog: any) {
    if (!this.canEditCatalog()) return;
    this.currentCatalogItem.set({ ...catalog });
    if (this.selectedTypeHasParent() === 1) {
      this.loadParentsList(catalog.id);
    }
    this.catalogFormSubmitted.set(false);
    this.catalogServerErrors.set({});
    this.currentCatalogView.set('edit');
  }

  onCatalogFieldChange(field: string) {
    this.catalogServerErrors.update(errs => {
      const copy = { ...errs };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }

  loadParentsList(excludeId?: number) {
    const typeId = this.selectedTypeId();
    if (!typeId) return;

    const unitId = this.isUnitScopedSelected()
      ? Number(this.currentCatalogItem().unitId || this.catalogSearchUnitId())
      : null;
    if (this.isUnitScopedSelected() && !unitId) {
      this.parentsList.set([]);
      return;
    }

    this.catalogService.getItemsByTypeId(typeId, 1, 9999, undefined, '1', unitId).subscribe({
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

  onCatalogUnitChange() {
    this.onCatalogFieldChange('unitId');
    this.currentCatalogItem().parentId = null;
    if (this.selectedTypeHasParent() === 1) {
      this.loadParentsList(this.currentCatalogItem().id);
    }
  }

  onSaveCatalog() {
    this.catalogFormSubmitted.set(true);
    this.catalogServerErrors.set({});
    if (this.catalogCodeError() || this.catalogNameError() || this.catalogUnitError()) {
      return;
    }

    const catalogDraft = this.currentCatalogItem();
    if (catalogDraft.priority === undefined || catalogDraft.priority === null) {
      catalogDraft.priority = 1;
    }
    if (this.isUnitScopedSelected()) {
      catalogDraft.unitId = Number(catalogDraft.unitId);
    }

    this.catalogSaving.set(true);

    if (this.currentCatalogView() === 'edit') {
      this.catalogService.updateItem(catalogDraft.id, catalogDraft).subscribe({
        next: () => {
          this.catalogSaving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: 'Cập nhật',
            detail: 'Cập nhật danh mục thành công!'
          });
          this.currentCatalogView.set('list');
          this.loadCatalogs();
        },
        error: (err) => {
          this.catalogSaving.set(false);
          this.parseCatalogErrors(err);
        }
      });
    } else {
      this.catalogService.createItem(catalogDraft).subscribe({
        next: () => {
          this.catalogSaving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: 'Thêm mới',
            detail: 'Thêm mới danh mục thành công!'
          });
          this.currentCatalogView.set('list');
          this.loadCatalogs();
        },
        error: (err) => {
          this.catalogSaving.set(false);
          this.parseCatalogErrors(err);
        }
      });
    }
  }

  private parseCatalogErrors(err: any) {
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
    this.catalogServerErrors.set(errorsObj);
    const errorMsg = err.error?.message || err.message || 'Thao tác danh mục thất bại.';
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi',
      detail: errorMsg
    });
  }

  onToggleCatalogStatusRequest(catalog: any) {
    if (!this.canManageCatalog()) return;
    this.catalogLockTarget.set(catalog);
    this.showCatalogLockConfirm.set(true);
  }

  onCancelCatalogLock() {
    this.showCatalogLockConfirm.set(false);
    this.catalogLockTarget.set(null);
  }

  onConfirmCatalogLock() {
    const catalog = this.catalogLockTarget();
    if (!catalog) return;

    this.catalogLockLoading.set(true);
    const isLocking = catalog.status === 1;
    this.catalogService.toggleStatus(catalog.id, isLocking).subscribe({
      next: (res: any) => {
        this.catalogLockLoading.set(false);
        this.showCatalogLockConfirm.set(false);
        this.catalogLockTarget.set(null);
        this.messageService.add({
          severity: 'success',
          summary: isLocking ? 'Ngừng hoạt động' : 'Kích hoạt',
          detail: res.message || 'Thay đổi trạng thái danh mục thành công!'
        });
        this.loadCatalogs();
      },
      error: (err) => {
        this.catalogLockLoading.set(false);
        this.showCatalogLockConfirm.set(false);
        this.catalogLockTarget.set(null);
        const errorMsg = err.error?.message || 'Không thể thay đổi trạng thái danh mục.';
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi thao tác',
          detail: errorMsg
        });
      }
    });
  }

  onDeleteCatalog(catalog: any) {
    if (!this.canDeleteCatalog()) return;
    this.catalogDeleteTarget.set(catalog);
    this.showCatalogDeleteConfirm.set(true);
  }

  onConfirmDeleteCatalog() {
    const catalog = this.catalogDeleteTarget();
    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!catalog || this.catalogDeleting()) return;

    this.catalogDeleting.set(true);
    this.catalogService.deleteItem(catalog.id)
      .pipe(finalize(() => this.catalogDeleting.set(false)))
      .subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Xóa thành công',
          detail: `Đã xóa danh mục "${catalog.name}" thành công!`
        });
        this.showCatalogDeleteConfirm.set(false);
        this.catalogDeleteTarget.set(null);
        this.loadCatalogs();
      },
      error: (err) => {
        const errorMsg = err.error?.message || 'Xóa danh mục thất bại.';
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi xóa',
          detail: errorMsg
        });
      }
    });
  }

  onCancelDeleteCatalog() {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.catalogDeleting()) return;

    this.showCatalogDeleteConfirm.set(false);
    this.catalogDeleteTarget.set(null);
  }

  // Pagination handlers
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

  loadOrganizationUnits() {
    this.catalogService.getOrganizationUnits().subscribe({
      next: (units) => {
        const allUnits = Array.isArray(units) ? units : [];
        const isAdmin = this.authService.getUserRoles().some(role => role.toUpperCase() === 'ADMIN');
        const currentUnitId = this.authService.getUserUnitId();
        const selectableUnits = isAdmin || !currentUnitId
          ? allUnits
          : allUnits.filter(unit => Number(unit.id) === Number(currentUnitId));

        this.organizationUnits.set(selectableUnits);
        if (this.isUnitScopedSelected() && !this.catalogSearchUnitId()) {
          const defaultUnitId = this.getDefaultUnitId();
          this.catalogSearchUnitId.set(defaultUnitId);
          if (defaultUnitId) this.loadCatalogs();
        }
      },
      error: () => this.organizationUnits.set([])
    });
  }

  private getDefaultUnitId(): number | null {
    const currentUnitId = this.authService.getUserUnitId();
    if (currentUnitId && this.organizationUnits().some(unit => Number(unit.id) === currentUnitId)) {
      return currentUnitId;
    }
    const firstUnit = this.organizationUnits()[0];
    return firstUnit ? Number(firstUnit.id) : null;
  }

  getUnitLabel(unitId: number | string | null | undefined): string {
    if (!unitId) return '';
    const unit = this.organizationUnits().find(x => Number(x.id) === Number(unitId));
    return unit?.name || '';
  }
}
