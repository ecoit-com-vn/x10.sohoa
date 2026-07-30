// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\administration\user-group.component.ts
import { Component, OnInit, signal, computed, effect } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { PickListModule } from 'primeng/picklist';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-user-group',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ToastModule,
    PickListModule,
    MenuModule,
    WfBreadcrumbComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './user-group.component.html',
  styleUrl: './user-group.component.scss'
})
export class UserGroupComponent implements OnInit {
  groups = signal<any[]>([]);
  searchKeyword = signal<string>('');
  searchStatus = signal<string>(''); // '' (All), 'active' (Hoạt động), 'inactive' (Ngưng hoạt động)
  totalCount = signal<number>(0);

  currentView = signal<'list' | 'add' | 'edit' | 'member' | 'role' | 'permission'>('list');
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentGroup = signal<any>({});
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Form Validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});
  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentGroup().name) return 'Tên nhóm người dùng là bắt buộc';
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

  // Thành viên
  memberDialogHeader = signal<string>('');
  activeGroupForMember = signal<any>(null);
  sourceUsers = signal<any[]>([]);
  targetUsers = signal<any[]>([]);
  savingMembers = signal<boolean>(false);

  // Vai trò
  roleDialogHeader = signal<string>('');
  activeGroupForRole = signal<any>(null);
  sourceRoles = signal<any[]>([]);
  targetRoles = signal<any[]>([]);
  savingRoles = signal<boolean>(false);

  // Quyền trực tiếp nhóm
  permissionDialogHeader = signal<string>('');
  activeGroupForPermission = signal<any>(null);
  systemPermissions = signal<any[]>([]);
  selectedPermissionCodes = signal<string[]>([]);
  savingPermissions = signal<boolean>(false);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/user-groups`;

  // Computed signal for filteredGroups
  filteredGroups = computed(() => {
    return this.groups();
  });

  // Paginated groups
  paginatedGroups = computed(() => {
    return this.groups();
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

  actionMenuItems: MenuItem[] = [];

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    public authService: AuthService
  ) {
    effect(() => {
      this.searchKeyword();
      this.searchStatus();
      this.currentPage.set(1);
    }, { allowSignalWrites: true });

    effect(() => {
      const page = this.currentPage();
      const size = this.pageSize();
      this.searchKeyword();
      this.searchStatus();
      this.loadGroups();
    }, { allowSignalWrites: true });
  }

  openActionMenu(group: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      ...(this.authService.hasPermission('USER_GROUP_MANAGE') ? [
        { label: 'Thành viên', title:'Thành viên', icon: 'pi pi-users color-blue', command: () => this.onManageMembers(group) },
        { label: 'Vai trò', title:'Vai trò', icon: 'pi pi-shield', command: () => this.onManageRoles(group) },
        { label: 'Phân quyền trực tiếp', title: 'Phân quyền trực tiếp', icon: 'pi pi-key color-blue', command: () => this.onManagePermissions(group) },
      ] : []),
      ...(this.authService.hasPermission('USER_GROUP_EDIT') ? [{ label: 'Chỉnh sửa', title:'Chỉnh sửa', icon: 'pi pi-pencil color-teal', command: () => this.onEdit(group) }] : []),
      ...(this.authService.hasPermission('USER_GROUP_EDIT') ? [{ label: group.isActive ? 'Khóa nhóm' : 'Mở khóa', title: group.isActive ? 'Khóa nhóm' : 'Mở khóa', icon: group.isActive ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal', command: () => this.onToggleStatusRequest(group) }] : []),
      ...(this.authService.hasPermission('USER_GROUP_DELETE') ? [{ label: 'Xóa', title:'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(group) }] : []),
    ];
    menu.toggle(event);
  }

  ngOnInit() {
    this.loadGroups();
    this.loadSystemPermissions();
  }

  loadGroups() {
    this.loading.set(true);
    let url = `${this.apiUrl}?page=${this.currentPage()}&pageSize=${this.pageSize()}&keyword=${this.searchKeyword() || ''}`;
    const statusVal = this.searchStatus();
    if (statusVal !== '') {
      url += `&isActive=${statusVal === 'active'}`;
    }
    this.http.get<any>(url)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const list = res?.items || [];
          this.groups.set(list);
          this.totalCount.set(res?.totalCount || 0);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách nhóm.' });
          this.groups.set([]);
          this.totalCount.set(0);
        }
      });
  }

  onSearch() {
    // Tự động thông qua computed
  }

  onAddNew() {
    if (!this.authService.hasPermission('USER_GROUP_CREATE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới nhóm người dùng.' });
      return;
    }
    this.isEdit.set(false);
    this.currentGroup.set({ name: '', description: '', isActive: true });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Thêm mới nhóm người dùng');
    this.currentView.set('add');
  }

  onToggleStatusRequest(group: any) {
    this.lockUnlockTarget.set(group);
    this.showLockUnlockConfirm.set(true);
  }

  onConfirmLockUnlock() {
    const group = this.lockUnlockTarget();
    if (!group) return;
    if (!this.authService.hasPermission('USER_GROUP_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa nhóm người dùng.' });
      return;
    }
    const updated = { ...group, isActive: !group.isActive };
    this.lockUnlockLoading.set(true);
    this.http.put(`${this.apiUrl}/${group.id}`, updated)
      .pipe(finalize(() => this.lockUnlockLoading.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `${group.isActive ? 'Khóa' : 'Mở khóa'} nhóm người dùng thành công!`
          });
          this.showLockUnlockConfirm.set(false);
          this.lockUnlockTarget.set(null);
          this.loadGroups();
        },
        error: (err) => {
          const detailMsg = err?.error?.message || err?.message || `Không thể ${group.isActive ? 'khóa' : 'mở khóa'} nhóm người dùng.`;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
        }
      });
  }

  onCancelLockUnlock() {
    this.showLockUnlockConfirm.set(false);
    this.lockUnlockTarget.set(null);
  }

  onEdit(group: any) {
    if (!this.authService.hasPermission('USER_GROUP_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa nhóm người dùng.' });
      return;
    }
    this.isEdit.set(true);
    this.currentGroup.set({ ...group });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Chỉnh sửa nhóm người dùng');
    this.currentView.set('edit');
  }

  onSaveGroup() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});
    if (this.nameError()) {
      return;
    }

    const groupDraft = this.currentGroup();
    this.saving.set(true);
    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${groupDraft.id}`, groupDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật nhóm thành công!' });
            this.loadGroups();
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
            const detailMsg = err?.error?.message || err?.message || 'Không thể cập nhật nhóm.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
    } else {
      this.http.post(this.apiUrl, groupDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Thêm mới nhóm thành công!' });
            this.loadGroups();
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
            const detailMsg = err?.error?.message || err?.message || 'Không thể tạo nhóm mới.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
    }
  }

  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);

  // Chuẩn hóa tên nhóm hiển thị trong popup xác nhận xóa dùng chung.
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.name || '');

  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);

  onDelete(group: any): void {
    this.deleteTarget.set(group);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete(): void {
    const group = this.deleteTarget();

    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!group || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.http.delete(`${this.apiUrl}/${group.id}`)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: `Đã xóa nhóm người dùng "${group.name}" thành công!` });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadGroups();
        },
        error: (err) => {
          const detail =
            err?.error?.message ||
            err?.message ||
            'Xóa nhóm người dùng thất bại.';

          // Giữ popup mở để hiển thị lỗi backend và cho phép thử lại.
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail });
        }
      });
  }

  onCancelDelete(): void {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.deleting()) {
      return;
    }

    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  onManageMembers(group: any) {
    if (!this.authService.hasPermission('USER_GROUP_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền quản lý thành viên nhóm này.' });
      return;
    }
    this.activeGroupForMember.set(group);
    this.memberDialogHeader.set(`Quản lý thành viên nhóm: ${group.name}`);
    this.sourceUsers.set([]);
    this.targetUsers.set([]);
 
    // Load tất cả người dùng
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/users/lookup`).subscribe({
      next: (allUsers) => {
        // Load thành viên của nhóm
        this.http.get<any[]>(`${this.apiUrl}/${group.id}/members`).subscribe({
          next: (members) => {
            const allUsersList = Array.isArray(allUsers) ? allUsers : (allUsers && Array.isArray((allUsers as any).items) ? (allUsers as any).items : (allUsers && Array.isArray((allUsers as any).value) ? (allUsers as any).value : []));
            const membersList = Array.isArray(members) ? members : (members && Array.isArray((members as any).items) ? (members as any).items : (members && Array.isArray((members as any).value) ? (members as any).value : []));
            const memberIds = new Set(membersList.map((m: any) => m.id));
            this.targetUsers.set((allUsersList || []).filter((u: any) => memberIds.has(u.id)));
            this.sourceUsers.set((allUsersList || []).filter((u: any) => !memberIds.has(u.id)));
            this.currentView.set('member');
          },
          error: () => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải thành viên nhóm.' });
          }
        });
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách tài khoản.' });
      }
    });
  }

  onSaveMembers() {
    const activeGroup = this.activeGroupForMember();
    if (!activeGroup) return;
    this.savingMembers.set(true);
    const userIds = this.targetUsers().map(u => u.id);

    this.http.post(`${this.apiUrl}/${activeGroup.id}/members`, userIds)
      .pipe(finalize(() => this.savingMembers.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thay đổi thành viên nhóm!' });
          this.currentView.set('list');
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi thành viên nhóm thất bại.' });
        }
      });
  }

  onManageRoles(group: any) {
    if (!this.authService.hasPermission('USER_GROUP_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền quản lý vai trò nhóm này.' });
      return;
    }
    this.activeGroupForRole.set(group);
    this.roleDialogHeader.set(`Quản lý vai trò nhóm: ${group.name}`);
    this.sourceRoles.set([]);
    this.targetRoles.set([]);

    // Load tất cả vai trò
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/roles/lookup`).subscribe({
      next: (allRoles) => {
        // Load vai trò của nhóm
        this.http.get<any[]>(`${this.apiUrl}/${group.id}/roles`).subscribe({
          next: (roles) => {
            const allRolesList = Array.isArray(allRoles) ? allRoles : (allRoles && Array.isArray((allRoles as any).items) ? (allRoles as any).items : (allRoles && Array.isArray((allRoles as any).value) ? (allRoles as any).value : []));
            const rolesList = Array.isArray(roles) ? roles : (roles && Array.isArray((roles as any).items) ? (roles as any).items : (roles && Array.isArray((roles as any).value) ? (roles as any).value : []));
            const roleIds = new Set(rolesList.map((r: any) => r.id));
            this.targetRoles.set((allRolesList || []).filter((r: any) => roleIds.has(r.id)));
            this.sourceRoles.set((allRolesList || []).filter((r: any) => !roleIds.has(r.id)));
            this.currentView.set('role');
          },
          error: () => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải vai trò của nhóm.' });
          }
        });
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách vai trò.' });
      }
    });
  }

  onSaveRoles() {
    const activeGroup = this.activeGroupForRole();
    if (!activeGroup) return;
    this.savingRoles.set(true);
    const roleIds = this.targetRoles().map(r => r.id);

    this.http.post(`${this.apiUrl}/${activeGroup.id}/roles`, roleIds)
      .pipe(finalize(() => this.savingRoles.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thay đổi vai trò của nhóm!' });
          this.currentView.set('list');
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi vai trò nhóm thất bại.' });
        }
      });
  }

  loadSystemPermissions() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/permissions/lookup`).subscribe({
      next: (res: any) => {
        this.systemPermissions.set(Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : [])));
      },
      error: (err) => {
        console.error('Không thể tải danh sách quyền hệ thống:', err);
      }
    });
  }

  onManagePermissions(group: any) {
    if (!this.authService.hasPermission('USER_GROUP_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền gán quyền nhóm người dùng này.' });
      return;
    }
    this.activeGroupForPermission.set(group);
    this.permissionDialogHeader.set(`Phân quyền trực tiếp cho nhóm: ${group?.name || ''}`);
    this.selectedPermissionCodes.set([]);
    
    this.http.get<any>(`${this.apiUrl}/${group.id}/permissions`).subscribe({
      next: (res: any) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : []));
        this.selectedPermissionCodes.set(list);
        this.currentView.set('permission');
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách quyền nhóm đã gán.' });
      }
    });
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
    const activeGroup = this.activeGroupForPermission();
    if (!activeGroup) return;
    
    this.savingPermissions.set(true);
    this.http.post(`${this.apiUrl}/${activeGroup.id}/permissions`, this.selectedPermissionCodes())
      .pipe(finalize(() => this.savingPermissions.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thay đổi phân quyền trực tiếp cho nhóm người dùng!' });
          this.currentView.set('list');
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi phân quyền trực tiếp thất bại.' });
        }
      });
  }
}
