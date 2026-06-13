import { Component, OnInit, signal, computed, inject, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '@sohoa.frontend/shared/core';
import { CatalogService } from '../../data-access/catalog.service';

@Component({
  selector: 'app-catalog',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, SelectModule, DialogModule],
  providers: [MessageService],
  templateUrl: './catalog.component.html',
  styleUrl: './catalog.component.css'
})
export class CatalogComponent implements OnInit {
  private catalogService = inject(CatalogService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private route = inject(ActivatedRoute);

  // Mode state
  isPrivate = signal<boolean>(false);

  // Left Panel - Catalog Types State
  types = signal<any[]>([]);
  typeSearchKeyword = signal<string>('');
  
  selectedTypeId = signal<number | null>(null);
  selectedTypeCode = signal<string>('');
  selectedTypeName = signal<string>('');
  selectedTypeHasParent = signal<number>(0);

  // Catalog Type Form Dialog
  showTypeDialog = signal<boolean>(false);
  isEditingType = signal<boolean>(false);
  currentTypeItem = signal<any>({});
  typeFormSubmitted = signal<boolean>(false);
  typeServerErrors = signal<any>({});
  typeSaving = signal<boolean>(false);

  // Catalog Type Delete Confirmation
  showTypeDeleteConfirm = signal<boolean>(false);
  typeDeleteTarget = signal<any>(null);
  typeDeleting = signal<boolean>(false);

  // Right Panel - Child Catalogs State
  items = signal<any[]>([]);
  parentsList = signal<any[]>([]);
  searchKeyword = signal<string>('');
  catalogSearchCode = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'
  totalCount = signal<number>(0);

  // Right Panel Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Right Panel Catalog Form Dialog
  currentCatalogView = signal<'list' | 'add' | 'edit'>('list');
  currentCatalogItem = signal<any>({});
  catalogFormSubmitted = signal<boolean>(false);
  catalogServerErrors = signal<any>({});
  catalogSaving = signal<boolean>(false);

  // Right Panel Catalog Delete Confirmation
  showCatalogDeleteConfirm = signal<boolean>(false);
  catalogDeleteTarget = signal<any>(null);
  catalogDeleting = signal<boolean>(false);

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
  }

  // ─── LEFT PANEL: CATALOG TYPES ─────────────────────────────

  loadCatalogTypes(callback?: () => void) {
    this.catalogService.getSharedCatalogTypes(this.typeSearchKeyword(), undefined, this.isPrivate()).subscribe({
      next: (res: any) => {
        this.types.set(Array.isArray(res) ? res : []);
        if (callback) callback();
      },
      error: (err) => {
        console.error('Không thể tải danh sách loại danh mục.', err);
        this.types.set([]);
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
    this.typeFormSubmitted.set(false);
    this.typeServerErrors.set({});
    this.showTypeDialog.set(true);
  }

  onEditType(type: any) {
    if (!this.canEditType()) return;
    this.currentTypeItem.set({ ...type });
    this.isEditingType.set(true);
    this.typeFormSubmitted.set(false);
    this.typeServerErrors.set({});
    this.showTypeDialog.set(true);
  }

  onCloseTypeDialog() {
    this.showTypeDialog.set(false);
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

  onToggleTypeStatus(type: any) {
    if (!this.canManageType()) return;
    const isLocking = type.status === 1;
    this.catalogService.toggleCatalogTypeStatus(type.id, isLocking, this.isPrivate()).subscribe({
      next: (res: any) => {
        this.messageService.add({
          severity: 'success',
          summary: isLocking ? 'Đã khóa' : 'Đã mở khóa',
          detail: res.message || 'Thay đổi trạng thái loại danh mục thành công!'
        });
        this.loadCatalogTypes();
      },
      error: (err) => {
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
    if (!type) return;

    this.typeDeleting.set(true);
    this.catalogService.deleteCatalogType(type.id, this.isPrivate()).subscribe({
      next: () => {
        this.typeDeleting.set(false);
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
        this.typeDeleting.set(false);
        this.showTypeDeleteConfirm.set(false);
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
    this.showTypeDeleteConfirm.set(false);
    this.typeDeleteTarget.set(null);
  }

  // ─── RIGHT PANEL: CATALOGS ─────────────────────────────────

  loadCatalogs() {
    const typeId = this.selectedTypeId();
    if (!typeId) return;

    const queryKeyword = this.searchKeyword() || this.catalogSearchCode();

    this.catalogService.getItemsByTypeId(
      typeId, 
      this.currentPage(), 
      this.pageSize(), 
      queryKeyword, 
      this.searchStatus()
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
    this.currentPage.set(1);
    this.loadCatalogs();
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
      status: 1
    });
    if (this.selectedTypeHasParent() === 1) {
      this.loadParentsList();
    }
    this.catalogFormSubmitted.set(false);
    this.catalogServerErrors.set({});
    this.currentCatalogView.set('add');
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

    this.catalogService.getItemsByTypeId(typeId, 1, 9999, undefined, '1').subscribe({
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

  onSaveCatalog() {
    this.catalogFormSubmitted.set(true);
    this.catalogServerErrors.set({});
    if (this.catalogCodeError() || this.catalogNameError()) {
      return;
    }

    const catalogDraft = this.currentCatalogItem();
    if (catalogDraft.priority === undefined || catalogDraft.priority === null) {
      catalogDraft.priority = 1;
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

  onToggleCatalogStatus(catalog: any) {
    if (!this.canManageCatalog()) return;
    const isLocking = catalog.status === 1;
    this.catalogService.toggleStatus(catalog.id, isLocking).subscribe({
      next: (res: any) => {
        this.messageService.add({
          severity: 'success',
          summary: isLocking ? 'Đã khóa' : 'Đã mở khóa',
          detail: res.message || 'Thay đổi trạng thái danh mục thành công!'
        });
        this.loadCatalogs();
      },
      error: (err) => {
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
    if (!catalog) return;

    this.catalogDeleting.set(true);
    this.catalogService.deleteItem(catalog.id).subscribe({
      next: () => {
        this.catalogDeleting.set(false);
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
        this.catalogDeleting.set(false);
        this.showCatalogDeleteConfirm.set(false);
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
}
