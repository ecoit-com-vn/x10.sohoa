import { Component, OnInit, signal, computed } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-organization-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
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

  // Lock/Unlock Confirmation
  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);

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

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService,
    public authService: AuthService
  ) {}

  ngOnInit() {
    this.loadUnits();
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

  onDelete(unit: any) {
    // Kiểm tra xem đơn vị này có đơn vị con không trước khi xóa
    const hasChildren = this.units().some(u => u.parentId === unit.id);
    if (hasChildren) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Không thể xóa đơn vị này vì còn đơn vị trực thuộc chưa được xóa.' });
      return;
    }

    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa phòng ban/đơn vị ${unit.name} (${unit.code})?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      accept: () => {
        this.http.delete(`${this.apiUrl}/${unit.id}`).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: 'Đã xóa đơn vị thành công!' });
            this.loadUnits();
          },
          error: (err) => {
            const detailMsg = err?.error?.message || err?.message || 'Xóa đơn vị thất bại.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
      }
    });
  }
}
