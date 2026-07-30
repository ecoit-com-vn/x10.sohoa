import { Component, OnInit, signal, computed, inject } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { AuthService, MenuService } from '@sohoa.frontend/shared/core';
import {
  buildMenuDisplayTree,
  isMenuViewPermission,
  MenuDisplayTreeNode,
  normalizeMenuLookupList
} from '../../utils/menu-permission-tree.util';

@Component({
  selector: 'app-menu-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ToastModule,
    MenuModule,
    WfBreadcrumbComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './menu-management.component.html',
  styleUrl: './menu-management.component.scss'
})
export class MenuManagement implements OnInit {
  menus = signal<any[]>([]);
  searchKeyword = signal<string>('');
  permissions = signal<any[]>([]);
  expandedMenuIds = signal<Set<number>>(new Set<number>());

  currentView = signal<'list' | 'add' | 'edit'>('list');
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentMenu = signal<any>({});

  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  actionMenuItems: MenuItem[] = [];

  formSubmitted = signal<boolean>(false);
  serverErrors = signal<any>({});
  nameError = computed(() => {
    if (this.formSubmitted() && !this.currentMenu().name) return 'Tên menu là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  menuViewPermissions = computed(() => {
    const viewPermissions = this.permissions().filter((p) => isMenuViewPermission(p.code || p.Code));
    const currentCode = this.currentMenu()?.permissionCode;

    if (currentCode && !viewPermissions.some((p) => (p.code || p.Code) === currentCode)) {
      const currentPermission = this.permissions().find((p) => (p.code || p.Code) === currentCode);
      if (currentPermission) {
        return [currentPermission, ...viewPermissions];
      }
    }

    return viewPermissions;
  });

