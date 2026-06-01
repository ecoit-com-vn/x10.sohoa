// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\administration\menu-management.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-menu-management',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
  providers: [MessageService],
  template: `
    <div class="wf-page">
      <p-toast></p-toast>
      <div class="wf-card">
        <!-- Breadcrumb -->
        <div class="breadcrumb">
          <i class="pi pi-home bc-icon"></i>
          <span class="bc-text">Trang chủ</span>
          <span class="bc-sep">/</span>
          <span class="bc-text">Quản trị hệ thống</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Quản lý Menu</span>
        </div>

        <p class="text-muted mb-4">
          Thiết lập sơ đồ Menu chức năng hiển thị tại Sidebar của người dùng theo quyền hạn cụ thể.
        </p>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm nhanh menu..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right">
            <button class="btn-green" (click)="onAddNew()">
              <i class="pi pi-plus"></i> Thêm menu mới
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 80px;">STT</th>
                <th>Tên hiển thị (Name)</th>
                <th>Đường dẫn (URL)</th>
                <th>Biểu tượng (Icon)</th>
                <th>Quyền yêu cầu (Permission)</th>
                <th style="width: 100px;">Thứ tự</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <!-- skeleton loading rows -->
              <ng-container *ngIf="loading">
                <tr *ngFor="let item of [1, 2, 3, 4]">
                  <td class="col-stt"><div class="skeleton-shimmer" style="height: 16px; width: 24px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 150px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 120px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 60px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 100px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 40px; border-radius: 4px;"></div></td>
                  <td class="col-hd"><div class="skeleton-shimmer" style="height: 24px; width: 60px; border-radius: 4px;"></div></td>
                </tr>
              </ng-container>

              <ng-container *ngIf="!loading">
                <tr *ngFor="let m of filteredMenus; let i = index">
                  <td class="col-stt text-muted">{{ i + 1 }}</td>
                  <td>
                    <div [style.padding-left.px]="getIndentLevel(m) * 20" style="display: flex; align-items: center; gap: 8px;">
                      <i [class]="m.icon ? m.icon : 'pi pi-folder'" style="color: #002D72;"></i>
                      <b class="wf-name-link" (click)="onEdit(m)">{{ m.name }}</b>
                    </div>
                  </td>
                  <td><code>{{ m.url || '-' }}</code></td>
                  <td>
                    <span *ngIf="m.icon"><i [class]="m.icon"></i> <code>{{ m.icon }}</code></span>
                    <span *ngIf="!m.icon" class="text-muted">-</span>
                  </td>
                  <td>
                    <span class="status-pill status-active" *ngIf="m.permission" style="background-color: #eff6ff; color: #1e40af; border-color: #bfdbfe;">
                      {{ m.permission }}
                    </span>
                    <span class="text-muted" *ngIf="!m.permission">Không yêu cầu</span>
                  </td>
                  <td>{{ m.orderNum }}</td>
                  <td class="col-hd">
                    <button class="act-btn act-edit" (click)="onEdit(m)" title="Chỉnh sửa"><i class="pi pi-pencil"></i></button>
                    <button class="act-btn act-delete" (click)="onDelete(m)" title="Xóa"><i class="pi pi-trash"></i></button>
                  </td>
                </tr>
                <tr *ngIf="filteredMenus.length === 0">
                  <td colspan="7" class="empty-row">
                    <i class="pi pi-inbox"></i>
                    <div>Không tìm thấy menu nào phù hợp.</div>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ menus.length }}</b> menu.</span>
        </div>
      </div>
    </div>

    <!-- Dialog Thêm/Sửa Menu -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label">Tên menu <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentMenu.name" placeholder="Ví dụ: Quản lý thiết bị..." />
        </div>
        
        <div class="form-group">
          <label class="form-label">Đường dẫn (URL)</label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentMenu.url" placeholder="Ví dụ: /search, /catalog/units..." />
        </div>

        <div class="form-group">
          <label class="form-label">Biểu tượng (Icon class PrimeNG)</label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentMenu.icon" placeholder="Ví dụ: pi pi-cog, pi pi-user..." />
        </div>

        <div class="form-group">
          <label class="form-label">Quyền hạn yêu cầu</label>
          <select class="wf-select w-full" [(ngModel)]="currentMenu.permission">
            <option [value]="null">-- Không yêu cầu (Cho phép tất cả) --</option>
            <option *ngFor="let p of permissions" [value]="p.code">{{ p.name }} ({{ p.code }})</option>
          </select>
        </div>

        <div class="form-group">
          <label class="form-label">Menu cấp trên</label>
          <select class="wf-select w-full" [(ngModel)]="currentMenu.parentId">
            <option [value]="null">-- Menu gốc --</option>
            <option *ngFor="let m of getEligibleParents(currentMenu.id)" [value]="m.id">{{ m.name }}</option>
          </select>
        </div>

        <div class="form-group">
          <label class="form-label">Thứ tự sắp xếp</label>
          <input type="number" class="wf-input w-full" [(ngModel)]="currentMenu.orderNum" />
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false" [disabled]="saving">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveMenu()" [disabled]="saving">
            <i class="pi pi-spin pi-spinner" *ngIf="saving" style="margin-right: 4px;"></i>
            Lưu
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: `
    @keyframes shimmer {
      0% { background-position: -200% 0; }
      100% { background-position: 200% 0; }
    }
    .skeleton-shimmer {
      background: linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 50%, #f3f4f6 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
    }
  `
})
export class MenuManagement implements OnInit {
  menus: any[] = [];
  filteredMenus: any[] = [];
  permissions: any[] = [];
  searchKeyword = '';

  displayDialog = false;
  dialogHeader = '';
  isEdit = false;
  currentMenu: any = {};
  
  loading = false;
  saving = false;

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/menus`;

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
    this.loading = true;
    this.http.get<any>(this.apiUrl).subscribe({
      next: (res) => {
        this.menus = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.onSearch();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách menu.' });
      }
    });
  }

  loadPermissions() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/roles/permissions/all`).subscribe({
      next: (res) => {
        this.permissions = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
      },
      error: (err) => {
        console.error('Không thể load permissions', err);
      }
    });
  }

  onSearch() {
    if (this.searchKeyword) {
      const kw = this.searchKeyword.toLowerCase();
      this.filteredMenus = this.menus.filter(m => 
        m.name.toLowerCase().includes(kw) || 
        (m.url && m.url.toLowerCase().includes(kw)) ||
        (m.permission && m.permission.toLowerCase().includes(kw))
      );
    } else {
      this.filteredMenus = this.buildHierarchicalList();
    }
  }

  buildHierarchicalList(): any[] {
    const result: any[] = [];
    const rootNodes = this.menus.filter(m => !m.parentId);
    // Sắp xếp theo orderNum
    rootNodes.sort((a, b) => a.orderNum - b.orderNum);
    
    const visit = (node: any) => {
      result.push(node);
      const children = this.menus.filter(m => m.parentId === node.id);
      children.sort((a, b) => a.orderNum - b.orderNum);
      children.forEach(visit);
    };

    rootNodes.forEach(visit);

    this.menus.forEach(m => {
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
      const parent = this.menus.find(m => m.id === parentId);
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
    if (!currentId) return this.menus.filter(m => !m.parentId);
    return this.menus.filter(m => m.id !== currentId && !m.parentId);
  }

  onAddNew() {
    this.isEdit = false;
    this.currentMenu = { name: '', url: '', icon: '', permission: null, parentId: null, orderNum: 0 };
    this.dialogHeader = 'Thêm mới Menu';
    this.displayDialog = true;
  }

  onEdit(menu: any) {
    this.isEdit = true;
    this.currentMenu = { ...menu };
    this.dialogHeader = 'Chỉnh sửa Menu';
    this.displayDialog = true;
  }

  onSaveMenu() {
    if (!this.currentMenu.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Tên Menu là bắt buộc.' });
      return;
    }

    this.saving = true;
    // Đảm bảo parentId là null hoặc số
    if (this.currentMenu.parentId === 'null' || this.currentMenu.parentId === null) {
      this.currentMenu.parentId = null;
    } else {
      this.currentMenu.parentId = Number(this.currentMenu.parentId);
    }

    if (this.currentMenu.permission === 'null' || this.currentMenu.permission === '') {
      this.currentMenu.permission = null;
    }

    if (this.isEdit) {
      this.http.put(`${this.apiUrl}/${this.currentMenu.id}`, this.currentMenu).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật menu thành công!' });
          this.loadMenus();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật menu.' });
        }
      });
    } else {
      this.http.post(this.apiUrl, this.currentMenu).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Thêm mới menu thành công!' });
          this.loadMenus();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể thêm mới menu.' });
        }
      });
    }
  }

  onDelete(menu: any) {
    const hasChildren = this.menus.some(m => m.parentId === menu.id);
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
