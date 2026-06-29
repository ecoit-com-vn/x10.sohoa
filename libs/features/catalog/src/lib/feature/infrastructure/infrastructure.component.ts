import { Component, OnInit, signal, computed, inject, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '@sohoa.frontend/shared/core';
import { InfrastructureService } from '../../data-access/infrastructure.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-infrastructure',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, SelectModule, DialogModule],
  providers: [MessageService],
  templateUrl: './infrastructure.component.html',
  styleUrl: './infrastructure.component.scss'
})
export class InfrastructureComponent implements OnInit {
  private infraService = inject(InfrastructureService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private route = inject(ActivatedRoute);

  // Dynamic Route Data
  infraTypeId = signal<number>(1);
  pageTitle = signal<string>('Danh mục cơ sở hạ tầng');

  // States
  items = signal<any[]>([]);
  orgUnits = signal<any[]>([]);
  gridTypes = signal<any[]>([]);
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

  constructor() {
    effect(() => {
      // Re-trigger load when page, pageSize, or infraTypeId changes
      this.currentPage();
      this.pageSize();
      this.infraTypeId();
      this.loadItems();
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

    // Listen to route data changes to adapt dynamically
    this.route.data.subscribe(data => {
      if (data) {
        this.infraTypeId.set(data['infraTypeId'] || 1);
        this.pageTitle.set(data['title'] || 'Danh mục cơ sở hạ tầng');
        // Reset state on route switch
        this.currentPage.set(1);
        this.searchKeyword.set('');
        this.searchStatus.set('');
        this.currentView.set('list');
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
    const isLocking = item.isActive === 1 || item.isActive === true;
    this.infraService.toggleStatus(this.infraTypeId(), item.id, isLocking).subscribe({
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
}