  menuTree = computed(() => buildMenuDisplayTree(this.menus(), this.searchKeyword()));

  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);

  // Quản lý trạng thái popup, menu được chọn và request xóa.
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<MenuDisplayTreeNode | null>(null);
  deleteLoading = signal<boolean>(false);

  // Chuẩn hóa tên menu hiển thị trong popup xác nhận xóa dùng chung.
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.name ?? '');

  private menuService = inject(MenuService);
  private messageService = inject(MessageService);
  public authService = inject(AuthService);

  ngOnInit() {
    this.loadMenus();
    this.loadPermissions();
  }

  openActionMenu(menuItem: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      ...(this.authService.hasPermission('MENU_CREATE') ? [{ label: 'Thêm menu con', title: 'Thêm menu con', icon: 'pi pi-plus', command: () => this.onAddNew(menuItem.id) }] : []),
      ...(this.authService.hasPermission('MENU_EDIT') ? [{ label: menuItem.isActive ? 'Khóa menu' : 'Mở khóa menu', title: menuItem.isActive ? 'Khóa menu' : 'Mở khóa menu' ,icon: menuItem.isActive ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal', command: () => this.onToggleStatusRequest(menuItem) }] : []),
      ...(this.authService.hasPermission('MENU_EDIT') ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa' ,icon: 'pi pi-pencil color-blue', command: () => this.onEdit(menuItem) }] : []),
      ...(this.authService.hasPermission('MENU_DELETE') ? [{ label: 'Xóa', title: 'Xóa' ,icon: 'pi pi-trash color-red', command: () => this.onDelete(menuItem) }] : []),
    ];
    menu.toggle(event);
  }

  loadMenus() {
    this.loading.set(true);
    this.menuService.getMenus()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const raw = Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : []));
          this.menus.set(normalizeMenuLookupList(raw));
          this.syncExpandedMenus();
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách menu.' });
        }
      });
  }

  loadPermissions() {
    this.menuService.getPermissions().subscribe({
      next: (res) => {
        this.permissions.set(Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : [])));
      },
      error: (err) => {
        console.error('Không thể load permissions', err);
      }
    });
  }

  syncExpandedMenus() {
    const kw = this.searchKeyword().trim();
    if (!kw) {
      return;
    }

    const ids = new Set<number>();
    const collect = (nodes: MenuDisplayTreeNode[]) => {
      nodes.forEach((node) => {
        if (node.children.length > 0) {
          ids.add(node.id);
        }
        collect(node.children);
      });
    };
    collect(this.menuTree());
    this.expandedMenuIds.set(ids);
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

  onSearch() {
    this.syncExpandedMenus();
  }

  toggleMenuGroup(menuId: number) {
    this.expandedMenuIds.update((prev) => {
      const next = new Set(prev);
      if (next.has(menuId)) {
        next.delete(menuId);
      } else {
        next.add(menuId);
      }
      return next;
    });
  }

  isMenuExpanded(menuId: number): boolean {
    return this.expandedMenuIds().has(menuId);
  }

  onParentRowClick(node: MenuDisplayTreeNode) {
    if (node.children.length > 0) {
      this.toggleMenuGroup(node.id);
    }
  }

  getEligibleParents(currentId: number | null): any[] {
    const allMenus = this.menus() || [];
    if (!currentId) {
      return [...allMenus].sort((a, b) => {
        const levelDiff = this.getIndentLevel(a) - this.getIndentLevel(b);
        if (levelDiff !== 0) {
          return levelDiff;
        }
        return (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
      });
    }

    const excludedIds = new Set<number>([currentId]);
    const collectDescendants = (parentId: number) => {
      allMenus.forEach((menu) => {
        if (menu.parentId === parentId && !excludedIds.has(menu.id)) {
          excludedIds.add(menu.id);
          collectDescendants(menu.id);
        }
      });
    };
    collectDescendants(currentId);

    return allMenus
      .filter((menu) => !excludedIds.has(menu.id))
      .sort((a, b) => {
        const levelDiff = this.getIndentLevel(a) - this.getIndentLevel(b);
        if (levelDiff !== 0) {
          return levelDiff;
        }
        return (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
      });
  }

  getParentOptionLabel(menu: any): string {
    const level = this.getIndentLevel(menu);
    return `${'— '.repeat(level)}${menu.name}`;
  }

  getIndentLevel(menu: any): number {
    let level = 0;
    let parentId = menu.parentId;
    while (parentId) {
      const parent = this.menus().find(m => m.id === parentId);
      if (parent && parent.id !== menu.id) {
        level++;
        parentId = parent.parentId;
      } else {
        break;
      }
    }
    return level;
  }

  onAddNew(parentId: number | null = null) {
    if (!this.authService.hasPermission('MENU_CREATE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới menu.' });
      return;
    }
    this.isEdit.set(false);
    this.currentMenu.set({ name: '', url: '', icon: '', permissionCode: null, parentId, sortOrder: 0, isActive: true });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set(parentId ? 'Thêm menu con' : 'Thêm menu gốc');
    this.currentView.set('add');
  }

  onToggleStatusRequest(menu: any) {
    this.lockUnlockTarget.set(menu);
    this.showLockUnlockConfirm.set(true);
  }

  onConfirmLockUnlock() {
    const menu = this.lockUnlockTarget();
    if (!menu) return;
    if (!this.authService.hasPermission('MENU_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa menu.' });
      return;
    }
    const updated = { ...menu, isActive: !menu.isActive };
    this.lockUnlockLoading.set(true);
    this.menuService.updateMenu(menu.id, updated)
      .pipe(finalize(() => this.lockUnlockLoading.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `${menu.isActive ? 'Khóa' : 'Mở khóa'} menu thành công!`
          });
          this.showLockUnlockConfirm.set(false);
          this.lockUnlockTarget.set(null);
          this.loadMenus();
        },
        error: (err: any) => {
          const detailMsg = err?.error?.message || err?.message || `Không thể ${menu.isActive ? 'khóa' : 'mở khóa'} menu.`;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
        }
      });
  }

  onCancelLockUnlock() {
    this.showLockUnlockConfirm.set(false);
    this.lockUnlockTarget.set(null);
  }

  getGlobalSTT(node: any): number {
    const flatList: any[] = [];
    const collect = (nodes: any[]) => {
      nodes.forEach((n) => {
        flatList.push(n);
        if (n.children && n.children.length > 0 && this.expandedMenuIds().has(n.id)) {
          collect(n.children);
        }
      });
    };
    collect(this.menuTree());
    const idx = flatList.findIndex(n => n.id === node.id);
    return idx >= 0 ? idx + 1 : 1;
  }

  onEdit(menu: any) {
    if (!this.authService.hasPermission('MENU_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa menu.' });
      return;
    }
    this.isEdit.set(true);
    this.currentMenu.set({ ...menu });
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.dialogHeader.set('Chỉnh sửa Menu');
    this.currentView.set('edit');
  }

  onSaveMenu() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});
    if (this.nameError()) {
      return;
    }

    const menuDraft = { ...this.currentMenu() };
    this.saving.set(true);

    if (menuDraft.parentId === 'null' || menuDraft.parentId === null || menuDraft.parentId === undefined) {
      menuDraft.parentId = null;
    } else {
      menuDraft.parentId = Number(menuDraft.parentId);
    }

    if (menuDraft.permissionCode === 'null' || menuDraft.permissionCode === '') {
      menuDraft.permissionCode = null;
    }

    if (this.isEdit()) {
      this.menuService.updateMenu(menuDraft.id, menuDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật menu thành công!' });
            this.loadMenus();
            this.currentView.set('list');
          },
          error: (err) => {
            this.serverErrors.set(this.extractErrors(err));
            const detailMsg = err?.error?.message || err?.message || 'Không thể cập nhật menu.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
    } else {
      this.menuService.createMenu(menuDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Thêm mới menu thành công!' });
            this.loadMenus();
            this.currentView.set('list');
          },
          error: (err) => {
            this.serverErrors.set(this.extractErrors(err));
            const detailMsg = err?.error?.message || err?.message || 'Không thể thêm mới menu.';
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
          }
        });
    }
  }

  onDelete(menu: MenuDisplayTreeNode): void {
    const hasChildren = this.menus().some(m => m.parentId === menu.id);
    if (hasChildren) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Không thể xóa Menu này vì có Menu con bên dưới!' });
      return;
    }

    // Lưu menu được chọn và mở popup xác nhận xóa.
    this.deleteTarget.set(menu);
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
    const menu = this.deleteTarget();

    // Chặn request trùng khi người dùng bấm nút Xóa nhiều lần.
    if (!menu || this.deleteLoading()) {
      return;
    }

    this.deleteLoading.set(true);

    this.menuService.deleteMenu(menu.id)
      .pipe(
        // Luôn tắt loading dù request thành công hay thất bại.
        finalize(() => this.deleteLoading.set(false))
      )
      .subscribe({
        next: () => {
          this.closeDeleteDialog();
          this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: 'Đã xóa Menu thành công!' });
          this.loadMenus();
        },
        error: (err: any) => {
          const detailMsg = err?.error?.message || err?.message || 'Xóa Menu thất bại.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
        }
      });
  }

  private closeDeleteDialog(): void {
    // Đóng popup và giải phóng bản ghi đang được chọn.
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  private extractErrors(err: any): Record<string, string> {
    if (err?.error) {
      if (typeof err.error === 'object') {
        return err.error.errors || err.error;
      }
      if (typeof err.error === 'string') {
        try {
          const parsed = JSON.parse(err.error);
          return parsed.errors || parsed;
        } catch {
          return {};
        }
      }
    }
    if (err?.errors) {
      return err.errors;
    }
    return {};
  }
}
