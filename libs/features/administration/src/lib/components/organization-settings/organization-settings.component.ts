import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { PaginatorModule } from 'primeng/paginator';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  DeleteConfirmDialogComponent,
  EcoPaginatorComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';

@Component({
  selector: 'app-organization-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, PaginatorModule, MenuModule, WfBreadcrumbComponent, EcoPaginatorComponent, DeleteConfirmDialogComponent],

  providers: [MessageService],
  templateUrl: './organization-settings.component.html',
  styleUrl: './organization-settings.component.scss'
})
export class OrganizationSettings implements OnInit {
  units = signal<any[]>([]);
  searchKeyword = signal<string>('');

  currentView = signal<'list' | 'add' | 'edit'>('list');
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentUnit = signal<any>({});

  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  actionMenuItems: MenuItem[] = [];

  // Lock/Unlock Confirmation
  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);

  // Quản lý trạng thái popup, đơn vị được chọn và request xóa.
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleteLoading = signal<boolean>(false);

  // Chuẩn hóa tên đơn vị hiển thị trong popup xác nhận xóa dùng chung.
  readonly deleteTargetLabel = computed(() => {
    const unit = this.deleteTarget();

    if (!unit) {
      return '';
    }

    return `${unit.name} (${unit.code})`;
  });

  // Form Validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});
  codeError = computed(() => {
    if (this.formSubmitted() && !this.currentUnit().code) return 'Mã phòng ban là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });
  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentUnit().name) return 'Tên phòng ban là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  onFieldChange(field: string) {
    this.currentUnit.update(unit => ({ ...unit }));
    this.serverErrors.update(errs => {
      const copy = { ...errs };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/organization-units`;
  searchStatus = signal<string>('');
  currentPage = signal(1);
  pageSize = signal(10);

  // Computed signal for filteredUnits
  filteredUnits = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const statusVal = this.searchStatus();
    let allUnits = this.units() || [];

    if (statusVal === 'active') {
      allUnits = allUnits.filter(u => u.isActive);
    } else if (statusVal === 'inactive') {
      allUnits = allUnits.filter(u => !u.isActive);
    }

    if (!kw) {
      return this.buildHierarchicalList(allUnits);
    }
    return allUnits.filter(u =>
      (u.code?.toLowerCase().includes(kw) ?? false) ||
      (u.name?.toLowerCase().includes(kw) ?? false) ||
      (u.description?.toLowerCase().includes(kw) ?? false)
    );
  });

  pagedUnits = computed(() => {
    const first = (this.currentPage() - 1) * this.pageSize();
    return this.filteredUnits().slice(first, first + this.pageSize());
  });

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    public authService: AuthService
  ) {}

  ngOnInit() {
    this.loadUnits();
  }

  openActionMenu(unit: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      ...(this.authService.hasPermission('ORGANIZATION_EDIT') ? [{ label: unit.isActive ? 'Khóa đơn vị' : 'Mở khóa đơn vị', title: unit.isActive ? 'Khóa đơn vị' : 'Mở khóa đơn vị', icon: unit.isActive ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal', command: () => this.onToggleStatusRequest(unit) }] : []),
      ...(this.authService.hasPermission('ORGANIZATION_EDIT') ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(unit) }] : []),
      ...(this.authService.hasPermission('ORGANIZATION_DELETE') ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(unit) }] : []),
    ];
    menu.toggle(event);
  }

  loadUnits() {
    this.loading.set(true);
    this.http.get<any[]>(this.apiUrl)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          this.units.set(Array.isArray(data) ? data : (data && Array.isArray((data as any).items) ? (data as any).items : (data && Array.isArray((data as any).value) ? (data as any).value : [])));
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải sơ đồ cây tổ chức.' });
        }
      });
  }

  onSearch() {
    this.currentPage.set(1);
  }

  onResetSearch() {
    this.searchKeyword.set('');
    this.searchStatus.set('');
    this.currentPage.set(1);
  }

  onUnitPageChange(event: { first?: number; rows?: number }) {
    const rows = Number(event.rows) || this.pageSize();
    const first = Number(event.first) || 0;
    this.pageSize.set(rows);
    this.currentPage.set(Math.floor(first / rows) + 1);
  }

  // Thuật toán DFS xây dựng danh sách phẳng thụt lề
  buildHierarchicalList(unitsList: any[] = this.units()): any[] {
    const result: any[] = [];
    const unitsSafe = unitsList || [];
    const rootNodes = unitsSafe.filter(u => !u.parentId || !unitsSafe.some(parent => parent.id === u.parentId));

    const visit = (node: any) => {
      result.push(node);
      const children = unitsSafe.filter(u => u.parentId === node.id);
      children.sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
      children.forEach(visit);
    };

    rootNodes.sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
    rootNodes.forEach(visit);

    // Thêm các nút bị mồ côi nếu có lỗi dữ liệu để tránh mất bản ghi hiển thị
    unitsSafe.forEach(u => {
      if (!result.includes(u)) {
        result.push(u);
      }
    });

    return result;
  }

  getIndentLevel(unit: any): number {
    let level = 0;
    let parentId = unit.parentId;
    while (parentId) {
      const parent = this.units().find(u => u.id === parentId);
      if (parent && parent.id !== unit.id) {
        level++;
        parentId = parent.parentId;
      } else {
        break;
      }
    }
    return level;
  }

  getUnitName(id: number): string {
    const unit = this.units().find(u => u.id === id);
    return unit ? unit.name : `Đơn vị #${id}`;
  }

  // Danh sách đơn vị cấp trên hợp lệ (loại trừ chính nó và các con của nó để tránh vòng lặp cây)
  getEligibleParents(currentId: number | null): any[] {
    const allUnits = this.units() || [];
    if (!currentId) return allUnits;

    // Tìm danh sách ID con trực tiếp và gián tiếp
    const childrenIds = new Set<number>();
    const findChildren = (pid: number) => {
      allUnits.forEach(u => {
        if (u.parentId === pid) {
          childrenIds.add(u.id);
          findChildren(u.id);
        }
      });
    };
    findChildren(currentId);

    return allUnits.filter(u => u.id !== currentId && !childrenIds.has(u.id));
  }

  onAddNew() {
    if (!this.authService.hasPermission('ORGANIZATION_CREATE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới đơn vị phòng ban.' });
      return;
    }
    this.isEdit.set(false);
    this.currentUnit.set({ code: '', name: '', parentId: null, description: '', identifier: '', sortOrder: 0, isActive: true });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Thêm mới đơn vị phòng ban');
    this.currentView.set('add');
  }

  onToggleStatusRequest(unit: any) {
    this.lockUnlockTarget.set(unit);
    this.showLockUnlockConfirm.set(true);
  }

  onCancelLockUnlock() {
    this.showLockUnlockConfirm.set(false);
    this.lockUnlockTarget.set(null);
  }

  onConfirmLockUnlock() {
    const unit = this.lockUnlockTarget();
    if (!unit) return;
    if (!this.authService.hasPermission('ORGANIZATION_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa đơn vị phòng ban.' });
      return;
    }
    const updated = { ...unit, isActive: !unit.isActive };
    this.lockUnlockLoading.set(true);
    this.http.put(`${this.apiUrl}/${unit.id}`, updated)
      .pipe(finalize(() => this.lockUnlockLoading.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `${unit.isActive ? 'Khóa' : 'Mở khóa'} đơn vị thành công!`
          });
          this.showLockUnlockConfirm.set(false);
          this.lockUnlockTarget.set(null);
          this.loadUnits();
        },
        error: (err) => {
          const detailMsg = err?.error?.message || err?.message || `Không thể ${unit.isActive ? 'khóa' : 'mở khóa'} đơn vị.`;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
        }
      });
  }

  onEdit(unit: any) {
    if (!this.authService.hasPermission('ORGANIZATION_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa đơn vị phòng ban.' });
      return;
    }
    this.isEdit.set(true);
    this.currentUnit.set({ ...unit });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Chỉnh sửa đơn vị phòng ban');
    this.currentView.set('edit');
  }

  onSaveUnit() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});
    if (this.codeError() || this.nameError()) {
      return;
    }

    const unitDraft = this.currentUnit();

    this.saving.set(true);
    // Đảm bảo parentId là null hoặc số
    if (unitDraft.parentId === 'null' || unitDraft.parentId === null) {
      unitDraft.parentId = null;
    } else {
      unitDraft.parentId = Number(unitDraft.parentId);
    }

    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${unitDraft.id}`, unitDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Đã cập nhật thông tin phòng ban thành công!' });
            this.loadUnits();
            this.currentView.set('list');
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
                  // ignore
                }
              }
            } else if (err?.errors) {
              errorsObj = err.errors;
            }
            this.serverErrors.set(errorsObj);
            const detailMsg = err?.error?.message || err?.message || 'Không thể chỉnh sửa đơn vị.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
    } else {
      this.http.post<any>(this.apiUrl, unitDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: (created) => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo đơn vị phòng ban mới thành công!' });
            this.loadUnits();
            this.currentView.set('list');
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
                  // ignore
                }
              }
            } else if (err?.errors) {
              errorsObj = err.errors;
            }
            this.serverErrors.set(errorsObj);
            const detailMsg = err?.error?.message || err?.message || 'Không thể thêm mới đơn vị.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
    }
  }

  onDelete(unit: any): void {
    // Không cho phép xóa đơn vị vẫn còn đơn vị con trực thuộc.
    const hasChildren = this.units().some(
      child => child.parentId === unit.id
    );

    if (hasChildren) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Cảnh báo',
        detail: 'Không thể xóa đơn vị này vì còn đơn vị trực thuộc chưa được xóa.'
      });
      return;
    }

    // Lưu đơn vị được chọn và mở popup xác nhận xóa.
    this.deleteTarget.set(unit);
    this.showDeleteConfirm.set(true);
  }

  onCancelDelete(): void {
    // Không cho đóng popup khi request xóa đang được xử lý.
    if (this.deleteLoading()) {
      return;
    }

    this.closeDeleteDialog();
  }

  onConfirmDelete(): void {
    const unit = this.deleteTarget();

    // Chặn request trùng khi người dùng bấm nút Xóa nhiều lần.
    if (!unit || this.deleteLoading()) {
      return;
    }

    this.deleteLoading.set(true);

    this.http
      .delete(`${this.apiUrl}/${unit.id}`)
      .pipe(
        // Luôn tắt loading dù request thành công hay thất bại.
        finalize(() => this.deleteLoading.set(false))
      )
      .subscribe({
        next: () => {
          this.closeDeleteDialog();

          this.messageService.add({
            severity: 'success',
            summary: 'Xóa thành công',
            detail: 'Đã xóa đơn vị thành công!'
          });

          this.loadUnits();
        },
        error: (err) => {
          const detailMsg =
            err?.error?.message ||
            err?.message ||
            'Xóa đơn vị thất bại.';

          // Giữ popup mở để người dùng có thể xem lỗi hoặc thử lại.
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: detailMsg
          });
        }
      });
  }

  private closeDeleteDialog(): void {
    // Đóng popup và giải phóng bản ghi đang được chọn.
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }
}
