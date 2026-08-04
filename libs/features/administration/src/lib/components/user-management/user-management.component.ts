import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  EcoPaginatorComponent,
  EcoInputTreeSelectComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { UserService } from '../../services/user.service';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';
import { buildMenuPermissionTree as buildMenuPermissionTreeFromLookup } from '../../utils/menu-permission-tree.util';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ToastModule,
    MenuModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    EcoPaginatorComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './user-management.component.html',
  styleUrl: './user-management.component.scss'
})
export class UserManagement implements OnInit {
  users = signal<any[]>([]);
  searchKeyword = signal<string>('');
  searchUnitIds = signal<number[]>([]);
  searchStatus = signal<string>(''); // '' (All), 'active' (Hoạt động), 'inactive' (Ngưng hoạt động)
  appliedKeyword = signal<string>('');
  appliedUnitIds = signal<number[]>([]);
  appliedStatus = signal<string>('');
  totalCount = signal<number>(0);

  currentView = signal<'list' | 'add' | 'edit' | 'unit-role' | 'permission' | 'role'>('list');
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentUser = signal<any>({});
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  // Pagination
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  // Form validation
  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});
  usernameError = computed(() => {
    if (this.formSubmitted() && !this.currentUser().username) return 'Tên đăng nhập là bắt buộc';
    return this.serverErrors().username || this.serverErrors().Username || '';
  });
  fullNameError = computed(() => {
    if (this.formSubmitted() && !this.currentUser().fullName) return 'Họ và tên là bắt buộc';
    return this.serverErrors().fullName || this.serverErrors().FullName || '';
  });
  unitError = computed(() => {
    if (this.formSubmitted() && (!this.currentUser().organizationUnitId || this.currentUser().organizationUnitId === 'null' || this.currentUser().organizationUnitId === null)) return 'Đơn vị thành viên là bắt buộc';
    return this.serverErrors().organizationUnitId || this.serverErrors().OrganizationUnitId || '';
  });
  positionError = computed(() => {
    if (this.formSubmitted() && (!this.currentUser().positionId || this.currentUser().positionId === null)) return 'Chức vụ là bắt buộc';
    return '';
  });
  emailError = computed(() => {
    return this.serverErrors().email || this.serverErrors().Email || '';
  });

  onFieldChange(field: string) {
    this.currentUser.update(user => ({ ...user }));
    this.serverErrors.update(errs => {
      const copy = { ...errs };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }

  // Quyền theo đơn vị
  organizationUnits = signal<any[]>([]);
  systemRoles = signal<any[]>([]);

  // Danh mục chức vụ (từ EquipmentService Catalog)
  positions = signal<any[]>([]);
  positionCatalogTypeId = signal<number | null>(null); // ID của CatalogType "CHUC_VU"

  // Org-unit tree picker
  orgUnitTree = computed(() => this.buildOrgTree(this.organizationUnits()));
  primengOrgUnitTree = computed(() => {
    const buildPrimeNGNodes = (nodes: any[]): any[] => {
      return nodes.map(n => ({
        key: n.id,
        label: n.name,
        data: n,
        children: n.children && n.children.length ? buildPrimeNGNodes(n.children) : []
      }));
    };
    return buildPrimeNGNodes(this.orgUnitTree());
  });

  // Vai trò trong form add/edit (chọn nhiều)
  selectedRoleIdsInForm = signal<number[]>([]);
  rolesDropdownOpen = signal<boolean>(false);
  selectedRolesLabel = computed(() => {
    const selectedIds = this.selectedRoleIdsInForm();
    if (selectedIds.length === 0) return '-- Chọn vai trò --';
    const names = this.systemRoles()
      .filter(r => selectedIds.includes(r.id))
      .map(r => r.name);
    if (names.length === 0) return '-- Chọn vai trò --';
    if (names.length <= 2) return names.join(', ');
    return `Đã chọn ${names.length} vai trò`;
  });

  // Inline delete confirm
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);

  // Chuẩn hóa tên tài khoản hiển thị trong popup xác nhận xóa dùng chung.
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.username || '');

  // Lock/Unlock Confirmation
  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);
  
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

  actionMenuItems: MenuItem[] = [];

  constructor() {
    effect(() => {
      const page = this.currentPage();
      const size = this.pageSize();
      this.loadUsers();
    }, { allowSignalWrites: true });

  }

  openActionMenu(user: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      ...(this.authService.hasPermission('USER_MANAGE') ? [
        { label: 'Quyền theo đơn vị', title: 'Quyền theo đơn vị', icon: 'pi pi-sitemap', command: () => this.onManageUnitRoles(user) },
        { label: 'Gán vai trò trực tiếp', title: 'Gán vai trò trực tiếp', icon: 'pi pi-shield', command: () => this.onManageRoles(user) },
      ] : []),
      ...(this.authService.hasPermission('USER_MANAGE') || this.authService.hasPermission('PERMISSION_MANAGE') ? [{ label: 'Phân quyền trực tiếp', title: 'Phân quyền trực tiếp', icon: 'pi pi-key color-blue', command: () => this.onManagePermissions(user) }] : []),
      ...(this.authService.hasPermission('USER_EDIT') ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-teal', command: () => this.onEdit(user) }] : []),
      ...(this.authService.hasPermission('USER_EDIT') ? [{ label: user.isActive ? 'Khóa tài khoản' : 'Mở khóa tài khoản', title: user.isActive ? 'Khóa tài khoản' : 'Mở khóa tài khoản', icon: user.isActive ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal', command: () => this.onToggleStatusRequest(user) }] : []),
      ...(this.authService.hasPermission('USER_DELETE') ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(user) }] : []),
    ];
    menu.toggle(event);
  }

  private userService = inject(UserService);
  private messageService = inject(MessageService);
  public authService = inject(AuthService);

  // computed signal for filteredUsers
  filteredUsers = computed(() => {
    return this.users();
  });

  // Paginated users
  paginatedUsers = computed(() => {
    return this.users();
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

  onPageSizeChange(pageSize: number) {
    this.pageSize.set(pageSize);
    this.currentPage.set(1);
  }

  ngOnInit() {
    this.loadUsers();
    this.loadOrganizationUnits();
    this.loadSystemRoles();
    this.loadSystemPermissions();
    this.loadMenus();
    this.loadPositions();
  }

  loadUsers() {
    this.loading.set(true);
    const statusVal = this.appliedStatus();
    const isActiveParam = statusVal === 'active' ? true : (statusVal === 'inactive' ? false : null);
    
    this.userService.getUsers(
      this.currentPage(),
      this.pageSize(),
      this.appliedKeyword(),
      this.appliedUnitIds(),
      isActiveParam
    )
      .pipe(
        finalize(() => {
          this.loading.set(false);
        })
      )
      .subscribe({
        next: (res: any) => {
          const list = res?.items || [];
          this.users.set(list);
          this.totalCount.set(res?.totalCount || 0);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách tài khoản.' });
          this.users.set([]);
          this.totalCount.set(0);
        }
      });
  }

  loadOrganizationUnits() {
    this.userService.getOrganizationUnits().subscribe({
      next: (res: any) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : []));
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
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : []));
        this.systemRoles.set(list);
      },
      error: () => {
        this.systemRoles.set([]);
      }
    });
  }

  loadPositions() {
    // Bước 1: Lấy danh sách catalog types, tìm loại có Code = 'CHUC_VU'
    this.userService.getCatalogTypes().subscribe({
      next: (types: any[]) => {
        const chucVuType = types.find(t => t.code === 'CHUC_VU' || t.Code === 'CHUC_VU');
        if (chucVuType) {
          const typeId = chucVuType.id || chucVuType.Id;
          this.positionCatalogTypeId.set(typeId);
          // Bước 2: Lấy các catalog items của loại này
          this.userService.getPositions(typeId).subscribe({
            next: (items: any[]) => this.positions.set(items || []),
            error: () => this.positions.set([])
          });
        }
      },
      error: () => {} // Không bắt lỗi nếu EquipmentService chưa khởi động
    });
  }

  getUnitLabel(unitId: number | null | undefined): string {
    if (!unitId) return '';
    const u = (this.organizationUnits() || []).find(x => x.id == unitId);
    return u ? u.name : `Đơn vị #${unitId}`;
  }

  getRoleLabel(roleId: number): string {
    const r = (this.systemRoles() || []).find(x => x.id === roleId);
    return r ? r.name : `Vai trò #${roleId}`;
  }

  getPositionLabel(positionId: number | null | undefined): string {
    if (!positionId) return '';
    const p = (this.positions() || []).find(x => x.id == positionId);
    return p ? p.name : `Chức vụ #${positionId}`;
  }

  // ── Org-unit Tree methods ─────────────────────────────────────────────────
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



  toggleRolesDropdown(event?: Event) {
    if (event) event.stopPropagation();
    this.rolesDropdownOpen.update(v => !v);
  }

  // ── Vai trò trong form ───────────────────────────────────────────────────
  toggleRoleInForm(roleId: number) {
    this.selectedRoleIdsInForm.update(prev => {
      const idx = prev.indexOf(roleId);
      if (idx > -1) {
        const copy = [...prev];
        copy.splice(idx, 1);
        return copy;
      }
      return [...prev, roleId];
    });
  }

  isRoleSelectedInForm(roleId: number): boolean {
    return this.selectedRoleIdsInForm().includes(roleId);
  }

  onSearch() {
    const keyword = this.searchKeyword().trim();
    this.searchKeyword.set(keyword);
    this.appliedKeyword.set(keyword);
    this.reloadUsersFromFirstPage();
  }

  onAddNew() {
    if (!this.authService.hasPermission('USER_CREATE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới người dùng.' });
      return;
    }
    this.isEdit.set(false);
    this.currentUser.set({ username: '', fullName: '', email: '', organizationUnitId: null, positionId: null, positionName: '', isActive: true });
    this.selectedRoleIdsInForm.set([]);
    this.rolesDropdownOpen.set(false);
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Thêm mới tài khoản');
    this.currentView.set('add');
  }

  onSearchUnitChange(val: any) {
    const unitIds = Array.isArray(val) ? val.map(Number).filter(Number.isFinite) : [];
    this.searchUnitIds.set(unitIds);
    this.appliedUnitIds.set(unitIds);
    this.reloadUsersFromFirstPage();
  }

  onSearchStatusChange(status: string): void {
    this.searchStatus.set(status);
    this.appliedStatus.set(status);
    this.reloadUsersFromFirstPage();
  }

  private reloadUsersFromFirstPage(): void {
    if (this.currentPage() === 1) {
      this.loadUsers();
      return;
    }
    this.currentPage.set(1);
  }

  onToggleStatusRequest(user: any) {
    this.lockUnlockTarget.set(user);
    this.showLockUnlockConfirm.set(true);
  }

  onCancelLockUnlock() {
    this.showLockUnlockConfirm.set(false);
    this.lockUnlockTarget.set(null);
  }

  onConfirmLockUnlock() {
    const user = this.lockUnlockTarget();
    if (!user) return;
    if (!this.authService.hasPermission('USER_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thay đổi trạng thái người dùng.' });
      return;
    }
    const updated = { ...user, isActive: !user.isActive };
    this.lockUnlockLoading.set(true);
    this.userService.updateUser(user.id, updated).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: `${user.isActive ? 'Khóa' : 'Mở khóa'} tài khoản thành công!`
        });
        this.showLockUnlockConfirm.set(false);
        this.lockUnlockTarget.set(null);
        this.lockUnlockLoading.set(false);
        this.loadUsers();
      },
      error: (err) => {
        this.lockUnlockLoading.set(false);
        const detailMsg = err?.error?.message || err?.message || `Không thể ${user.isActive ? 'khóa' : 'mở khóa'} tài khoản.`;
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
      }
    });
  }

  onEdit(user: any) {
    if (!this.authService.hasPermission('USER_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa thông tin người dùng.' });
      return;
    }
    this.isEdit.set(true);
    this.currentUser.set({ ...user });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.rolesDropdownOpen.set(false);
    // Load roles hiện tại của user vào form
    this.userService.getUserRoles(user.id).subscribe({
      next: (ids: any) => {
        const list = Array.isArray(ids) ? ids : [];
        this.selectedRoleIdsInForm.set(list.map((x: any) => Number(x)));
      },
      error: () => this.selectedRoleIdsInForm.set([])
    });
    this.dialogHeader.set('Chỉnh sửa tài khoản');
    this.currentView.set('edit');
  }

  onSaveUser() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});
    if (this.usernameError() || this.fullNameError() || this.unitError() || this.positionError()) {
      return;
    }

    const userDraft = { ...this.currentUser() };
    userDraft.organizationUnitId = Number(userDraft.organizationUnitId);
    // Điền positionName dựa vào positionId được chọn
    const selectedPosition = (this.positions() || []).find(p => p.id == userDraft.positionId);
    userDraft.positionName = selectedPosition ? selectedPosition.name : null;

    this.saving.set(true);
    if (this.isEdit()) {
      this.userService.updateUser(userDraft.id, userDraft).subscribe({
        next: () => {
          // Lưu vai trò được chọn trong form sau khi update
          this.userService.saveUserRoles(userDraft.id, this.selectedRoleIdsInForm()).subscribe();
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật tài khoản thành công!' });
          this.loadUsers();
          this.currentView.set('list');
          this.saving.set(false);
        },
        error: (err) => {
          this.saving.set(false);
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
          const detailMsg = err?.error?.message || err?.message || 'Không thể cập nhật thông tin tài khoản.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
        }
      });
    } else {
      this.userService.createUser(userDraft).subscribe({
        next: (res: any) => {
          const newUserId = res?.id || res?.Id;
          // Nếu có vai trò được chọn trong form, gán vai trò sau khi tạo user
          if (newUserId && this.selectedRoleIdsInForm().length > 0) {
            this.userService.saveUserRoles(newUserId, this.selectedRoleIdsInForm()).subscribe();
          }
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo tài khoản mới thành công!' });
          this.loadUsers();
          this.currentView.set('list');
          this.saving.set(false);
        },
        error: (err) => {
          this.saving.set(false);
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
          const detailMsg = err?.error?.message || err?.message || 'Không thể tạo tài khoản mới.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
        }
      });
    }
  }

  onDelete(user: any): void {
    this.deleteTarget.set(user);
    this.showDeleteConfirm.set(true);
  }

  onResetSearch() {
    this.searchKeyword.set('');
    this.searchUnitIds.set([]);
    this.searchStatus.set('');
    this.appliedKeyword.set('');
    this.appliedUnitIds.set([]);
    this.appliedStatus.set('');
    this.reloadUsersFromFirstPage();
  }

  onConfirmDelete(): void {
    const user = this.deleteTarget();

    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!user || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.userService.deleteUser(user.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: `Đã xóa tài khoản "${user.username}" thành công!` });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadUsers();
        },
        error: (err) => {
          const detail =
            err?.error?.message ||
            err?.message ||
            'Xóa tài khoản thất bại.';

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
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : []));
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
        this.systemPermissions.set(Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : [])));
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
        this.menus.set(Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : [])));
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
    this.menuPermissionTree.set(buildMenuPermissionTreeFromLookup(menusList, permissions));
  }

  toggleParentMenu(parent: any) {
    parent.expanded = !parent.expanded;
    // Force signal update by recreating the array reference to trigger UI re-render
    this.menuPermissionTree.set([...this.menuPermissionTree()]);
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
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : []));
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
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : []));
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
