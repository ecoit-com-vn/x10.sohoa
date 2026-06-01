import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-role-management',
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
          <span class="bc-current">Quản lý nhóm quyền</span>
        </div>

        <p class="text-muted mb-4">
          Thiết lập các nhóm quyền (vai trò) hệ thống và cấu hình gán quyền chi tiết cho từng nhóm để kiểm soát truy cập và bảo mật.
        </p>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm kiếm vai trò..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="onSearch()" />
            <button class="btn-tim" (click)="onSearch()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right">
            <button class="btn-green" (click)="onAddNew()">
              <i class="pi pi-plus"></i> Thêm nhóm mới
            </button>
          </div>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th class="col-stt">STT</th>
                <th>Mã vai trò (Code)</th>
                <th>Tên vai trò (Name)</th>
                <th>Mô tả</th>
                <th class="col-hd" style="width: 250px;">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <!-- skeleton loading rows -->
              <ng-container *ngIf="loading">
                <tr *ngFor="let item of [1, 2, 3, 4]">
                  <td class="col-stt"><div class="skeleton-shimmer" style="height: 16px; width: 24px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 100px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 150px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 200px; border-radius: 4px;"></div></td>
                  <td class="col-hd"><div class="skeleton-shimmer" style="height: 24px; width: 180px; border-radius: 4px;"></div></td>
                </tr>
              </ng-container>

              <ng-container *ngIf="!loading">
                <tr *ngFor="let role of filteredRoles; let i = index">
                  <td class="col-stt text-muted">{{ i + 1 }}</td>
                  <td><code>{{ role.code }}</code></td>
                  <td><b class="wf-name-link" (click)="onEdit(role)">{{ role.name }}</b></td>
                  <td><span class="text-muted">{{ role.description }}</span></td>
                  <td class="col-hd">
                    <button class="btn-outlined btn-small mr-1" (click)="onAssignPermissions(role)" title="Phân quyền">
                      <i class="pi pi-shield mr-1"></i> Phân quyền
                    </button>
                    <button class="act-btn act-edit" (click)="onEdit(role)" title="Chỉnh sửa">
                      <i class="pi pi-pencil"></i>
                    </button>
                    <button class="act-btn act-delete" (click)="onDelete(role)" title="Xóa">
                      <i class="pi pi-trash"></i>
                    </button>
                  </td>
                </tr>
                <tr *ngIf="filteredRoles.length === 0">
                  <td colspan="5" class="empty-row">
                    <i class="pi pi-inbox"></i>
                    <div>Không tìm thấy nhóm quyền phù hợp.</div>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ filteredRoles.length }}</b> nhóm quyền.</span>
        </div>

      </div>
    </div>

    <!-- Dialog Thêm/Sửa Nhóm Quyền -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label">Mã nhóm quyền <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentRole.code" placeholder="Ví dụ: QUAN_TRI, CAN_BO..." [disabled]="isEdit || saving" />
        </div>
        
        <div class="form-group">
          <label class="form-label">Tên nhóm quyền <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentRole.name" placeholder="Ví dụ: Quản trị viên hệ thống..." [disabled]="saving" />
        </div>
        
        <div class="form-group">
          <label class="form-label">Mô tả chi tiết</label>
          <textarea class="wf-textarea w-full" rows="3" [(ngModel)]="currentRole.description" placeholder="Ghi chú về phạm vi của vai trò này..." [disabled]="saving"></textarea>
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false" [disabled]="saving">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveRole()" [disabled]="saving">
            <i class="pi pi-spin pi-spinner" *ngIf="saving" style="margin-right: 4px;"></i>
            Lưu
          </button>
        </div>
      </ng-template>
    </p-dialog>

    <!-- Dialog Phân Quyền (Permissions assignment) -->
    <p-dialog [(visible)]="displayPermissionDialog" [header]="permissionDialogHeader" [modal]="true" [style]="{ width: '600px' }" styleClass="evn-dialog-custom">
      <div style="padding-top: 10px;">
        <p class="text-muted mb-3" style="font-size: 0.85rem;">Chọn các quyền hạn cụ thể cấp cho nhóm quyền này:</p>
        
        <div style="display: flex; flex-direction: column; gap: 12px; max-height: 400px; overflow-y: auto; padding-right: 5px;">
          <div *ngFor="let perm of systemPermissions" 
               style="display: flex; align-items: flex-start; gap: 10px; padding: 10px; border: 1px solid #e2e8f0; border-radius: 6px; background-color: #f8fafc;">
            <input type="checkbox" [id]="perm.code" [checked]="isPermissionChecked(perm.code)" (change)="togglePermission(perm.code)" [disabled]="savingPermissions" style="margin-top: 3px; scale: 1.1; cursor: pointer;" />
            <div style="display: flex; flex-direction: column; cursor: pointer;" (click)="!savingPermissions && togglePermission(perm.code)">
              <label [for]="perm.code" style="font-weight: 600; color: #002D72; margin: 0; cursor: pointer;">{{ perm.name }}</label>
              <span style="font-size: 0.8rem; color: #64748b; margin-top: 2px;">{{ perm.description }}</span>
            </div>
          </div>
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayPermissionDialog = false" [disabled]="savingPermissions">Hủy</button>
          <button class="btn-save btn-small" (click)="onSavePermissions()" [disabled]="savingPermissions">
            <i class="pi pi-spin pi-spinner" *ngIf="savingPermissions" style="margin-right: 4px;"></i>
            <i class="pi pi-check-circle mr-1" *ngIf="!savingPermissions"></i> Xác nhận lưu
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
export class RoleManagement implements OnInit {
  roles: any[] = [];
  filteredRoles: any[] = [];
  searchKeyword = '';
  
