import { Component, OnInit, signal, computed, effect } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  buildAssignedMenuSummary,
  buildMenuPermissionTree as buildMenuPermissionTreeFromLookup,
} from '../../utils/menu-permission-tree.util';

@Component({
  selector: 'app-role-management',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, MenuModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './role-management.component.html',
  styleUrl: './role-management.component.scss'
})
export class RoleManagement implements OnInit {
  private static readonly ROLE_CODE_PATTERN = /^[A-Za-z0-9_]+$/;

  roles = signal<any[]>([]);
  searchKeyword = signal<string>('');
  totalCount = signal<number>(0);
  organizationUnits = signal<any[]>([]);
  isCentralAdmin = computed(() => {
    const userRoles = this.authService.getUserRoles();
    return userRoles.includes('ADMIN') || userRoles.includes('SUPER_ADMIN');
  });
  
  currentView = signal<'list' | 'add' | 'edit' | 'permission'>('list');
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentRole = signal<any>({});

  permissionDialogHeader = signal<string>('');
  permissionTab = signal<'permissions' | 'accounts'>('permissions');
  activeRoleForPermission = signal<any>(null);
  availablePermissionGroups = signal<any[]>([]);
  selectedPermissionGroupIds = signal<number[]>([]);
  expandedPermissionGroupIds = signal<Set<number>>(new Set<number>());
  permissionGroupsLoading = signal<boolean>(false);
  permissionGroupMenuSummaries = computed(() => {
    const summaries = new Map<number, ReturnType<typeof buildAssignedMenuSummary>>();
    for (const group of this.availablePermissionGroups()) {
      summaries.set(
        group.id,
        buildAssignedMenuSummary(this.menus(), this.systemPermissions(), group.permissionCodes || [])
      );
    }
    return summaries;
  });

