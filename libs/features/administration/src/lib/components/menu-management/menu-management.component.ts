// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\administration\menu-management.component.ts
import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';

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

  displayDialog = signal<boolean>(false);
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentMenu = signal<any>({});
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/menus`;

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

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadMenus();
    this.loadPermissions();
  }

  loadMenus() {
    this.loading.set(true);
    this.http.get<any>(this.apiUrl)
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
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/roles/permissions/all`).subscribe({
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
    this.isEdit.set(false);
    this.currentMenu.set({ name: '', url: '', icon: '', permission: null, parentId: null, orderNum: 0 });
    this.dialogHeader.set('Thêm mới Menu');
    this.displayDialog.set(true);
  }

  onEdit(menu: any) {
    this.isEdit.set(true);
    this.currentMenu.set({ ...menu });
    this.dialogHeader.set('Chỉnh sửa Menu');
    this.displayDialog.set(true);
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
      this.http.put(`${this.apiUrl}/${menuDraft.id}`, menuDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật menu thành công!' });
            this.loadMenus();
            this.displayDialog.set(false);
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật menu.' });
          }
        });
    } else {
      this.http.post(this.apiUrl, menuDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Thêm mới menu thành công!' });
            this.loadMenus();
            this.displayDialog.set(false);
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
        this.http.delete(`${this.apiUrl}/${menu.id}`).subscribe({
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
