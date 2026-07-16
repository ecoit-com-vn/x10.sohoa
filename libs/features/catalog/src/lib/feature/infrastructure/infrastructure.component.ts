import { Component, OnInit, signal, computed, inject, effect, HostListener } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '@sohoa.frontend/shared/core';
import { InfrastructureService } from '../../data-access/infrastructure.service';
import { EquipmentService } from '@sohoa.frontend/features/equipment';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-infrastructure',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, SelectModule, DialogModule, MenuModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './infrastructure.component.html',
  styleUrl: './infrastructure.component.scss'
})
export class InfrastructureComponent implements OnInit {
  private infraService = inject(InfrastructureService);
  private equipmentService = inject(EquipmentService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  protected readonly Math = Math;

  // Dynamic Route Data
  infraTypeId = signal<number>(1);
  pageTitle = signal<string>('Danh mục cơ sở hạ tầng');

  // States
  items = signal<any[]>([]);
  orgUnits = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  searchKeyword = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'
  searchUnitId = signal<number | null>(null);
  totalCount = signal<number>(0);
  // Personal catalog only toggle
  searchPersonalOnly = signal<boolean>(false);

  // Lock/Unlock Confirmation Dialog Signals
  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);

  // Equipment Lock/Unlock Confirmation Signals
  showEquipmentLockConfirm = signal<boolean>(false);
  equipmentLockTarget = signal<any>(null);
  equipmentLockLoading = signal<boolean>(false);

