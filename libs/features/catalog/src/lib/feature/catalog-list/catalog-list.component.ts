import { Component, OnInit, signal, computed, inject, effect } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  EcoPaginatorComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { TreeSelectModule } from 'primeng/treeselect';
import { DialogModule } from 'primeng/dialog';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService, TreeNode } from 'primeng/api';
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
    TreeSelectModule,
    DialogModule,
    WfBreadcrumbComponent,
    EcoPaginatorComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './catalog-list.component.html',
  styles: [`
    .catalog-tree-name { display: flex; align-items: center; gap: 7px; min-height: 24px; }
    .catalog-tree-toggle { width: 18px; height: 18px; padding: 0; border: 0; background: transparent; color: #64748b; cursor: pointer; }
    .catalog-tree-toggle-placeholder { display: inline-block; width: 18px; flex: 0 0 18px; }
    .catalog-tree-row-child { background: #fbfdff; }
    .catalog-tree-context { opacity: .78; }
  `]
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
  selectedUnitNode = signal<TreeNode | null>(null);
  selectedUnitNodes = computed<TreeNode[]>(() => this.selectedUnitNode() ? [this.selectedUnitNode()!] : []);
  searchKeyword = signal<string>('');
  searchName = signal<string>('');
  searchCode = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'
  searchUnitId = signal<number | null>(null);
  searchUnitNode = signal<TreeNode | null>(null);
  searchUnitNodes = computed<TreeNode[]>(() => this.searchUnitNode() ? [this.searchUnitNode()!] : []);
  totalCount = signal<number>(0);
  expandedCatalogIds = signal<Set<number>>(new Set<number>());

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
      ...(this.canEdit() ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(item) }] : []),
      ...(this.canManage() ? [{
        label: active ? 'Khóa danh mục' : 'Mở khóa danh mục',
        title: active ? 'Khóa danh mục' : 'Mở khóa danh mục',
        icon: active ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal',
        command: () => this.onToggleStatus(item)
      }] : []),
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
    if (this.isUnitScopedCatalog() && this.formSubmitted() && !this.currentItem().unitId)
      return 'Đơn vị tạo là bắt buộc';
    return this.serverErrors().unitId || this.serverErrors().UnitId || '';
  });
  parentError = computed(() => this.serverErrors().parentId || this.serverErrors().ParentId || '');

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

  // Check if this catalog type supports hierarchy (Parent-Child)
  hasParent = computed(() => {
    const type = this.catalogType();
    const typeObj = this.catalogTypes().find(t => t.code === type);
    return typeObj ? typeObj.hasParent === 1 : false;
  });

  isPhongCatalog = computed(() => this.catalogType() === 'PHONG');
  isMucLucCatalog = computed(() => this.catalogType() === 'MUC_LUC');
  usesStandardCatalogTableStyle = computed(() => [
    'PROCESSING_CATEGORY',
    'PHONG',
    'CHUC_VU',
    'LINH_VUC',
    'TINH_TRANG_VAT_LY'
  ].includes(this.catalogType()));
  isUnitScopedCatalog = computed(() => this.isPhongCatalog() || this.isMucLucCatalog());
  usesStandardAuditColumns = computed(() => [
    'PROCESSING_CATEGORY',
    'CHUC_VU',
    'LINH_VUC',
    'TINH_TRANG_VAT_LY'
  ].includes(this.catalogType()));
  showParentColumn = computed(() => this.hasParent() && !this.usesStandardAuditColumns() && !this.isMucLucCatalog());
  organizationUnitTree = computed<TreeNode[]>(() => {
    const activeUnits = this.organizationUnits().filter(unit =>
      unit.isActive !== false && unit.isActive !== 0 && unit.isDeleted !== true && unit.isDeleted !== 1);
    const nodes = new Map<number, TreeNode>();
    const roots: TreeNode[] = [];

    activeUnits.forEach(unit => nodes.set(Number(unit.id), {
      key: String(unit.id),
      label: unit.name,
      data: unit,
      expanded: !unit.parentId,
      children: []
    }));
    const rootUnit = activeUnits.find(unit =>
      unit.parentId == null || String(unit.name).toUpperCase().includes('TỔNG CÔNG TY ĐIỆN LỰC TP.HÀ NỘI'));
    activeUnits.forEach(unit => {
      const node = nodes.get(Number(unit.id))!;
      if (rootUnit && Number(unit.id) !== Number(rootUnit.id)) {
        nodes.get(Number(rootUnit.id))!.children!.push(node);
      } else {
        roots.push(node);
      }
    });
    const sortTree = (items: TreeNode[]) => {
      items.sort((a, b) => String(a.label).localeCompare(String(b.label), 'vi'));
      items.forEach(item => sortTree(item.children ?? []));
    };
    sortTree(roots);
    return roots;
  });
  isAdmin = computed(() => this.authService.getUserRoles().some(role =>
    ['ADMIN', 'SUPER_ADMIN'].includes(role.toUpperCase())));
  customBreadcrumbItems = computed(() => this.isPhongCatalog()
    ? [
        { label: 'Quản lý danh mục' },
        { label: 'Danh mục phông' }
      ]
    : null);

  // Paginated items
  paginatedItems = computed(() => {
    if (!this.isMucLucCatalog()) return this.items();

    const pageItems = this.items();
    const itemsById = new Map<number, any>(pageItems.map(item => [Number(item.id), item]));
    return pageItems.filter(item => {
      let parentId = item.parentId == null ? null : Number(item.parentId);
      const visited = new Set<number>();
      while (parentId != null && itemsById.has(parentId) && visited.add(parentId)) {
        if (!this.expandedCatalogIds().has(parentId)) return false;
        const parent = itemsById.get(parentId);
        parentId = parent?.parentId == null ? null : Number(parent.parentId);
      }
      return true;
    });
  });

  isCatalogExpanded(id: number): boolean {
    return this.expandedCatalogIds().has(Number(id));
  }

  toggleCatalogNode(item: any, event?: Event): void {
    event?.stopPropagation();
    if (!item?.hasChildren) return;
    this.expandedCatalogIds.update(current => {
      const next = new Set(current);
      const id = Number(item.id);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  getCatalogTreeLevel(item: any): number {
    const level = Number(item?.level ?? 0);
    return Number.isFinite(level) && level > 0 ? level : 0;
  }

  private syncExpandedCatalogs(): void {
    if (!this.isMucLucCatalog()) return;
    const pageItems = this.items();
    const validIds = new Set(pageItems.map(item => Number(item.id)));
    const expandAll = Boolean(this.searchName().trim() || this.searchCode().trim() || this.searchStatus());
    this.expandedCatalogIds.update(current => {
      const next = new Set<number>();
      current.forEach(id => {
        if (validIds.has(id)) next.add(id);
      });
      pageItems.forEach(item => {
        if (item.hasChildren && (expandAll || this.getCatalogTreeLevel(item) === 0)) {
          next.add(Number(item.id));
        }
      });
      return next;
    });
  }

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

  onPageSizeChange(pageSize: number) {
    this.pageSize.set(pageSize);
    this.currentPage.set(1);
  }

  // Map catalogType → permission prefix (mỗi controller riêng = 1 nhóm quyền riêng)
  private readonly PERMISSION_PREFIX_MAP: Record<string, string> = {
    KE: 'SHELF',
    TANG: 'FLOOR',
    HOP: 'BOX',
    CHUC_VU: 'POSITION',
    PHONG: 'PHONG',
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
      if (['PHONG', 'MUC_LUC'].includes(data['type'] || '')) {
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
    if (this.isUnitScopedCatalog()) {
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
      const creatorUnitId = this.authService.getUserUnitId();
      if (!creatorUnitId && !this.isAdmin()) {
        this.items.set([]);
        this.totalCount.set(0);
        return;
      }
      this.catalogService.getItems(
        type,
        this.currentPage(),
        this.pageSize(),
        undefined,
        this.searchStatus(),
        undefined,
        this.searchName(),
        this.searchCode()
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

    if (this.isMucLucCatalog()) {
      const typeId = this.catalogTypes().find(t => t.code === type)?.id;
      if (!typeId) {
        this.items.set([]);
        this.totalCount.set(0);
        return;
      }
      this.catalogService.getItemsByTypeId(
        Number(typeId), this.currentPage(), this.pageSize(),
        this.searchName() || this.searchCode(), this.searchStatus(), this.searchUnitId()
      ).subscribe({
        next: (res) => {
          this.items.set(res?.items || []);
          this.totalCount.set(res?.totalCount || 0);
          this.syncExpandedCatalogs();
          this.loadParentsList();
        },
        error: () => {
          this.items.set([]);
          this.totalCount.set(0);
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách mục lục hồ sơ.' });
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
      next: (units) => {
        const allUnits = Array.isArray(units) ? units : [];
        const isAdmin = this.authService.getUserRoles().some(role =>
          ['ADMIN', 'SUPER_ADMIN'].includes(role.toUpperCase()));
        const userUnitId = this.authService.getUserUnitId();
        const selectableUnits = isAdmin || !userUnitId
          ? allUnits
          : allUnits.filter(unit => Number(unit.id) === Number(userUnitId));
        this.organizationUnits.set(selectableUnits);
        this.syncSelectedUnitNode();
        if (!isAdmin && userUnitId) {
          this.searchUnitId.set(Number(userUnitId));
          this.searchUnitNode.set(this.findUnitNode(userUnitId));
          this.loadItems();
        }
      },
      error: () => this.organizationUnits.set([])
    });
  }

  getParentName(parentId: number): string {
    const parent = this.items().find(item => item.id === parentId) ||
      this.parentsList().find(item => item.id === parentId);
    return parent ? parent.name : '';
  }

  loadParentsList(excludeId?: number) {
    const type = this.catalogType();
    if (this.isMucLucCatalog()) {
      const typeId = this.catalogTypes().find(t => t.code === type)?.id;
      const unitId = Number(this.currentItem().unitId || this.searchUnitId());
      if (!typeId || !unitId) {
        this.parentsList.set([]);
        return;
      }
      this.catalogService.getItemsByTypeId(Number(typeId), 1, 9999, undefined, '1', unitId).subscribe({
        next: data => this.parentsList.set((data?.items || []).filter((item: any) => !excludeId || item.id !== excludeId)),
        error: () => this.parentsList.set([])
      });
      return;
    }
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

  onSearchUnitNodeChange(value: TreeNode | TreeNode[] | null): void {
    const node = Array.isArray(value) ? value[value.length - 1] ?? null : value;
    this.searchUnitNode.set(node);
    this.onSearchUnitChange(node?.data?.id ?? null);
  }

  onResetSearch() {
    this.searchKeyword.set('');
    this.searchName.set('');
    this.searchCode.set('');
    this.searchStatus.set('');
    const isAdmin = this.authService.getUserRoles().some(role =>
      ['ADMIN', 'SUPER_ADMIN'].includes(role.toUpperCase()));
    this.searchUnitId.set(isAdmin ? null : (this.authService.getUserUnitId() ?? null));
    this.searchUnitNode.set(this.findUnitNode(this.searchUnitId()));
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
      unitId: this.isAdmin() ? null : (this.authService.getUserUnitId() ?? null),
      catalogTypeId: this.catalogTypes().find(t => t.code === this.catalogType())?.id
    });
    this.syncSelectedUnitNode();
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
    this.syncSelectedUnitNode();
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
      // Backend xác định loại PHONG và đơn vị tạo từ token, không tin dữ liệu scope do client gửi lên.
      delete itemDraft.catalogTypeId;
      if (this.isAdmin()) {
        // API trả id đơn vị có thể ở dạng chuỗi; chuẩn hóa để model long? của backend bind được.
        itemDraft.unitId = Number(itemDraft.unitId);
      } else {
        delete itemDraft.unitId;
      }
    } else if (this.isMucLucCatalog()) {
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
          if (this.isMucLucCatalog()) {
            this.searchUnitId.set(Number(itemDraft.unitId));
            this.searchUnitNode.set(this.findUnitNode(itemDraft.unitId));
          }
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
          if (this.isMucLucCatalog()) {
            this.searchUnitId.set(Number(itemDraft.unitId));
            this.searchUnitNode.set(this.findUnitNode(itemDraft.unitId));
          }
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
    if (!item || this.togglingStatus()) return;
    const isLocking = item.status === 1;
    this.togglingStatus.set(true);
    this.catalogService.toggleStatus(item.id, isLocking, this.catalogType())
      .pipe(finalize(() => this.togglingStatus.set(false)))
      .subscribe({
      next: (res: any) => {
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
        const errorMsg = err.error?.message || 'Không thể thay đổi trạng thái danh mục.';
        this.messageService.add({ severity: 'error', summary: 'Lỗi thao tác', detail: errorMsg });
      }
    });
  }

  onCancelToggleStatus() {
    if (this.togglingStatus()) return;

    this.showStatusConfirm.set(false);
    this.statusTarget.set(null);
  }

  getUnitLabel(unitId: number | string | null | undefined): string {
    if (!unitId) return '';
    const unit = this.organizationUnits().find(x => Number(x.id) === Number(unitId));
    return unit?.name || '';
  }

  onUnitNodeChange(value: TreeNode | TreeNode[] | null): void {
    const node = Array.isArray(value) ? value[value.length - 1] ?? null : value;
    this.selectedUnitNode.set(node);
    this.currentItem().unitId = node?.data?.id == null ? null : Number(node.data.id);
    this.onFieldChange('unitId');
    if (this.isMucLucCatalog()) {
      this.currentItem().parentId = null;
      this.loadParentsList(this.currentItem().id);
    }
  }

  isUnitNodeSelected(node: TreeNode): boolean {
    return Number(this.selectedUnitNode()?.key) === Number(node.key);
  }

  private syncSelectedUnitNode(): void {
    const unitId = Number(this.currentItem()?.unitId);
    const findNode = (nodes: TreeNode[]): TreeNode | null => {
      for (const node of nodes) {
        if (Number(node.key) === unitId) return node;
        const child = findNode(node.children ?? []);
        if (child) return child;
      }
      return null;
    };
    this.selectedUnitNode.set(unitId ? findNode(this.organizationUnitTree()) : null);
  }

  isSearchUnitNodeSelected(node: TreeNode): boolean {
    return Number(this.searchUnitNode()?.key) === Number(node.key);
  }

  private findUnitNode(unitId: number | string | null | undefined): TreeNode | null {
    if (!unitId) return null;
    const find = (nodes: TreeNode[]): TreeNode | null => {
      for (const node of nodes) {
        if (Number(node.key) === Number(unitId)) return node;
        const child = find(node.children ?? []);
        if (child) return child;
      }
      return null;
    };
    return find(this.organizationUnitTree());
  }
}