  displayDialog = false;
  dialogHeader = '';
  isEdit = false;
  currentRole: any = {};

  displayPermissionDialog = false;
  permissionDialogHeader = '';
  activeRoleForPermission: any = null;
  systemPermissions: any[] = [];
  selectedPermissionCodes: string[] = [];
  
  loading = false;
  saving = false;
  savingPermissions = false;

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/roles`;

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadRoles();
    this.loadSystemPermissions();
  }

  loadRoles() {
    this.loading = true;
    this.http.get<any>(this.apiUrl).subscribe({
      next: (res) => {
        this.roles = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.onSearch();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách nhóm quyền.' });
      }
    });
  }

  loadSystemPermissions() {
    this.http.get<any>(`${this.apiUrl}/permissions/all`).subscribe({
      next: (res) => {
        this.systemPermissions = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
      },
      error: (err) => {
        console.error('Không thể tải danh sách quyền hệ thống:', err);
      }
    });
  }

  onSearch() {
    if (this.searchKeyword) {
      const kw = this.searchKeyword.toLowerCase();
      this.filteredRoles = this.roles.filter(r => 
        r.code.toLowerCase().includes(kw) || 
        r.name.toLowerCase().includes(kw) || 
        (r.description && r.description.toLowerCase().includes(kw))
      );
    } else {
      this.filteredRoles = [...this.roles];
    }
  }

  onAddNew() {
    this.isEdit = false;
    this.currentRole = { code: '', name: '', description: '' };
    this.dialogHeader = 'Thêm mới vai trò';
    this.displayDialog = true;
  }

  onEdit(role: any) {
    this.isEdit = true;
    this.currentRole = { ...role };
    this.dialogHeader = 'Chỉnh sửa vai trò';
    this.displayDialog = true;
  }

  onSaveRole() {
    if (!this.currentRole.code || !this.currentRole.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập Mã và Tên vai trò.' });
      return;
    }

    this.saving = true;
    if (this.isEdit) {
      this.http.put(`${this.apiUrl}/${this.currentRole.id}`, this.currentRole).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật thông tin vai trò thành công!' });
          this.loadRoles();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Cập nhật vai trò thất bại.' });
        }
      });
    } else {
      this.http.post<any>(this.apiUrl, this.currentRole).subscribe({
        next: (created) => {
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo vai trò mới thành công!' });
          this.loadRoles();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Tạo vai trò thất bại.' });
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
    this.activeRoleForPermission = role;
    this.permissionDialogHeader = `Phân quyền vai trò: ${role.name}`;
    this.selectedPermissionCodes = [];
    
    // Load existing permissions of role
    this.http.get<any>(`${this.apiUrl}/${role.id}/permissions`).subscribe({
      next: (res) => {
        this.selectedPermissionCodes = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.displayPermissionDialog = true;
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải quyền đã gán.' });
      }
    });
  }

  isPermissionChecked(code: string): boolean {
    return this.selectedPermissionCodes.includes(code);
  }

  togglePermission(code: string) {
    const idx = this.selectedPermissionCodes.indexOf(code);
    if (idx > -1) {
      this.selectedPermissionCodes.splice(idx, 1);
    } else {
      this.selectedPermissionCodes.push(code);
    }
  }

  onSavePermissions() {
    if (!this.activeRoleForPermission) return;
    
    this.savingPermissions = true;
    this.http.post(`${this.apiUrl}/${this.activeRoleForPermission.id}/permissions`, this.selectedPermissionCodes).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Phân quyền thành công', detail: 'Đã lưu thay đổi phân quyền hệ thống!' });
        this.displayPermissionDialog = false;
        this.savingPermissions = false;
      },
      error: (err) => {
        this.savingPermissions = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu phân quyền vai trò thất bại.' });
      }
    });
  }
}
