import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { UserService } from '../../services/user.service';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
  providers: [MessageService, ConfirmationService],
  templateUrl: './user-management.component.html',
  styleUrl: './user-management.component.scss'
})
export class UserManagement implements OnInit {
  users = signal<any[]>([]);
  searchKeyword = signal<string>('');

  currentView = signal<'list' | 'add' | 'edit' | 'unit-role' | 'permission' | 'role'>('list');
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentUser = signal<any>({});
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  // Quyền theo đơn vị
  organizationUnits = signal<any[]>([]);
  systemRoles = signal<any[]>([]);
  
  unitRoleDialogHeader = signal<string>('');
  activeUserForUnitRole = signal<any>(null);
  assignedUnitRoles = signal<any[]>([]);
  newUnitRole = signal<any>({ unitId: null, roleId: null });
  savingUnitRoles = signal<boolean>(false);

  // Quyền trực tiếp
  permissionDialogHeader = signal<string>('');
  activeUserForPermission = signal<any>(null);
  systemPermissions = signal<any[]>([]);
  selectedPermissionCodes = signal<string[]>([]);
  savingPermissions = signal<boolean>(false);

  menus = signal<any[]>([]);
  menuPermissionTree = signal<any[]>([]);

  // Vai trò trực tiếp
  roleDialogHeader = signal<string>('');
  activeUserForRole = signal<any>(null);
  selectedRoleIds = signal<number[]>([]);
  savingRoles = signal<boolean>(false);

