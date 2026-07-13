import { Component, OnInit, signal, computed, effect } from '@angular/core';
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
import { buildMenuPermissionTree as buildMenuPermissionTreeFromLookup } from '../../utils/menu-permission-tree.util';

@Component({
  selector: 'app-unit-permission-group-management',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './unit-permission-group-management.component.html',
  styleUrl: './unit-permission-group-management.component.scss'
})
export class UnitPermissionGroupManagement implements OnInit {
  organizationUnits = signal<any[]>([]);
  filterOrganizationUnitId = signal<number | null>(null);
  roles = signal<any[]>([]);
  searchKeyword = signal<string>('');
  totalCount = signal<number>(0);
  
  currentView = signal<'list' | 'add' | 'edit' | 'permission'>('list');
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentRole = signal<any>({});

  permissionDialogHeader = signal<string>('');
  activeRoleForPermission = signal<any>(null);
  systemPermissions = signal<any[]>([]);
  selectedPermissionCodes = signal<string[]>([]);
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  savingPermissions = signal<boolean>(false);

  // Lock/Unlock Confirmation
  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Form Validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});
  codeError = computed(() => {
    if (this.formSubmitted() && !this.currentRole().code) return 'Mã vai trò là bắt buộc';
    return this.serverErrors().code || this.serverErrors().Code || '';
  });
  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentRole().name) return 'Tên vai trò là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });
  unitError = computed(() => {
    if (this.formSubmitted() && !this.currentRole().organizationUnitId) return 'Đơn vị là bắt buộc';
    return this.serverErrors().organizationUnitId || this.serverErrors().OrganizationUnitId || '';
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

  menus = signal<any[]>([]);
  menuPermissionTree = signal<any[]>([]);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/unit-permission-groups`;

  // Computed signal for filteredRoles
  filteredRoles = computed(() => {
    return this.roles();
  });

  // Paginated roles
  paginatedRoles = computed(() => {
    return this.roles();
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

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService,
    public authService: AuthService
  ) {
    effect(() => {
      const kw = this.searchKeyword();
      this.currentPage.set(1);
    }, { allowSignalWrites: true });

    effect(() => {
      const page = this.currentPage();
      const size = this.pageSize();
      const kw = this.searchKeyword();
      this.loadRoles();
    }, { allowSignalWrites: true });


  }

  ngOnInit() {
    this.loadOrganizationUnits();
    this.loadRoles();
    this.loadMenus();
    this.loadSystemPermissions();
  }

  loadOrganizationUnits() {
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/organization-units/lookup`).subscribe({
      next: (res) => this.organizationUnits.set(Array.isArray(res) ? res : []),
      error: () => this.organizationUnits.set([])
    });
  }

  loadRoles() {
    this.loading.set(true);
    let url = `${this.apiUrl}?page=${this.currentPage()}&pageSize=${this.pageSize()}&keyword=${this.searchKeyword() || ''}`;
    if (this.filterOrganizationUnitId()) {
      url += `&organizationUnitId=${this.filterOrganizationUnitId()}`;
    }
    this.http.get<any>(url)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const list = res?.items || [];
          this.roles.set(list);
          this.totalCount.set(res?.totalCount || 0);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách nhóm quyền.' });
          this.roles.set([]);
          this.totalCount.set(0);
        }
      });
  }

  loadMenus() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/menus/lookup`).subscribe({
      next: (res) => {
        this.menus.set(Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : [])));
        this.updateTree();
      },
      error: (err) => {
        console.error('Không thể tải danh sách menu:', err);
      }
    });
  }

  loadSystemPermissions() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/permissions/lookup`).subscribe({
      next: (res) => {
        this.systemPermissions.set(Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : [])));
        this.updateTree();
      },
      error: (err) => {
        console.error('Không thể tải danh sách quyền hệ thống:', err);
      }
    });
  }

  updateTree() {
    if (this.menus().length > 0 && this.systemPermissions().length > 0) {
      this.buildMenuPermissionTree(this.menus(), this.systemPermissions());
    }
  }

  buildMenuPermissionTree(menusList: any[], permissions: any[]) {
    this.menuPermissionTree.set(buildMenuPermissionTreeFromLookup(menusList, permissions));
  }

  onSearch() {
    this.currentPage.set(1);
    this.loadRoles();
  }

  onAddNew() {
    if (!this.authService.hasPermission('UNIT_PERMISSION_GROUP_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới nhóm quyền.' });
      return;
    }
    this.isEdit.set(false);
    this.currentRole.set({ code: '', name: '', description: '', isActive: true, organizationUnitId: null });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Thêm mới nhóm quyền');
    this.currentView.set('add');
  }

  onToggleStatusRequest(role: any) {
    this.lockUnlockTarget.set(role);
    this.showLockUnlockConfirm.set(true);
  }

  onCancelLockUnlock() {
    this.showLockUnlockConfirm.set(false);
    this.lockUnlockTarget.set(null);
  }

  onConfirmLockUnlock() {
    const role = this.lockUnlockTarget();
    if (!role) return;
    if (!this.authService.hasPermission('UNIT_PERMISSION_GROUP_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa nhóm quyền.' });
      return;
    }
    const updated = { ...role, isActive: !role.isActive };
    this.lockUnlockLoading.set(true);
    this.http.put(`${this.apiUrl}/${role.id}`, updated)
      .pipe(finalize(() => this.lockUnlockLoading.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `${role.isActive ? 'Khóa' : 'Mở khóa'} vai trò thành công!`
          });
          this.showLockUnlockConfirm.set(false);
          this.lockUnlockTarget.set(null);
          this.loadRoles();
        },
        error: (err) => {
          const detailMsg = err?.error?.message || err?.message || `Không thể ${role.isActive ? 'khóa' : 'mở khóa'} vai trò.`;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
        }
      });
  }

  onEdit(role: any) {
    if (!this.authService.hasPermission('UNIT_PERMISSION_GROUP_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa nhóm quyền.' });
      return;
    }
    this.isEdit.set(true);
    this.currentRole.set({ ...role });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Chỉnh sửa nhóm quyền');
    this.currentView.set('edit');
  }

  onSaveRole() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});
    if (this.codeError() || this.nameError() || this.unitError()) {
      return;
    }

    const roleDraft = this.currentRole();
    this.saving.set(true);
    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${roleDraft.id}`, roleDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật thông tin nhóm quyền thành công!' });
            this.loadRoles();
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
            const detailMsg = err?.error?.message || err?.message || 'Cập nhật nhóm quyền thất bại.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
    } else {
      this.http.post<any>(this.apiUrl, roleDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: (created) => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo nhóm quyền mới thành công!' });
            this.loadRoles();
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
            const detailMsg = err?.error?.message || err?.message || 'Tạo nhóm quyền thất bại.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
    }
  }

  onDelete(role: any) {
    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa vai trò ${role.name} (${role.code})?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      acceptButtonStyleClass: 'btn-save',
      rejectButtonStyleClass: 'btn-cancel',
      accept: () => {
        this.http.delete(`${this.apiUrl}/${role.id}`).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: 'Đã xóa vai trò thành công!' });
            this.loadRoles();
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể xóa vai trò.' });
          }
        });
      }
    });
  }

  onAssignPermissions(role: any) {
    if (!this.authService.hasPermission('UNIT_PERMISSION_GROUP_MANAGE') && !this.authService.hasPermission('PERMISSION_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền cấu hình nhóm quyền này.' });
      return;
    }
    this.activeRoleForPermission.set({ ...role });
    this.permissionDialogHeader.set(`Phân quyền nhóm: ${role.name}`);
    this.selectedPermissionCodes.set([]);

    this.http.get<any>(`${this.apiUrl}/${role.id}/permissions`).subscribe({
      next: (res) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : []));
        this.selectedPermissionCodes.set(list);
        this.expandAllPermissionGroups();
        this.currentView.set('permission');
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải quyền đã gán.' });
      }
    });
  }

  private expandAllPermissionGroups() {
    this.menuPermissionTree.update((tree) =>
      tree.map((parent) => ({
        ...parent,
        expanded: parent.expanded ?? this.hasAssignablePermissions(parent),
        subMenus: (parent.subMenus || []).map((sub: any) => ({ ...sub, expanded: true }))
      }))
    );
  }

  private hasAssignablePermissions(parent: any): boolean {
    if ((parent.permissions || []).length > 0) {
      return true;
    }
    return (parent.subMenus || []).some((sub: any) => (sub.permissions || []).length > 0);
  }

  getParentPermissionCodes(parent: any): string[] {
    const codes: string[] = (parent.permissions || []).map((p: any) => p.code);
    (parent.subMenus || []).forEach((sub: any) => {
      codes.push(...this.getSubmenuPermissionCodes(sub));
    });
    return codes;
  }

  getSubmenuPermissionCodes(sub: any): string[] {
    return (sub.permissions || []).map((p: any) => p.code);
  }

  isAllPermissionsChecked(codes: string[]): boolean {
    return codes.length > 0 && codes.every((code) => this.isPermissionChecked(code));
  }

  getPermissionInputId(parentId: number, subId: number | null, code: string): string {
    return `perm-${parentId}-${subId ?? 'root'}-${code}`;
  }

  toggleAllPermissions(codes: string[]) {
    if (this.isAllPermissionsChecked(codes)) {
      this.selectedPermissionCodes.update((prev) => prev.filter((code) => !codes.includes(code)));
      return;
    }
    this.selectedPermissionCodes.update((prev) => Array.from(new Set([...prev, ...codes])));
  }

  toggleParentMenu(parent: any) {
    parent.expanded = !parent.expanded;
    this.menuPermissionTree.set([...this.menuPermissionTree()]);
  }

  toggleSubMenu(sub: any) {
    sub.expanded = !(sub.expanded ?? true);
    this.menuPermissionTree.set([...this.menuPermissionTree()]);
  }



  isPermissionChecked(code: string): boolean {
    return this.selectedPermissionCodes().includes(code);
  }

  togglePermission(code: string) {
    this.selectedPermissionCodes.update(prev => {
      const idx = prev.indexOf(code);
      if (idx > -1) {
        const copy = [...prev];
        copy.splice(idx, 1);
        return copy;
      } else {
        return [...prev, code];
      }
    });
  }

  onSavePermissions() {
    const activeRole = this.activeRoleForPermission();
    if (!activeRole) return;
    
    this.savingPermissions.set(true);
    this.http.post(`${this.apiUrl}/${activeRole.id}/permissions`, this.selectedPermissionCodes())
      .pipe(finalize(() => this.savingPermissions.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Phân quyền thành công', detail: 'Đã lưu thay đổi phân quyền hệ thống!' });
          this.currentView.set('list');
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu phân quyền vai trò thất bại.' });
        }
      });
  }
}