  roleUsers = signal<any[]>([]);
  roleUsersKeyword = signal<string>('');
  roleUsersTotal = signal<number>(0);
  roleUsersPage = signal<number>(1);
  roleUsersPageSize = signal<number>(10);
  roleUsersLoading = signal<boolean>(false);
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  savingPermissions = signal<boolean>(false);
  actionMenuItems: MenuItem[] = [];

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
    const code = this.currentRole().code ?? '';
    if (this.formSubmitted() && !code) return 'Mã vai trò là bắt buộc';
    if (this.formSubmitted() && code.length > 50) return 'Mã vai trò không được vượt quá 50 ký tự';
    if (this.formSubmitted() && !RoleManagement.ROLE_CODE_PATTERN.test(code)) {
      return 'Mã vai trò chỉ được chứa chữ cái không dấu, chữ số và dấu gạch dưới; không được chứa khoảng trắng';
    }
    return this.serverErrors().code || this.serverErrors().Code || '';
  });
  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentRole().name) return 'Tên vai trò là bắt buộc';
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

  menus = signal<any[]>([]);
  systemPermissions = signal<any[]>([]);
  menuPermissionTree = signal<any[]>([]);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/roles`;

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

  roleUsersTotalPages = computed(() => {
    return Math.ceil(this.roleUsersTotal() / this.roleUsersPageSize()) || 1;
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

    effect(() => {
      const tab = this.permissionTab();
      const role = this.activeRoleForPermission();
      const page = this.roleUsersPage();
      const size = this.roleUsersPageSize();
      const kw = this.roleUsersKeyword();
      if (this.currentView() === 'permission' && tab === 'accounts' && role?.id) {
        this.loadRoleUsers();
      }
    }, { allowSignalWrites: true });
  }

  ngOnInit() {
    this.loadRoles();
    this.loadMenus();
    this.loadSystemPermissions();
    if (this.isCentralAdmin()) {
      this.loadOrganizationUnits();
    }
  }

  openActionMenu(role: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      ...(this.authService.hasPermission('ROLE_MANAGE') || this.authService.hasPermission('PERMISSION_MANAGE') ? [{ label: 'Phân quyền', title:'Phân quyền', icon: 'pi pi-shield', command: () => this.onAssignPermissions(role) }] : []),
      ...(this.authService.hasPermission('ROLE_EDIT') ? [{ label: role.isActive ? 'Khóa vai trò' : 'Mở khóa vai trò', title: role.isActive ? 'Khóa vai trò' : 'Mở khóa vai trò', icon: role.isActive ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal', command: () => this.onToggleStatusRequest(role) }] : []),
      ...(this.authService.hasPermission('ROLE_EDIT') ? [{ label: 'Chỉnh sửa', title:'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(role) }] : []),
      ...(this.authService.hasPermission('ROLE_DELETE') ? [{ label: 'Xóa', title:'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(role) }] : []),
    ];
    menu.toggle(event);
  }

  loadOrganizationUnits() {
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/organization-units/lookup`).subscribe({
      next: (res) => this.organizationUnits.set(Array.isArray(res) ? res : []),
      error: () => this.organizationUnits.set([])
    });
  }

  loadRoles() {
    this.loading.set(true);
    this.http.get<any>(`${this.apiUrl}?page=${this.currentPage()}&pageSize=${this.pageSize()}&keyword=${this.searchKeyword() || ''}`)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const list = res?.items || [];
          this.roles.set(list);
          this.totalCount.set(res?.totalCount || 0);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách vai trò.' });
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
    if (!this.authService.hasPermission('ROLE_CREATE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới vai trò.' });
      return;
    }
    this.isEdit.set(false);
    
    if (this.isCentralAdmin()) {
      this.currentRole.set({ code: '', name: '', description: '', scopeTypeId: 1, organizationUnitId: null, isActive: true });
    } else {
      this.currentRole.set({ 
        code: '', 
        name: '', 
        description: '', 
        scopeTypeId: 2, 
        organizationUnitId: this.authService.getUserUnitId(), 
        isActive: true 
      });
    }

    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Thêm mới vai trò');
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
    if (!this.authService.hasPermission('ROLE_EDIT')) {
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
    if (!this.authService.hasPermission('ROLE_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa vai trò.' });
      return;
    }
    this.isEdit.set(true);
    this.currentRole.set({ ...role });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Chỉnh sửa vai trò');
    this.currentView.set('edit');
  }

  onSaveRole() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});
    if (this.codeError() || this.nameError()) {
      return;
    }

    const roleDraft = this.currentRole();
    this.saving.set(true);
    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${roleDraft.id}`, roleDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật thông tin vai trò thành công!' });
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
            const detailMsg = err?.error?.message || err?.message || 'Cập nhật vai trò thất bại.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
    } else {
      this.http.post<any>(this.apiUrl, roleDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: (created) => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo vai trò mới thành công!' });
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
            const detailMsg = err?.error?.message || err?.message || 'Tạo vai trò thất bại.';
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
    if (!this.authService.hasPermission('ROLE_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền phân bổ nhóm quyền cho vai trò.' });
      return;
    }
    this.activeRoleForPermission.set({ ...role });
    this.permissionDialogHeader.set(`Phân bổ nhóm quyền: ${role.name}`);
    this.permissionTab.set('permissions');
    this.roleUsersKeyword.set('');
    this.roleUsersPage.set(1);
    this.selectedPermissionGroupIds.set([]);
    this.availablePermissionGroups.set([]);
    this.expandedPermissionGroupIds.set(new Set<number>());
    this.permissionGroupsLoading.set(true);

    this.http.get<any[]>(`${this.apiUrl}/${role.id}/available-permission-groups`)
      .pipe(finalize(() => this.permissionGroupsLoading.set(false)))
      .subscribe({
      next: (res) => {
        const list = Array.isArray(res) ? res : [];
        const normalized = list.map((group: any) => ({
          ...group,
          id: Number(group.id ?? group.Id),
          code: group.code ?? group.Code ?? '',
          name: group.name ?? group.Name ?? '',
          groupType: group.groupType ?? group.GroupType ?? '',
          organizationUnitId: group.organizationUnitId ?? group.OrganizationUnitId ?? null,
          organizationUnitName: group.organizationUnitName ?? group.OrganizationUnitName ?? null,
          organizationUnitIds: group.organizationUnitIds ?? group.OrganizationUnitIds ?? [],
          organizationUnitNames: group.organizationUnitNames ?? group.OrganizationUnitNames ?? null,
          permissionCodes: group.permissionCodes || group.PermissionCodes || [],
          isAssigned: group.isAssigned ?? group.IsAssigned ?? false
        })).filter((group: any) => Number.isFinite(group.id) && group.id > 0);
        this.availablePermissionGroups.set(normalized);
        this.selectedPermissionGroupIds.set(
          normalized.filter((group: any) => group.isAssigned).map((group: any) => group.id)
        );
        this.currentView.set('permission');
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách nhóm quyền khả dụng.' });
      }
    });
  }

  isPermissionGroupExpanded(groupId: number): boolean {
    return this.expandedPermissionGroupIds().has(groupId);
  }

  togglePermissionGroupDetails(groupId: number, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.expandedPermissionGroupIds.update((current) => {
      const next = new Set(current);
      if (next.has(groupId)) {
        next.delete(groupId);
      } else {
        next.add(groupId);
      }
      return next;
    });
  }

  getPermissionGroupMenuSummary(groupId: number) {
    return this.permissionGroupMenuSummaries().get(groupId) || [];
  }

  getPermissionGroupMenuCount(groupId: number): number {
    return this.getPermissionGroupMenuSummary(groupId)
      .reduce((total, parent) => total + (parent.children.length || 1), 0);
  }

  isPermissionGroupSelected(groupId: number): boolean {
    return this.selectedPermissionGroupIds().includes(groupId);
  }

  togglePermissionGroup(groupId: number) {
    this.selectedPermissionGroupIds.update((prev) => {
      const idx = prev.indexOf(groupId);
      if (idx > -1) {
        const copy = [...prev];
        copy.splice(idx, 1);
        return copy;
      }
      return [...prev, groupId];
    });
  }

  switchPermissionTab(tab: 'permissions' | 'accounts') {
    this.permissionTab.set(tab);
    if (tab === 'accounts') {
      this.roleUsersPage.set(1);
    }
  }

  loadRoleUsers() {
    const role = this.activeRoleForPermission();
    if (!role?.id) return;

    this.roleUsersLoading.set(true);
    this.http.get<any>(
      `${this.apiUrl}/${role.id}/users?page=${this.roleUsersPage()}&pageSize=${this.roleUsersPageSize()}&keyword=${encodeURIComponent(this.roleUsersKeyword() || '')}`
    )
      .pipe(finalize(() => this.roleUsersLoading.set(false)))
      .subscribe({
        next: (res) => {
          this.roleUsers.set(res?.items || []);
          this.roleUsersTotal.set(res?.totalCount || 0);
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách tài khoản.' });
          this.roleUsers.set([]);
          this.roleUsersTotal.set(0);
        }
      });
  }

  onRoleUsersSearch() {
    this.roleUsersPage.set(1);
    this.loadRoleUsers();
  }

  roleUsersPrevPage() {
    if (this.roleUsersPage() > 1) {
      this.roleUsersPage.update((p) => p - 1);
    }
  }

  roleUsersNextPage() {
    if (this.roleUsersPage() < this.roleUsersTotalPages()) {
      this.roleUsersPage.update((p) => p + 1);
    }
  }

  onRoleUsersPageSizeChange(event: Event) {
    this.roleUsersPageSize.set(Number((event.target as HTMLSelectElement).value));
    this.roleUsersPage.set(1);
  }

  onSavePermissions() {
    const activeRole = this.activeRoleForPermission();
    if (!activeRole) return;
    
    this.savingPermissions.set(true);
    this.http.put(`${this.apiUrl}/${activeRole.id}/permission-groups`, this.selectedPermissionGroupIds())
      .pipe(finalize(() => this.savingPermissions.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã phân bổ nhóm quyền cho vai trò!' });
          this.currentView.set('list');
        },
        error: (err) => {
          const detail = err?.error?.message || 'Lưu phân bổ nhóm quyền thất bại.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail });
        }
      });
  }
}