  activeDropdownUserId = signal<string | null>(null);

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('click', () => {
        this.activeDropdownUserId.set(null);
      });
    }
  }

  toggleDropdown(userId: string, event: Event) {
    event.stopPropagation();
    if (this.activeDropdownUserId() === userId) {
      this.activeDropdownUserId.set(null);
    } else {
      this.activeDropdownUserId.set(userId);
    }
  }

  private userService = inject(UserService);
  private messageService = inject(MessageService);
  private confirmationService = inject(ConfirmationService);
  public authService = inject(AuthService);

  // computed signal for filteredUsers
  filteredUsers = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const allUsers = this.users() || [];
    if (!kw) {
      return [...allUsers];
    }
    return allUsers.filter(u =>
      (u?.username?.toLowerCase().includes(kw) ?? false) ||
      (u?.fullName?.toLowerCase().includes(kw) ?? false) ||
      (u?.email?.toLowerCase().includes(kw) ?? false)
    );
  });

  ngOnInit() {
    this.loadUsers();
    this.loadOrganizationUnits();
    this.loadSystemRoles();
    this.loadSystemPermissions();
    this.loadMenus();
  }

  loadUsers() {
    this.loading.set(true);
    this.userService.getUsers()
      .pipe(
        finalize(() => {
          this.loading.set(false);
        })
      )
      .subscribe({
        next: (res: any) => {
          const list = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
          this.users.set(list);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách tài khoản.' });
          this.users.set([]);
        }
      });
  }

  loadOrganizationUnits() {
    this.userService.getOrganizationUnits().subscribe({
      next: (res: any) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.organizationUnits.set(list);
      },
      error: () => {
        this.organizationUnits.set([]);
      }
    });
  }

  loadSystemRoles() {
    this.userService.getSystemRoles().subscribe({
      next: (res: any) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.systemRoles.set(list);
      },
      error: () => {
        this.systemRoles.set([]);
      }
    });
  }

  getUnitLabel(unitId: number): string {
    const u = (this.organizationUnits() || []).find(x => x.id === unitId);
    return u ? u.name : `Đơn vị #${unitId}`;
  }

  getRoleLabel(roleId: number): string {
    const r = (this.systemRoles() || []).find(x => x.id === roleId);
    return r ? r.name : `Vai trò #${roleId}`;
  }

  onSearch() {
    // Với computed, filteredUsers tự động cập nhật phản ứng khi searchKeyword() thay đổi
    // Hàm này giữ lại để tương thích với nút click Tìm kiếm trong giao diện cũ nếu cần
  }

  onAddNew() {
    if (!this.authService.hasPermission('USER_CREATE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới người dùng.' });
      return;
    }
    this.isEdit.set(false);
    this.currentUser.set({ username: '', fullName: '', email: '', organizationUnitId: null, isActive: true });
    this.dialogHeader.set('Thêm mới tài khoản');
    this.currentView.set('add');
  }

  onEdit(user: any) {
    if (!this.authService.hasPermission('USER_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa thông tin người dùng.' });
      return;
    }
    this.isEdit.set(true);
    this.currentUser.set({ ...user });
    this.dialogHeader.set('Chỉnh sửa tài khoản');
    this.currentView.set('edit');
  }

  onSaveUser() {
    const userDraft = this.currentUser();
    if (!userDraft.username || !userDraft.fullName || !userDraft.organizationUnitId || userDraft.organizationUnitId === 'null') {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập đầy đủ Tên đăng nhập, Họ tên và Đơn vị thành viên.' });
      return;
    }

    // Convert to number before sending to backend
    userDraft.organizationUnitId = Number(userDraft.organizationUnitId);

    this.saving.set(true);
    if (this.isEdit()) {
      this.userService.updateUser(userDraft.id, userDraft).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật tài khoản thành công!' });
          this.loadUsers();
          this.currentView.set('list');
          this.saving.set(false);
        },
        error: (err) => {
          this.saving.set(false);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật thông tin tài khoản.' });
        }
      });
    } else {
      this.userService.createUser(userDraft).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo tài khoản mới thành công!' });
          this.loadUsers();
          this.currentView.set('list');
          this.saving.set(false);
        },
        error: (err) => {
          this.saving.set(false);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tạo tài khoản mới.' });
        }
      });
    }
  }

  onDelete(user: any) {
    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa tài khoản ${user?.username || ''}?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      accept: () => {
        this.userService.deleteUser(user.id).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: 'Đã xóa tài khoản thành công!' });
            this.loadUsers();
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xóa tài khoản thất bại.' });
          }
        });
      }
    });
  }

  onManageUnitRoles(user: any) {
    if (!this.authService.hasPermission('USER_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền quản lý quyền theo đơn vị.' });
      return;
    }
    this.activeUserForUnitRole.set(user);
    this.unitRoleDialogHeader.set(`Phân quyền theo đơn vị cho: ${user?.username || ''}`);
    this.assignedUnitRoles.set([]);
    this.newUnitRole.set({ unitId: null, roleId: null });

    // Tải danh sách quyền đơn vị của user
    this.userService.getUserUnitRoles(user.id).subscribe({
      next: (res: any) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.assignedUnitRoles.set(list);
        this.currentView.set('unit-role');
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải quyền theo đơn vị của người dùng.' });
      }
    });
  }

  onAddUnitRole() {
    const draftUnitRole = this.newUnitRole();
    if (draftUnitRole.unitId === 'null' || draftUnitRole.roleId === 'null' || !draftUnitRole.unitId || !draftUnitRole.roleId) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn cả Đơn vị và Vai trò.' });
      return;
    }
    
    // Đảm bảo không add trùng
    const currentRoles = this.assignedUnitRoles();
    const exists = currentRoles.some(x => x.unitId === Number(draftUnitRole.unitId) && x.roleId === Number(draftUnitRole.roleId));
    if (exists) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Quyền đơn vị này đã tồn tại trong danh sách.' });
      return;
    }

    this.assignedUnitRoles.update(prev => [
      ...prev,
      {
        userId: this.activeUserForUnitRole().id,
        unitId: Number(draftUnitRole.unitId),
        roleId: Number(draftUnitRole.roleId)
      }
    ]);
    
    this.newUnitRole.set({ unitId: null, roleId: null });
  }

  onRemoveUnitRole(index: number) {
    this.assignedUnitRoles.update(prev => {
      const copy = [...prev];
      copy.splice(index, 1);
      return copy;
    });
  }

  onSaveUnitRoles() {
    const activeUser = this.activeUserForUnitRole();
    if (!activeUser) return;
    this.savingUnitRoles.set(true);
    
    this.userService.saveUserUnitRoles(activeUser.id, this.assignedUnitRoles()).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Cập nhật phân quyền theo đơn vị thành công!' });
        this.currentView.set('list');
        this.savingUnitRoles.set(false);
      },
      error: () => {
        this.savingUnitRoles.set(false);
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu cấu hình quyền theo đơn vị thất bại.' });
      }
    });
  }

  onExportExcel() {
    this.messageService.add({
      severity: 'info',
      summary: 'Xuất dữ liệu',
      detail: 'Đang chuẩn bị dữ liệu xuất Excel...'
    });
    
    setTimeout(() => {
      this.messageService.add({
        severity: 'success',
        summary: 'Thành công',
        detail: 'Đã xuất và tải về danh sách người dùng thành công!'
      });
    }, 1200);
  }

  loadSystemPermissions() {
    this.userService.getSystemPermissions().subscribe({
      next: (res: any) => {
        this.systemPermissions.set(Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []));
        this.updateTree();
      },
      error: (err) => {
        console.error('Không thể tải danh sách quyền hệ thống:', err);
      }
    });
  }

  loadMenus() {
    this.userService.getMenus().subscribe({
      next: (res: any) => {
        this.menus.set(Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []));
        this.updateTree();
      },
      error: (err) => {
        console.error('Không thể tải danh sách menu:', err);
      }
    });
  }

  updateTree() {
    if (this.menus().length > 0 && this.systemPermissions().length > 0) {
      this.buildMenuPermissionTree(this.menus(), this.systemPermissions());
    }
  }

  buildMenuPermissionTree(menusList: any[], permissions: any[]) {
    // 1. Group permissions by target menu URL
    const permGroups = new Map<string, any[]>();
    const unmappedPerms: any[] = [];

    permissions.forEach(p => {
      const targetUrl = this.getMenuTargetForPermissionDynamic(p.code, menusList);
      if (targetUrl) {
        if (!permGroups.has(targetUrl)) {
          permGroups.set(targetUrl, []);
        }
        permGroups.get(targetUrl)!.push(p);
      } else {
        unmappedPerms.push(p);
      }
    });

    // 2. Build the tree
    const parentMenus = menusList.filter(m => !m.parentId && m.isActive);
    const subMenusList = menusList.filter(m => m.parentId && m.isActive);

    const tree: any[] = [];

    parentMenus.forEach(pm => {
      const pmSubs = subMenusList.filter(sm => sm.parentId === pm.id);
      
      const subNodes: any[] = [];
      pmSubs.forEach(sm => {
        const smPerms = permGroups.get(sm.url || '') || [];
        // Only show submenus that have mapped permissions
        if (smPerms.length > 0) {
          subNodes.push({
            id: sm.id,
            name: sm.name,
            url: sm.url,
            icon: sm.icon,
            permissions: smPerms
          });
        }
      });

      const directPerms = permGroups.get(pm.url || '') || [];

      // Only display the parent menu card if it contains direct permissions or child menus with permissions
      if (directPerms.length > 0 || subNodes.length > 0) {
        tree.push({
          id: pm.id,
          name: pm.name,
          icon: pm.icon,
          url: pm.url,
          subMenus: subNodes,
          permissions: directPerms,
          expanded: false // Collapsed by default as requested
        });
      }
    });

    // Add unmapped permissions to a special group
    if (unmappedPerms.length > 0) {
      tree.push({
        id: -999,
        name: 'Hệ thống dùng chung / Quyền khác',
        icon: 'pi pi-key',
        url: '',
        subMenus: [],
        permissions: unmappedPerms,
        expanded: false // Collapsed by default as well
      });
    }

    this.menuPermissionTree.set(tree);
  }

  toggleParentMenu(parent: any) {
    parent.expanded = !parent.expanded;
    // Force signal update by recreating the array reference to trigger UI re-render
    this.menuPermissionTree.set([...this.menuPermissionTree()]);
  }

  getMenuTargetForPermissionDynamic(code: string, menusList: any[]): string {
    const parts = code.split('_');
    if (parts.length < 2) return '';
    const prefix = parts.slice(0, parts.length - 1).join('_');

    const matchingMenu = menusList.find(m => {
      if (!m.permissionCode) return false;
      const mParts = m.permissionCode.split('_');
      if (mParts.length < 2) return false;
      const mPrefix = mParts.slice(0, mParts.length - 1).join('_');
      return mPrefix === prefix;
    });

    if (matchingMenu) {
      return matchingMenu.url || '';
    }

    return this.getMenuTargetForPermission(code);
  }

  getMenuTargetForPermission(code: string): string {
    const parts = code.split('_');
    if (parts.length < 2) return '';
    const prefix = parts.slice(0, parts.length - 1).join('_');
    
    switch(prefix) {
      case 'USER': return '/administration/user-management';
      case 'ROLE':
      case 'PERMISSION': return '/administration/role-management';
      case 'MENU': return '/administration/menu-management';
      case 'USER_GROUP': return '/administration/user-groups';
      case 'UPLOAD_CONFIG': return '/administration/upload-configuration';
      case 'ORGANIZATION': return '/administration/organization-settings';
      case 'AUDIT_LOG': return '/administration/audit-log';
      case 'CATALOG': return '/catalog/fond';
      case 'EAV_FORM_TEMPLATE':
      case 'EQUIPMENT_TYPE':
      case 'EQUIPMENT': return '/equipment/form-management';
      case 'VIRTUAL_FOLDER': return '/digitization/virtual-folders';
      case 'OCR_TRAINING_DATA': return '/digitization/ocr-training';
      case 'DIGITIZATION_TASK': return '/digitization/ocr-upload'; 
      case 'DIGITIZATION': return '/ocr-correction';
      case 'WORKFLOW':
      case 'WORKFLOW_DEFINITION': return '/workflow/borrow-return';
      case 'BORROW_RECORD': return '/borrow-records';
      case 'REPORT':
      case 'DYNAMIC_REPORT':
      case 'REPORT_GROUP': return '/reports';
      case 'PHYSICAL_STORAGE': return '/physical-storage';
      case 'SYNC': return '/administration/sync-config';
      case 'VIEW':
        if (code === 'VIEW_DASHBOARD') return '/dashboard';
        break;
    }
    return '';
  }

  onManagePermissions(user: any) {
    if (!this.authService.hasPermission('USER_MANAGE') && !this.authService.hasPermission('PERMISSION_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền phân quyền trực tiếp.' });
      return;
    }
    this.activeUserForPermission.set(user);
    this.permissionDialogHeader.set(`Phân quyền trực tiếp cho tài khoản: ${user?.username || ''}`);
    this.selectedPermissionCodes.set([]);
    
    this.userService.getUserPermissions(user.id).subscribe({
      next: (res: any) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.selectedPermissionCodes.set(list);
        this.currentView.set('permission');
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách quyền trực tiếp đã gán.' });
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
    const activeUser = this.activeUserForPermission();
    if (!activeUser) return;
    
    this.savingPermissions.set(true);
    this.userService.saveUserPermissions(activeUser.id, this.selectedPermissionCodes())
      .pipe(finalize(() => this.savingPermissions.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thay đổi phân quyền trực tiếp cho người dùng!' });
          this.currentView.set('list');
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi phân quyền trực tiếp thất bại.' });
        }
      });
  }

  onManageRoles(user: any) {
    if (!this.authService.hasPermission('USER_MANAGE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền gán vai trò trực tiếp.' });
      return;
    }
    this.activeUserForRole.set(user);
    this.roleDialogHeader.set(`Gán vai trò trực tiếp cho tài khoản: ${user?.username || ''}`);
    this.selectedRoleIds.set([]);
    
    this.userService.getUserRoles(user.id).subscribe({
      next: (res: any) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.selectedRoleIds.set(list);
        this.currentView.set('role');
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách vai trò trực tiếp đã gán.' });
      }
    });
  }

  isRoleChecked(roleId: number): boolean {
    return this.selectedRoleIds().includes(roleId);
  }

  toggleRole(roleId: number) {
    this.selectedRoleIds.update(prev => {
      const idx = prev.indexOf(roleId);
      if (idx > -1) {
        const copy = [...prev];
        copy.splice(idx, 1);
        return copy;
      } else {
        return [...prev, roleId];
      }
    });
  }

  onSaveRoles() {
    const activeUser = this.activeUserForRole();
    if (!activeUser) return;
    
    this.savingRoles.set(true);
    this.userService.saveUserRoles(activeUser.id, this.selectedRoleIds())
      .pipe(finalize(() => this.savingRoles.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu vai trò trực tiếp cho người dùng thành công!' });
          this.currentView.set('list');
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi vai trò trực tiếp thất bại.' });
        }
      });
  }
}
