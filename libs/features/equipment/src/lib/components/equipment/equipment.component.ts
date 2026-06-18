import { Component, OnInit, signal, computed, inject, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { AuthService } from '@sohoa.frontend/shared/core';
import { EquipmentService } from '../../data-access/equipment.service';
import { forkJoin } from 'rxjs';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-equipment-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, SelectModule, DialogModule],
  providers: [MessageService],
  templateUrl: './equipment.component.html',
  styleUrls: ['./equipment.component.css']
})
export class EquipmentComponent implements OnInit {
  private equipmentService = inject(EquipmentService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);

  // Lists from lookup
  organizationUnits = signal<any[]>([]);
  infrastructures = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
  equipmentTypes = signal<any[]>([]);
  countries = signal<any[]>([]);

  // Tree computed and states
  orgUnitTree = computed(() => this.buildOrgTree(this.organizationUnits()));
  
  // Search Tree Picker State
  searchOrgTreeOpen = signal<boolean>(false);
  expandedSearchUnitNodes = signal<Set<number>>(new Set<number>());

  // Form Tree Picker State
  formOrgTreeOpen = signal<boolean>(false);
  expandedFormUnitNodes = signal<Set<number>>(new Set<number>());

  // Search Filters
  searchCode = signal<string>('');
  searchName = signal<string>('');
  searchUnitId = signal<string>(''); 
  searchInfrastructureId = signal<string>('');
  searchGridTypeId = signal<string>('');
  searchEquipmentTypeId = signal<string>('');
  searchStatus = signal<string>(''); // '', '1', '0'

  // Cascading lists for Search
  searchInfrastructures = computed(() => {
    const uId = this.searchUnitId();
    if (!uId) return [];
    return this.infrastructures().filter(inf => inf.unitId === Number(uId));
  });

  searchEquipmentTypes = computed(() => {
    const gtId = this.searchGridTypeId();
    if (!gtId) return [];
    return this.equipmentTypes().filter(et => et.gridTypeId === Number(gtId));
  });

  // Cascading lists for Form
  formInfrastructures = computed(() => {
    const uId = this.currentItem().unitId;
    if (!uId) return [];
    return this.infrastructures().filter(inf => inf.unitId === Number(uId));
  });

  formEquipmentTypes = computed(() => {
    const gtId = this.currentItem().gridTypeId;
    if (!gtId) return [];
    return this.equipmentTypes().filter(et => et.gridTypeId === Number(gtId));
  });