  currentView = signal<'list' | 'add' | 'edit' | 'detail'>('list');
  currentItem = signal<any>({});
  isSaving = signal<boolean>(false);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Form Validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});

  codeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().code) return 'Mã là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });

  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().name) return 'Tên là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  gridTypeIdError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().gridTypeId) return 'Loại lưới điện là bắt buộc';
    return this.serverErrors().gridTypeId || this.serverErrors().GridTypeId || '';
  });

  // Delete Confirmation Dialog Signals
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);

  // Org-unit tree picker signals
  orgUnitTree = computed(() => this.buildOrgTree(this.orgUnits()));
  expandedUnitNodes = signal<Set<any>>(new Set<any>());
  orgTreePickerOpen = signal<boolean>(false);

  // Pagination Computeds
  paginatedItems = computed(() => {
    return this.items();
  });

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

  // ── DETAIL VIEW SIGNALS ────────────────────────────────────────────────────
  activeTab = signal<number>(0);

  // Danh sách thiết bị trong tab chi tiết
  equipmentItems = signal<any[]>([]);
  equipmentTotalCount = signal<number>(0);
  equipmentPage = signal<number>(1);
  equipmentPageSize = signal<number>(10);
  equipmentTypes = signal<any[]>([]);
  isLoadingEquipments = signal<boolean>(false);

  // Search thiết bị
  equipmentSearchKeyword = signal<string>('');
  equipmentSearchTypeId = signal<string>('');

  equipmentTotalPages = computed(() =>
    Math.ceil(this.equipmentTotalCount() / this.equipmentPageSize())
  );

  // More-menu 3 chấm cho bảng thiết bị
  activeEquipmentMenu = signal<string | null>(null);
  actionMenuItems: MenuItem[] = [];

  @HostListener('document:click')
  closeActionMenus() {
    this.activeEquipmentMenu.set(null);
  }

  openActionMenu(item: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      { label: 'Xem chi tiết', title: 'Xem chi tiết', icon: 'pi pi-eye color-teal', command: () => this.onViewDetail(item) },
      ...(this.canManage() ? [{ label: (item.isActive === 1 || item.isActive === true) ? 'Khóa bản ghi' : 'Mở khóa bản ghi', title: (item.isActive === 1 || item.isActive === true) ? 'Khóa bản ghi' : 'Mở khóa bản ghi', icon: (item.isActive === 1 || item.isActive === true) ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal', command: () => this.onToggleStatus(item) }] : []),
      ...(this.canEdit() ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(item) }] : []),
      ...(this.canDelete() ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(item) }] : []),
    ];
    menu.toggle(event);
  }

  toggleEquipmentMenu(item: any, event: Event) {
    event.stopPropagation();
    if (this.activeEquipmentMenu() === item.id) {
      this.activeEquipmentMenu.set(null);
    } else {
      this.activeEquipmentMenu.set(item.id);
    }
  }
  // ── END DETAIL VIEW SIGNALS ────────────────────────────────────────────────

  // Dynamic Permissions check per catalog type
  canCreate = computed(() => {
    const perm = this.infraTypeId() === 1 ? 'SUBSTATION_CREATE' : 'TRANSMISSION_LINE_CREATE';
    return this.authService.hasPermission(perm) || this.authService.hasPermission('SUPER_ADMIN');
  });

  canEdit = computed(() => {
    const perm = this.infraTypeId() === 1 ? 'SUBSTATION_EDIT' : 'TRANSMISSION_LINE_EDIT';
    return this.authService.hasPermission(perm) || this.authService.hasPermission('SUPER_ADMIN');
  });

  canDelete = computed(() => {
    const perm = this.infraTypeId() === 1 ? 'SUBSTATION_DELETE' : 'TRANSMISSION_LINE_DELETE';
    return this.authService.hasPermission(perm) || this.authService.hasPermission('SUPER_ADMIN');
  });

  canManage = computed(() => {
    const perm = this.infraTypeId() === 1 ? 'SUBSTATION_MANAGE' : 'TRANSMISSION_LINE_MANAGE';
    return this.authService.hasPermission(perm) || this.authService.hasPermission('SUPER_ADMIN');
  });

  canCreateEquipment = computed(() =>
    this.authService.hasPermission('EQUIPMENT_CREATE') || this.authService.hasPermission('SUPER_ADMIN')
  );

  constructor() {
    effect(() => {
      // Re-trigger load when page, pageSize, or infraTypeId changes
      this.currentPage();
      this.pageSize();
      this.infraTypeId();
      if (this.currentView() === 'list') {
        this.loadItems();
      }
    }, { allowSignalWrites: true });

    if (typeof window !== 'undefined') {
      window.addEventListener('click', () => {
        this.orgTreePickerOpen.set(false);
      });
    }
  }

  ngOnInit() {
    this.authService.loadPermissions();
    this.loadOrgUnits();
    this.loadGridTypes();
    this.loadEquipmentTypes();

    // Listen to route data changes to adapt dynamically
    this.route.data.subscribe(data => {
      if (data) {
        this.infraTypeId.set(data['infraTypeId'] || 1);
        this.pageTitle.set(data['title'] || 'Danh mục cơ sở hạ tầng');
        // Reset state on route switch
        this.currentPage.set(1);
        this.searchKeyword.set('');
        this.searchStatus.set('');
        this.searchUnitId.set(null);
      }
    });

    // Detect detail route (has :id param)
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.currentView.set('detail');
        this.activeTab.set(0);
        this.equipmentPage.set(1);
        this.equipmentSearchKeyword.set('');
        this.equipmentSearchTypeId.set('');
        // Load item detail
        this.infraService.getInfrastructureById(this.infraTypeId(), id).subscribe({
          next: (res) => {
            this.currentItem.set(res || {});
            this.loadEquipments();
          },
          error: () => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải thông tin chi tiết.' });
          }
        });
      } else {
        this.currentView.set('list');
        this.loadItems();
      }
    });
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

  loadOrgUnits() {
    this.infraService.getOrganizationUnits().subscribe({
      next: (data) => {
        const rawUnits = Array.isArray(data) ? data : (data && Array.isArray((data as any).items) ? (data as any).items : (data && Array.isArray((data as any).value) ? (data as any).value : []));
        this.orgUnits.set(rawUnits);
      },
      error: () => {
        console.error('Không thể tải danh sách đơn vị');
      }
    });
  }

  loadGridTypes() {
    this.infraService.getGridTypes().subscribe({
      next: (data) => {
        this.gridTypes.set(data || []);
      },
      error: () => {
        console.error('Không thể tải danh sách loại lưới điện');
      }
    });
  }

  loadEquipmentTypes() {
    this.infraService.getEquipmentTypes().subscribe({
      next: (data) => {
        this.equipmentTypes.set(data || []);
      },
      error: () => {
        console.error('Không thể tải danh sách loại thiết bị');
      }
    });
  }

  // ── Org-unit Tree Picker methods ──────────────────────────────────────────
  buildOrgTree(units: any[]): any[] {
    const map = new Map<any, any>();
    const roots: any[] = [];
    units.forEach(u => map.set(u.id, { ...u, children: [] }));
    map.forEach(node => {
      if (node.parentId && map.has(node.parentId)) {
        map.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });
    return roots;
  }

  toggleUnitNode(unitId: any, event?: Event) {
    if (event) event.stopPropagation();
    const current = new Set(this.expandedUnitNodes());
    if (current.has(unitId)) {
      current.delete(unitId);
    } else {
      current.add(unitId);
    }
    this.expandedUnitNodes.set(current);
  }

  isNodeExpanded(unitId: any): boolean {
    return this.expandedUnitNodes().has(unitId);
  }

  selectOrgUnit(unitId: any) {
    this.currentItem.update(u => ({ ...u, unitId: unitId }));
    this.orgTreePickerOpen.set(false);
    this.onFieldChange('unitId');
  }

  toggleOrgTreePicker(event?: Event) {
    if (event) event.stopPropagation();
    this.orgTreePickerOpen.update(v => !v);
  }

  getUnitLabel(unitId: any): string {
    if (!unitId) return '';
    const u = (this.orgUnits() || []).find(x => x.id == unitId);
    return u ? u.name : '';
  }

  clearOrgUnit(event: Event) {
    event.stopPropagation();
    this.currentItem.update(u => ({ ...u, unitId: null }));
    this.onFieldChange('unitId');
  }

  loadItems() {
    this.infraService.getInfrastructures(
      this.infraTypeId(),
      this.currentPage(),
      this.pageSize(),
      this.searchKeyword(),
      this.searchStatus(),
      this.searchUnitId(),
      this.searchPersonalOnly()
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
          detail: 'Không thể tải danh sách cơ sở hạ tầng'
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
    this.searchUnitId.set(null);
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
      infraTypeId: this.infraTypeId(),
      unitId: null,
      gridTypeId: null,
      address: '',
      organization: null
    });
    this.orgTreePickerOpen.set(false);
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('add');
  }

  onEdit(item: any) {
    this.currentItem.set({ ...item });
    this.orgTreePickerOpen.set(false);
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('edit');
  }

  // Điều hướng vào màn hình chi tiết
  onViewDetail(item: any) {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['../', segment, item.id], { relativeTo: this.route });
  }

  // Quay lại danh sách
  goBack() {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['../', segment], { relativeTo: this.route });
  }

  isGridTypeLocked(item?: any): boolean {
    if (this.currentView() !== 'edit') return false;
    const target = item ?? this.currentItem();
    const count = Number(target?.equipmentCount ?? target?.EquipmentCount ?? 0);
    return count > 0;
  }

  onSaveItem() {
    this.formSubmitted.set(true);
    const item = this.currentItem();

    if (!item.code || !item.name || !item.gridTypeId) {
      return;
    }

    this.isSaving.set(true);

    const payload = {
      id: item.id,
      code: item.code.trim(),
      name: item.name.trim(),
      address: item.address ? item.address.trim() : null,
      infraTypeId: this.infraTypeId(),
      unitId: item.unitId || null,
      gridTypeId: item.gridTypeId,
      isActive: item.isActive
    };

    const request$ = this.currentView() === 'add'
      ? this.infraService.createInfrastructure(this.infraTypeId(), payload)
      : this.infraService.updateInfrastructure(this.infraTypeId(), item.id, payload);

    request$.pipe(
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: this.currentView() === 'add' ? 'Đã thêm mới thành công!' : 'Đã cập nhật thành công!'
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
    this.lockUnlockTarget.set(item);
    this.showLockUnlockConfirm.set(true);
  }

  onCancelLockUnlock() {
    this.showLockUnlockConfirm.set(false);
    this.lockUnlockTarget.set(null);
  }

  onConfirmLockUnlock() {
    const item = this.lockUnlockTarget();
    if (!item) return;

    const isLocking = item.isActive === 1 || item.isActive === true;
    this.lockUnlockLoading.set(true);
    this.infraService.toggleStatus(this.infraTypeId(), item.id, isLocking)
      .pipe(
        finalize(() => {
          this.lockUnlockLoading.set(false);
          this.showLockUnlockConfirm.set(false);
          this.lockUnlockTarget.set(null);
        })
      ).subscribe({
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
            detail: err?.error?.message || 'Không thể cập nhật trạng thái.'
          });
        }
      });
  }

  onDelete(item: any) {
    this.deleteTarget.set(item);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete() {
    const item = this.deleteTarget();
    if (!item) return;

    this.deleting.set(true);
    this.infraService.deleteInfrastructure(this.infraTypeId(), item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: `Đã xóa "${item.name}" thành công!`
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
            detail: err?.error?.message || 'Không thể xóa bản ghi.'
          });
        }
      });
  }

  onCancelDelete() {
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  // ── DETAIL VIEW METHODS ────────────────────────────────────────────────────

  loadEquipments() {
    const item = this.currentItem();
    if (!item?.id) return;

    this.isLoadingEquipments.set(true);
    const keyword = this.equipmentSearchKeyword();
    const typeId = this.equipmentSearchTypeId();

    this.equipmentService.getEquipments(
      this.equipmentPage(),
      this.equipmentPageSize(),
      undefined, // code
      undefined, // name
      undefined, // unitId
      String(item.id), // infrastructureId
      undefined, // gridTypeId
      typeId || undefined, // equipmentTypeId
      undefined, // isActive
      keyword || undefined // keyword
    ).pipe(finalize(() => this.isLoadingEquipments.set(false)))
     .subscribe({
       next: (res) => {
         this.equipmentItems.set(res?.items || []);
         this.equipmentTotalCount.set(res?.totalCount || 0);
       },
       error: () => {
         this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách thiết bị.' });
       }
     });
  }

   onEquipmentFilterChange() {
    this.equipmentPage.set(1);
    this.loadEquipments();
  }

  onSearchEquipments() {
    this.equipmentPage.set(1);
    this.loadEquipments();
  }

  onResetEquipmentSearch() {
    this.equipmentSearchKeyword.set('');
    this.equipmentSearchTypeId.set('');
    this.equipmentPage.set(1);
    this.loadEquipments();
  }

  equipmentPrevPage() {
    if (this.equipmentPage() > 1) {
      this.equipmentPage.update(p => p - 1);
      this.loadEquipments();
    }
  }

  equipmentNextPage() {
    if (this.equipmentPage() < this.equipmentTotalPages()) {
      this.equipmentPage.update(p => p + 1);
      this.loadEquipments();
    }
  }

  goToEquipmentPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.equipmentTotalPages()) {
      this.equipmentPage.set(p);
      this.loadEquipments();
    }
  }

  onEquipmentPageSizeChange(event: any) {
    this.equipmentPageSize.set(Number(event.target.value));
    this.equipmentPage.set(1);
    this.loadEquipments();
  }

  // Thêm thiết bị mới gắn với trạm/đường dây này
  onAddEquipment() {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['/catalog', segment, this.currentItem().id, 'device-add']);
  }

  // Xem chi tiết thiết bị
  onViewEquipment(equipment: any) {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['/catalog', segment, this.currentItem().id, 'device-detail', equipment.id]);
  }

  // Chỉnh sửa thiết bị (vào thẳng chế độ sửa)
  onEditEquipment(equipment: any) {
    const segment = this.infraTypeId() === 1 ? 'substation' : 'transmission-line';
    this.router.navigate(['/catalog', segment, this.currentItem().id, 'device-detail', equipment.id], {
      queryParams: { mode: 'edit' }
    });
  }

  // Khóa / Mở khóa thiết bị (Mở popup xác nhận)
  onToggleEquipmentStatus(equipment: any) {
    this.equipmentLockTarget.set(equipment);
    this.showEquipmentLockConfirm.set(true);
  }

  onCancelEquipmentLock() {
    this.showEquipmentLockConfirm.set(false);
    this.equipmentLockTarget.set(null);
  }

  onConfirmEquipmentLock() {
    const equipment = this.equipmentLockTarget();
    if (!equipment) return;

    const isLocking = equipment.isActive === 1 || equipment.isActive === true;
    this.equipmentLockLoading.set(true);
    this.equipmentService.toggleStatus(equipment.id, isLocking)
      .pipe(
        finalize(() => {
          this.equipmentLockLoading.set(false);
          this.showEquipmentLockConfirm.set(false);
          this.equipmentLockTarget.set(null);
        })
      )
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: res.message || (isLocking ? 'Khóa thiết bị thành công!' : 'Mở khóa thiết bị thành công!')
          });
          this.loadEquipments();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể cập nhật trạng thái thiết bị.'
          });
        }
      });
  }

  // Lấy tên loại thiết bị
  getEquipmentTypeName(typeId: any): string {
    const et = this.equipmentTypes().find(t => t.id == typeId);
    return et ? et.name : '-';
  }
}
