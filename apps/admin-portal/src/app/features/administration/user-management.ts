import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-user-management',
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
          <span class="bc-current">Quản lý người dùng</span>
        </div>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm kiếm tài khoản, tên..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right" style="display: flex; gap: 8px;">
            <button class="btn-outlined" (click)="onExportExcel()" title="Xuất Excel" [disabled]="loading">
              <i class="pi pi-file-excel"></i> Xuất Excel
            </button>
            <button class="btn-green" (click)="onAddNew()">
              <i class="pi pi-plus"></i> Thêm mới
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th class="col-stt">STT</th>
                <th>Tên đăng nhập</th>
                <th>Họ và tên</th>
                <th>Email</th>
                <th class="col-tt">Trạng thái</th>
                <th class="col-hd">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <!-- skeleton loading rows -->
              <ng-container *ngIf="loading">
                <tr *ngFor="let item of [1, 2, 3, 4, 5]">
                  <td class="col-stt"><div class="skeleton-shimmer" style="height: 16px; width: 24px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 120px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 150px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 180px; border-radius: 4px;"></div></td>
                  <td class="col-tt"><div class="skeleton-shimmer" style="height: 24px; width: 80px; border-radius: 12px;"></div></td>
                  <td class="col-hd"><div class="skeleton-shimmer" style="height: 24px; width: 60px; border-radius: 4px;"></div></td>
                </tr>
              </ng-container>

              <ng-container *ngIf="!loading">
                <tr *ngFor="let user of filteredUsers; let i = index">
                  <td class="col-stt text-muted">{{ i + 1 }}</td>
                  <td><b class="wf-name-link" (click)="onEdit(user)">{{ user.username }}</b></td>
                  <td>{{ user.fullName }}</td>
                  <td><span class="text-muted">{{ user.email }}</span></td>
                  <td class="col-tt">
                    <span class="status-pill"
                      [class.status-active]="user.isActive"
                      [class.status-inactive]="!user.isActive">
                      <i class="pi pi-clock"></i>
                      {{ user.isActive ? 'Hoạt động' : 'Ngưng hoạt động' }}
                    </span>
                  </td>
                  <td class="col-hd">
                    <button class="act-btn act-edit" (click)="onManageUnitRoles(user)" title="Quyền theo đơn vị" style="background-color: #fef3c7; color: #d97706; border-color: #fde68a; margin-right: 4px;">
                      <i class="pi pi-sitemap"></i>
                    </button>
                    <button class="act-btn act-edit" (click)="onEdit(user)" title="Chỉnh sửa">
                      <i class="pi pi-pencil"></i>
                    </button>
                    <button class="act-btn act-delete" (click)="onDelete(user)" title="Xóa">
                      <i class="pi pi-trash"></i>
                    </button>
                  </td>
                </tr>
                <tr *ngIf="filteredUsers.length === 0">
                  <td colspan="6" class="empty-row">
                    <i class="pi pi-inbox"></i>
                    <div>Không tìm thấy người dùng phù hợp.</div>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ filteredUsers.length }}</b> người dùng.</span>
        </div>

      </div>
    </div>

    <!-- Dialog Thêm/Sửa Người dùng -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label">Tên đăng nhập <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentUser.username" placeholder="Nhập username..." [disabled]="isEdit" />
        </div>
        
        <div class="form-group">
          <label class="form-label">Họ và tên <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentUser.fullName" placeholder="Nhập họ và tên..." />
        </div>

        <div class="form-group">
          <label class="form-label">Email</label>
          <input type="email" class="wf-input w-full" [(ngModel)]="currentUser.email" placeholder="example@evnhanoi.vn" />
        </div>
        
        <div class="form-group" style="display: flex; align-items: center; gap: 10px; padding-top: 5px;">
          <input type="checkbox" id="isActiveCheck" [(ngModel)]="currentUser.isActive" style="scale: 1.2; cursor: pointer;" />
          <label for="isActiveCheck" style="font-weight: 600; cursor: pointer; margin: 0;">Trạng thái hoạt động</label>
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false" [disabled]="saving">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveUser()" [disabled]="saving">
            <i class="pi pi-spin pi-spinner" *ngIf="saving" style="margin-right: 4px;"></i>
            Lưu
          </button>
        </div>
      </ng-template>
    </p-dialog>

    <!-- Dialog Quyền theo Đơn vị -->
    <p-dialog [(visible)]="displayUnitRoleDialog" [header]="unitRoleDialogHeader" [modal]="true" [style]="{ width: '650px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <p class="text-muted" style="font-size: 0.85rem;">Phân quyền vai trò cụ thể cho từng Đơn vị thành viên của người dùng:</p>
        
        <!-- Form thêm mới quyền đơn vị -->
        <div style="display: flex; gap: 8px; align-items: flex-end; background-color: #f8fafc; padding: 12px; border-radius: 6px; border: 1px solid #e2e8f0;">
          <div class="form-group" style="flex: 1; margin: 0;">
            <label class="form-label" style="font-size: 0.8rem; font-weight: 600;">Chọn Đơn vị</label>
            <select class="wf-select w-full" [(ngModel)]="newUnitRole.unitId" style="height: 38px;">
              <option [value]="null">-- Chọn Đơn vị --</option>
              <option *ngFor="let u of organizationUnits" [value]="u.id">{{ u.name }}</option>
            </select>
          </div>
          <div class="form-group" style="flex: 1; margin: 0;">
            <label class="form-label" style="font-size: 0.8rem; font-weight: 600;">Chọn Vai trò</label>
            <select class="wf-select w-full" [(ngModel)]="newUnitRole.roleId" style="height: 38px;">
              <option [value]="null">-- Chọn Vai trò --</option>
              <option *ngFor="let r of systemRoles" [value]="r.id">{{ r.name }}</option>
            </select>
          </div>
          <button class="btn-green btn-small" (click)="onAddUnitRole()" style="height: 38px;">
            Thêm
          </button>
        </div>

        <!-- Bảng danh sách quyền đơn vị hiện có -->
        <div class="wf-table-wrap" style="max-height: 250px; overflow-y: auto;">
          <table class="wf-table" style="font-size: 0.85rem;">
            <thead>
              <tr>
                <th style="width: 50px;">STT</th>
                <th>Đơn vị thành viên</th>
                <th>Vai trò được gán</th>
                <th style="width: 80px;">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let ur of assignedUnitRoles; let idx = index">
                <td>{{ idx + 1 }}</td>
                <td><b>{{ getUnitLabel(ur.unitId) }}</b></td>
                <td><span class="status-pill status-active" style="background-color: #eff6ff; color: #1e40af; border-color: #bfdbfe;">{{ getRoleLabel(ur.roleId) }}</span></td>
                <td>
                  <button class="act-btn act-delete" (click)="onRemoveUnitRole(idx)" title="Xóa quyền này">
                    <i class="pi pi-trash"></i>
                  </button>
                </td>
              </tr>
              <tr *ngIf="assignedUnitRoles.length === 0">
                <td colspan="4" class="empty-row" style="padding: 20px;">Chưa cấu hình quyền theo đơn vị.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayUnitRoleDialog = false" [disabled]="savingUnitRoles">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveUnitRoles()" [disabled]="savingUnitRoles">
            <i class="pi pi-spin pi-spinner" *ngIf="savingUnitRoles" style="margin-right: 4px;"></i>
            Lưu thay đổi
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
    :global(html.dark-mode) .skeleton-shimmer {
      background: linear-gradient(90deg, #1e293b 25%, #334155 50%, #1e293b 75%);
      background-size: 200% 100%;
    }
  `
})
export class UserManagement implements OnInit {
  users: any[] = [];
  filteredUsers: any[] = [];
  searchKeyword = '';

  displayDialog = false;
  dialogHeader = '';
  isEdit = false;
  currentUser: any = {};
  
  loading = false;
  saving = false;

  // Quyền theo đơn vị
  organizationUnits: any[] = [];
  systemRoles: any[] = [];
  
  displayUnitRoleDialog = false;
  unitRoleDialogHeader = '';
  activeUserForUnitRole: any = null;
  assignedUnitRoles: any[] = [];
  newUnitRole: any = { unitId: null, roleId: null };
  savingUnitRoles = false;

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/users`;

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadUsers();
    this.loadOrganizationUnits();
    this.loadSystemRoles();
  }

  loadUsers() {
    this.loading = true;
    this.http.get<any>(this.apiUrl).subscribe({
      next: (res) => {
        this.users = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.onSearch();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách tài khoản.' });
      }
    });
  }

  loadOrganizationUnits() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/organization-units`).subscribe({
      next: (res) => { this.organizationUnits = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []); }
    });
  }

  loadSystemRoles() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/roles`).subscribe({
      next: (res) => { this.systemRoles = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []); }
    });
  }

  getUnitLabel(unitId: number): string {
    const u = this.organizationUnits.find(x => x.id === unitId);
    return u ? u.name : `Đơn vị #${unitId}`;
  }

  getRoleLabel(roleId: number): string {
    const r = this.systemRoles.find(x => x.id === roleId);
    return r ? r.name : `Vai trò #${roleId}`;
  }

  onSearch() {
    if (this.searchKeyword) {
      const kw = this.searchKeyword.toLowerCase();
      this.filteredUsers = this.users.filter(u =>
        u.username.toLowerCase().includes(kw) ||
        u.fullName.toLowerCase().includes(kw) ||
        (u.email && u.email.toLowerCase().includes(kw))
      );
    } else {
      this.filteredUsers = [...this.users];
    }
  }

  onAddNew() {
    this.isEdit = false;
    this.currentUser = { username: '', fullName: '', email: '', isActive: true };
    this.dialogHeader = 'Thêm mới tài khoản';
    this.displayDialog = true;
  }

  onEdit(user: any) {
    this.isEdit = true;
    this.currentUser = { ...user };
    this.dialogHeader = 'Chỉnh sửa tài khoản';
    this.displayDialog = true;
  }

  onSaveUser() {
    if (!this.currentUser.username || !this.currentUser.fullName) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập Username và Họ tên.' });
      return;
    }

    this.saving = true;
    if (this.isEdit) {
      this.http.put(`${this.apiUrl}/${this.currentUser.id}`, this.currentUser).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật tài khoản thành công!' });
          this.loadUsers();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật thông tin tài khoản.' });
        }
      });
    } else {
      this.http.post<any>(this.apiUrl, this.currentUser).subscribe({
        next: (created) => {
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo tài khoản mới thành công!' });
          this.loadUsers();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tạo tài khoản mới.' });
        }
      });
    }
  }

  onDelete(user: any) {
    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa tài khoản ${user.username}?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      accept: () => {
        this.http.delete(`${this.apiUrl}/${user.id}`).subscribe({
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
    this.activeUserForUnitRole = user;
    this.unitRoleDialogHeader = `Phân quyền theo đơn vị cho: ${user.username}`;
    this.assignedUnitRoles = [];
    this.newUnitRole = { unitId: null, roleId: null };

    // Tải danh sách quyền đơn vị của user
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/user-unit-roles/user/${user.id}`).subscribe({
      next: (res) => {
        this.assignedUnitRoles = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.displayUnitRoleDialog = true;
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải quyền theo đơn vị của người dùng.' });
      }
    });
  }

  onAddUnitRole() {
    if (this.newUnitRole.unitId === 'null' || this.newUnitRole.roleId === 'null' || !this.newUnitRole.unitId || !this.newUnitRole.roleId) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn cả Đơn vị và Vai trò.' });
      return;
    }
    
    // Đảm bảo không add trùng
    const exists = this.assignedUnitRoles.some(x => x.unitId === Number(this.newUnitRole.unitId) && x.roleId === Number(this.newUnitRole.roleId));
    if (exists) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Quyền đơn vị này đã tồn tại trong danh sách.' });
      return;
    }

    this.assignedUnitRoles.push({
      userId: this.activeUserForUnitRole.id,
      unitId: Number(this.newUnitRole.unitId),
      roleId: Number(this.newUnitRole.roleId)
    });
    this.newUnitRole = { unitId: null, roleId: null };
  }

  onRemoveUnitRole(index: number) {
    this.assignedUnitRoles.splice(index, 1);
  }

  onSaveUnitRoles() {
    if (!this.activeUserForUnitRole) return;
    this.savingUnitRoles = true;
    
    this.http.post(`${environment.apiGatewayUrl}/api/v1/user-unit-roles/user/${this.activeUserForUnitRole.id}`, this.assignedUnitRoles).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Cập nhật phân quyền theo đơn vị thành công!' });
        this.displayUnitRoleDialog = false;
        this.savingUnitRoles = false;
      },
      error: () => {
        this.savingUnitRoles = false;
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
    
    // Giả lập gọi API và tải file
    setTimeout(() => {
      this.messageService.add({
        severity: 'success',
        summary: 'Thành công',
        detail: 'Đã xuất và tải về danh sách người dùng thành công!'
      });
    }, 1200);
  }
}