  // State lists
  items = signal<any[]>([]);
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
    if (this.formSubmitted() && !this.currentItem().code) return 'Mã thiết bị là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });

  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().name) return 'Tên thiết bị là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  unitError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().unitId) return 'Đơn vị quản lý là bắt buộc';
    return this.serverErrors().unitId || this.serverErrors().UnitId || '';
  });

  gridTypeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().gridTypeId) return 'Loại lưới điện là bắt buộc';
    return this.serverErrors().gridTypeId || this.serverErrors().GridTypeId || '';
  });

  infrastructureError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().infrastructureId) return 'Trạm/Đường dây là bắt buộc';
    return this.serverErrors().infrastructureId || this.serverErrors().InfrastructureId || '';
  });

  equipmentTypeError = computed(() => {
    if (this.formSubmitted() && !this.currentItem().equipmentTypeId) return 'Loại thiết bị là bắt buộc';
    return this.serverErrors().equipmentTypeId || this.serverErrors().EquipmentTypeId || '';
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
  canCreate = computed(() => this.authService.hasPermission('EQUIPMENT_CREATE') || this.authService.hasPermission('SUPER_ADMIN'));
  canEdit = computed(() => this.authService.hasPermission('EQUIPMENT_EDIT') || this.authService.hasPermission('SUPER_ADMIN'));
  canDelete = computed(() => this.authService.hasPermission('EQUIPMENT_DELETE') || this.authService.hasPermission('SUPER_ADMIN'));
  canManage = computed(() => this.authService.hasPermission('EQUIPMENT_MANAGE') || this.authService.hasPermission('SUPER_ADMIN'));

  constructor() {
    effect(() => {
      this.currentPage();
      this.pageSize();
      this.loadItems();
    }, { allowSignalWrites: true });

    if (typeof window !== 'undefined') {
      window.addEventListener('click', () => {
        this.searchOrgTreeOpen.set(false);
        this.formOrgTreeOpen.set(false);
      });
    }
  }

  ngOnInit() {
    this.authService.loadPermissions();
    this.loadLookupData();
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

  loadLookupData() {
    forkJoin({
      organizationUnits: this.equipmentService.getOrganizationUnits(),
      infrastructures: this.equipmentService.getInfrastructures(),
      gridTypes: this.equipmentService.getGridTypes(),
      equipmentTypes: this.equipmentService.getEquipmentTypes(),
      countries: this.equipmentService.getCountries()
    }).subscribe({
      next: (data) => {
        this.organizationUnits.set(Array.isArray(data.organizationUnits) ? data.organizationUnits : []);
        this.infrastructures.set(Array.isArray(data.infrastructures) ? data.infrastructures : []);
        this.gridTypes.set(Array.isArray(data.gridTypes) ? data.gridTypes : []);
        this.equipmentTypes.set(Array.isArray(data.equipmentTypes) ? data.equipmentTypes : []);
        this.countries.set(Array.isArray(data.countries) ? data.countries : []);
      },
      error: () => {
        console.error('Không thể tải dữ liệu danh mục');
      }
    });
  }

  loadItems() {
    const unitId = this.searchUnitId() ? Number(this.searchUnitId()) : undefined;
    const gridTypeId = this.searchGridTypeId() ? Number(this.searchGridTypeId()) : undefined;
    const isActive = this.searchStatus() !== '' ? this.searchStatus() === '1' : undefined;

    this.equipmentService.getEquipments(
      this.currentPage(),
      this.pageSize(),
      this.searchCode(),
      this.searchName(),
      unitId,
      this.searchInfrastructureId(),
      gridTypeId,
      this.searchEquipmentTypeId(),
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
          detail: 'Không thể tải danh sách thiết bị'
        });
      }
    });
  }

  // Tree Helpers
  buildOrgTree(units: any[]): any[] {
    const map = new Map<number, any>();
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

  getUnitLabel(unitId: any): string {
    if (!unitId) return '';
    const u = (this.organizationUnits() || []).find(x => x.id == unitId);
    return u ? u.name : `Đơn vị #${unitId}`;
  }

  // Search tree picker actions
  toggleSearchOrgTree(event?: Event) {
    if (event) event.stopPropagation();
    this.searchOrgTreeOpen.update(v => !v);
    this.formOrgTreeOpen.set(false);
  }

  toggleSearchUnitNode(unitId: number, event?: Event) {
    if (event) event.stopPropagation();
    const current = new Set(this.expandedSearchUnitNodes());
    if (current.has(unitId)) {
      current.delete(unitId);
    } else {
      current.add(unitId);
    }
    this.expandedSearchUnitNodes.set(current);
  }

  isSearchNodeExpanded(unitId: number): boolean {
    return this.expandedSearchUnitNodes().has(unitId);
  }

  selectSearchOrgUnit(unitId: number) {
    this.searchUnitId.set(unitId.toString());
    this.searchInfrastructureId.set('');
    this.searchOrgTreeOpen.set(false);
    this.onSearch();
  }

  clearSearchOrgUnit(event: Event) {
    event.stopPropagation();
    this.searchUnitId.set('');
    this.searchInfrastructureId.set('');
    this.searchOrgTreeOpen.set(false);
    this.onSearch();
  }

  // Form tree picker actions
  toggleFormOrgTree(event?: Event) {
    if (event) event.stopPropagation();
    this.formOrgTreeOpen.update(v => !v);
    this.searchOrgTreeOpen.set(false);
  }

  toggleFormUnitNode(unitId: number, event?: Event) {
    if (event) event.stopPropagation();
    const current = new Set(this.expandedFormUnitNodes());
    if (current.has(unitId)) {
      current.delete(unitId);
    } else {
      current.add(unitId);
    }
    this.expandedFormUnitNodes.set(current);
  }

  isFormNodeExpanded(unitId: number): boolean {
    return this.expandedFormUnitNodes().has(unitId);
  }

  selectFormOrgUnit(unitId: number) {
    this.currentItem.update(item => ({
      ...item,
      unitId: unitId,
      infrastructureId: null
    }));
    this.formOrgTreeOpen.set(false);
    this.onFieldChange('unitId');
  }

  onGridTypeChange(value: any) {
    this.currentItem.update(item => ({ ...item, gridTypeId: value, equipmentTypeId: null }));
    this.onFieldChange('gridTypeId');
  }

  onSearch() {
    this.currentPage.set(1);
    this.loadItems();
  }

  onResetSearch() {
    this.searchCode.set('');
    this.searchName.set('');
    this.searchUnitId.set('');
    this.searchInfrastructureId.set('');
    this.searchGridTypeId.set('');
    this.searchEquipmentTypeId.set('');
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
      unitId: null,
      gridTypeId: null,
      infrastructureId: null,
      equipmentTypeId: null,
      countryId: null,
      code: '',
      name: '',
      serialNumber: ''
    });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('add');
    this.formOrgTreeOpen.set(false);
  }

  onEdit(item: any) {
    this.currentItem.set({ ...item });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.currentView.set('edit');
    this.formOrgTreeOpen.set(false);
  }

  onSaveItem() {
    this.formSubmitted.set(true);
    const item = this.currentItem();

    if (!item.code || !item.name || !item.unitId || !item.gridTypeId || !item.infrastructureId || !item.equipmentTypeId) {
      return;
    }

    this.isSaving.set(true);

    const payload = {
      equipmentTypeId: item.equipmentTypeId,
      infrastructureId: item.infrastructureId,
      countryId: item.countryId || null,
      code: item.code.trim(),
      name: item.name.trim(),
      serialNumber: item.serialNumber ? item.serialNumber.trim() : '',
      unitId: Number(item.unitId),
      isActive: item.isActive === true || item.isActive === 1
    };

    const request$ = this.currentView() === 'add'
      ? this.equipmentService.create(payload)
      : this.equipmentService.update(item.id, payload);

    request$.pipe(
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: this.currentView() === 'add' ? 'Đã thêm mới thiết bị thành công!' : 'Đã cập nhật thiết bị thành công!'
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
              // Ignore
            }
          }
        } else if (err?.errors) {
          errorsObj = err.errors;
        }
        this.serverErrors.set(errorsObj);
        
        const errMsg = err?.error?.message || 'Có lỗi xảy ra khi lưu thông tin thiết bị.';
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
    this.equipmentService.toggleStatus(item.id, isLocking).subscribe({
      next: (res) => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: res.message || (isLocking ? 'Khóa thiết bị thành công!' : 'Mở khóa thiết bị thành công!')
        });
        this.loadItems();
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

  onDelete(item: any) {
    this.deleteTarget.set(item);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete() {
    const item = this.deleteTarget();
    if (!item) return;

    this.deleting.set(true);
    this.equipmentService.delete(item.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: `Đã xóa thiết bị "${item.name}" thành công!`
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
            detail: err?.error?.message || 'Không thể xóa thiết bị.'
          });
        }
      });
  }

  onCancelDelete() {
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  goBack() {
    this.currentView.set('list');
  }
}
