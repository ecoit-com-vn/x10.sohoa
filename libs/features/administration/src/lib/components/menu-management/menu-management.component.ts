import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { finalize } from 'rxjs';
import { AuthService, MenuService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-menu-management',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
  providers: [MessageService],
  templateUrl: './menu-management.component.html',
  styleUrl: './menu-management.component.scss'
})
export class MenuManagement implements OnInit {
  menus = signal<any[]>([]);
  searchKeyword = signal<string>('');
  permissions = signal<any[]>([]);

  currentView = signal<'list' | 'add' | 'edit'>('list');
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentMenu = signal<any>({});
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  private menuService = inject(MenuService);
  private messageService = inject(MessageService);
  private confirmationService = inject(ConfirmationService);
  public authService = inject(AuthService);

  // Computed signal for filteredMenus
  filteredMenus = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const allMenus = this.menus() || [];
    if (!kw) {
      return this.buildHierarchicalList();
    }
    return allMenus.filter(m => 
      (m.name?.toLowerCase().includes(kw) ?? false) || 
      (m.url?.toLowerCase().includes(kw) ?? false) ||
      (m.permission?.toLowerCase().includes(kw) ?? false)
    );
  });

  ngOnInit() {
    this.loadMenus();
    this.loadPermissions();
  }

  loadMenus() {
    this.loading.set(true);
    this.menuService.getMenus()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          this.menus.set(Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []));
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách menu.' });
        }
      });
  }

  loadPermissions() {
    this.menuService.getPermissions().subscribe({
      next: (res) => {
        this.permissions.set(Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []));
      },
      error: (err) => {
        console.error('Không thể load permissions', err);
      }
    });
  }

  onSearch() {
    // Tự động thông qua computed
  }

  buildHierarchicalList(): any[] {
    const result: any[] = [];
    const menusSafe = this.menus() || [];
    const rootNodes = menusSafe.filter(m => !m.parentId);
    // Sắp xếp theo orderNum
    rootNodes.sort((a, b) => a.orderNum - b.orderNum);
    
    const visit = (node: any) => {
      result.push(node);
      const children = menusSafe.filter(m => m.parentId === node.id);
      children.sort((a, b) => a.orderNum - b.orderNum);
      children.forEach(visit);
    };

    rootNodes.forEach(visit);

    menusSafe.forEach(m => {
      if (!result.includes(m)) {
        result.push(m);
      }
    });

    return result;
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

  getEligibleParents(currentId: number | null): any[] {
    const allMenus = this.menus() || [];
    if (!currentId) return allMenus.filter(m => !m.parentId);
    return allMenus.filter(m => m.id !== currentId && !m.parentId);
  }

  onAddNew() {
    if (!this.authService.hasPermission('MENU_CREATE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới menu.' });
      return;
    }
    this.isEdit.set(false);
    this.currentMenu.set({ name: '', url: '', icon: '', permission: null, parentId: null, orderNum: 0 });
    this.dialogHeader.set('Thêm mới Menu');
    this.currentView.set('add');
  }

  onEdit(menu: any) {
    if (!this.authService.hasPermission('MENU_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa menu.' });
      return;
    }
    this.isEdit.set(true);
    this.currentMenu.set({ ...menu });
    this.dialogHeader.set('Chỉnh sửa Menu');
    this.currentView.set('edit');
  }

  onSaveMenu() {
    const menuDraft = this.currentMenu();
    if (!menuDraft.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Tên Menu là bắt buộc.' });
      return;
    }

    this.saving.set(true);
    // Đảm bảo parentId là null hoặc số
    if (menuDraft.parentId === 'null' || menuDraft.parentId === null) {
      menuDraft.parentId = null;
    } else {
      menuDraft.parentId = Number(menuDraft.parentId);
    }

    if (menuDraft.permission === 'null' || menuDraft.permission === '') {
      menuDraft.permission = null;
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
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật menu.' });
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
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể thêm mới menu.' });
          }
        });
    }
  }

  onDelete(menu: any) {
    const hasChildren = this.menus().some(m => m.parentId === menu.id);
    if (hasChildren) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Không thể xóa Menu này vì có Menu con bên dưới!' });
      return;
    }

    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa Menu ${menu.name}?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      accept: () => {
        this.menuService.deleteMenu(menu.id).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: 'Đã xóa Menu thành công!' });
            this.loadMenus();
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xóa Menu thất bại.' });
          }
        });
      }
    });
  }
}
