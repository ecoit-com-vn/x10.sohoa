// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\administration\user-group.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { PickListModule } from 'primeng/picklist';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-user-group',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, PickListModule],
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
          <span class="bc-current">Quản lý nhóm người dùng</span>
        </div>

        <p class="text-muted mb-4">
          Quản lý các nhóm người dùng (User Group) giúp gán nhanh vai trò/quyền hạn cho nhiều tài khoản cùng lúc.
        </p>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input type="text" class="wf-search-input"
              placeholder="Tìm kiếm nhóm..."
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
                <th style="width: 80px;">STT</th>
                <th>Tên nhóm người dùng</th>
                <th>Mô tả</th>
                <th class="col-hd" style="width: 320px;">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <!-- skeleton loading rows -->
              <ng-container *ngIf="loading">
                <tr *ngFor="let item of [1, 2, 3]">
                  <td class="col-stt"><div class="skeleton-shimmer" style="height: 16px; width: 24px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 150px; border-radius: 4px;"></div></td>
                  <td><div class="skeleton-shimmer" style="height: 16px; width: 250px; border-radius: 4px;"></div></td>
                  <td class="col-hd"><div class="skeleton-shimmer" style="height: 24px; width: 200px; border-radius: 4px;"></div></td>
                </tr>
              </ng-container>

              <ng-container *ngIf="!loading">
                <tr *ngFor="let g of filteredGroups; let i = index">
                  <td class="col-stt text-muted">{{ i + 1 }}</td>
                  <td><b class="wf-name-link" (click)="onEdit(g)">{{ g.name }}</b></td>
                  <td><span class="text-muted">{{ g.description || '-' }}</span></td>
                  <td class="col-hd">
                    <button class="btn-outlined btn-small mr-1" (click)="onManageMembers(g)" title="Thành viên">
                      <i class="pi pi-users mr-1"></i> Thành viên
                    </button>
                    <button class="btn-outlined btn-small mr-1" (click)="onManageRoles(g)" title="Vai trò">
                      <i class="pi pi-shield mr-1"></i> Vai trò
                    </button>
                    <button class="act-btn act-edit" (click)="onEdit(g)" title="Chỉnh sửa"><i class="pi pi-pencil"></i></button>
                    <button class="act-btn act-delete" (click)="onDelete(g)" title="Xóa"><i class="pi pi-trash"></i></button>
                  </td>
                </tr>
                <tr *ngIf="filteredGroups.length === 0">
                  <td colspan="4" class="empty-row">
                    <i class="pi pi-inbox"></i>
                    <div>Không tìm thấy nhóm người dùng nào phù hợp.</div>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ groups.length }}</b> nhóm.</span>
        </div>
      </div>
    </div>

    <!-- Dialog Thêm/Sửa Nhóm -->
    <p-dialog [(visible)]="displayDialog" [header]="dialogHeader" [modal]="true" [style]="{ width: '450px' }" styleClass="evn-dialog-custom">
      <div style="display: flex; flex-direction: column; gap: 14px; padding-top: 10px;">
        <div class="form-group">
          <label class="form-label">Tên nhóm <span class="required">*</span></label>
          <input type="text" class="wf-input w-full" [(ngModel)]="currentGroup.name" placeholder="Nhập tên nhóm..." />
        </div>
        
        <div class="form-group">
          <label class="form-label">Mô tả chi tiết</label>
          <textarea class="wf-textarea w-full" rows="3" [(ngModel)]="currentGroup.description" placeholder="Nhập mô tả nhóm người dùng..."></textarea>
        </div>
      </div>
      
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayDialog = false" [disabled]="saving">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveGroup()" [disabled]="saving">
            <i class="pi pi-spin pi-spinner" *ngIf="saving" style="margin-right: 4px;"></i>
            Lưu
          </button>
        </div>
      </ng-template>
    </p-dialog>

    <!-- Dialog Gán Thành Viên (PickList) -->
    <p-dialog [(visible)]="displayMemberDialog" [header]="memberDialogHeader" [modal]="true" [style]="{ width: '800px' }" styleClass="evn-dialog-custom">
      <div style="padding-top: 10px;">
        <p-pickList 
          [source]="sourceUsers" 
          [target]="targetUsers" 
          sourceHeader="Người dùng sẵn có" 
          targetHeader="Thành viên đã gán" 
          [dragdrop]="true" 
          [responsive]="true" 
          [sourceStyle]="{height: '350px'}" 
          [targetStyle]="{height: '350px'}" 
          filterBy="username,fullName" 
          sourceFilterPlaceholder="Tìm kiếm tài khoản..." 
          targetFilterPlaceholder="Tìm kiếm thành viên...">
          <ng-template let-u pTemplate="item">
            <div style="display: flex; flex-direction: column; gap: 2px;">
              <span style="font-weight: 600; color: #002D72;">{{u.username}}</span>
              <span style="font-size: 0.8rem; color: #64748b;">{{u.fullName}}</span>
            </div>
          </ng-template>
        </p-pickList>
      </div>
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayMemberDialog = false" [disabled]="savingMembers">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveMembers()" [disabled]="savingMembers">
            <i class="pi pi-spin pi-spinner" *ngIf="savingMembers" style="margin-right: 4px;"></i>
            Lưu thay đổi
          </button>
        </div>
      </ng-template>
    </p-dialog>

    <!-- Dialog Gán Vai Trò (PickList) -->
    <p-dialog [(visible)]="displayRoleDialog" [header]="roleDialogHeader" [modal]="true" [style]="{ width: '800px' }" styleClass="evn-dialog-custom">
      <div style="padding-top: 10px;">
        <p-pickList 
          [source]="sourceRoles" 
          [target]="targetRoles" 
          sourceHeader="Vai trò sẵn có" 
          targetHeader="Vai trò đã gán" 
          [dragdrop]="true" 
          [responsive]="true" 
          [sourceStyle]="{height: '350px'}" 
          [targetStyle]="{height: '350px'}" 
          filterBy="code,name" 
          sourceFilterPlaceholder="Tìm theo mã/tên..." 
          targetFilterPlaceholder="Tìm vai trò...">
          <ng-template let-r pTemplate="item">
            <div style="display: flex; flex-direction: column; gap: 2px;">
              <span style="font-weight: 600; color: #002D72;">{{r.name}}</span>
              <span style="font-size: 0.8rem; color: #64748b;"><code>{{r.code}}</code></span>
            </div>
          </ng-template>
        </p-pickList>
      </div>
      <ng-template #footer>
        <div class="flex gap-2 justify-content-end pt-3" style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9;">
          <button class="btn-outlined btn-small" (click)="displayRoleDialog = false" [disabled]="savingRoles">Hủy</button>
          <button class="btn-save btn-small" (click)="onSaveRoles()" [disabled]="savingRoles">
            <i class="pi pi-spin pi-spinner" *ngIf="savingRoles" style="margin-right: 4px;"></i>
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
  `
})
export class UserGroupComponent implements OnInit {
  groups: any[] = [];
  filteredGroups: any[] = [];
  searchKeyword = '';

  displayDialog = false;
  dialogHeader = '';
  isEdit = false;
  currentGroup: any = {};
  
  loading = false;
  saving = false;

  // Thành viên
  displayMemberDialog = false;
  memberDialogHeader = '';
  activeGroupForMember: any = null;
  sourceUsers: any[] = [];
  targetUsers: any[] = [];
  savingMembers = false;

  // Vai trò
  displayRoleDialog = false;
  roleDialogHeader = '';
  activeGroupForRole: any = null;
  sourceRoles: any[] = [];
  targetRoles: any[] = [];
  savingRoles = false;

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/user-groups`;

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadGroups();
  }

  loadGroups() {
    this.loading = true;
    this.http.get<any[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.groups = data;
        this.onSearch();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách nhóm.' });
      }
    });
  }

  onSearch() {
    if (this.searchKeyword) {
      const kw = this.searchKeyword.toLowerCase();
      this.filteredGroups = this.groups.filter(g => 
        g.name.toLowerCase().includes(kw) || 
        (g.description && g.description.toLowerCase().includes(kw))
      );
    } else {
      this.filteredGroups = [...this.groups];
    }
  }

  onAddNew() {
    this.isEdit = false;
    this.currentGroup = { name: '', description: '' };
    this.dialogHeader = 'Thêm mới nhóm người dùng';
    this.displayDialog = true;
  }

  onEdit(group: any) {
    this.isEdit = true;
    this.currentGroup = { ...group };
    this.dialogHeader = 'Chỉnh sửa nhóm người dùng';
    this.displayDialog = true;
  }

  onSaveGroup() {
    if (!this.currentGroup.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Tên nhóm người dùng là bắt buộc.' });
      return;
    }

    this.saving = true;
    if (this.isEdit) {
      this.http.put(`${this.apiUrl}/${this.currentGroup.id}`, this.currentGroup).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật nhóm thành công!' });
          this.loadGroups();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật nhóm.' });
        }
      });
    } else {
      this.http.post(this.apiUrl, this.currentGroup).subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Thêm mới nhóm thành công!' });
          this.loadGroups();
          this.displayDialog = false;
          this.saving = false;
        },
        error: (err) => {
          this.saving = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tạo nhóm mới.' });
        }
      });
    }
  }

  onDelete(group: any) {
    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa nhóm người dùng ${group.name}?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Đồng ý',
      rejectLabel: 'Hủy',
      accept: () => {
        this.http.delete(`${this.apiUrl}/${group.id}`).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: 'Đã xóa nhóm người dùng thành công!' });
            this.loadGroups();
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Xóa nhóm người dùng thất bại.' });
          }
        });
      }
    });
  }

  onManageMembers(group: any) {
    this.activeGroupForMember = group;
    this.memberDialogHeader = `Quản lý thành viên nhóm: ${group.name}`;
    this.sourceUsers = [];
    this.targetUsers = [];

    // Load tất cả người dùng
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/users`).subscribe({
      next: (allUsers) => {
        // Load thành viên của nhóm
        this.http.get<any[]>(`${this.apiUrl}/${group.id}/members`).subscribe({
          next: (members) => {
            const memberIds = new Set(members.map(m => m.id));
            this.targetUsers = allUsers.filter(u => memberIds.has(u.id));
            this.sourceUsers = allUsers.filter(u => !memberIds.has(u.id));
            this.displayMemberDialog = true;
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
    if (!this.activeGroupForMember) return;
    this.savingMembers = true;
    const userIds = this.targetUsers.map(u => u.id);

    this.http.post(`${this.apiUrl}/${this.activeGroupForMember.id}/members`, userIds).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thay đổi thành viên nhóm!' });
        this.displayMemberDialog = false;
        this.savingMembers = false;
      },
      error: () => {
        this.savingMembers = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi thành viên nhóm thất bại.' });
      }
    });
  }

  onManageRoles(group: any) {
    this.activeGroupForRole = group;
    this.roleDialogHeader = `Quản lý vai trò nhóm: ${group.name}`;
    this.sourceRoles = [];
    this.targetRoles = [];

    // Load tất cả vai trò
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/roles`).subscribe({
      next: (allRoles) => {
        // Load vai trò của nhóm
        this.http.get<any[]>(`${this.apiUrl}/${group.id}/roles`).subscribe({
          next: (roles) => {
            const roleIds = new Set(roles.map(r => r.id));
            this.targetRoles = allRoles.filter(r => roleIds.has(r.id));
            this.sourceRoles = allRoles.filter(r => !roleIds.has(r.id));
            this.displayRoleDialog = true;
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
    if (!this.activeGroupForRole) return;
    this.savingRoles = true;
    const roleIds = this.targetRoles.map(r => r.id);

    this.http.post(`${this.apiUrl}/${this.activeGroupForRole.id}/roles`, roleIds).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu thay đổi vai trò của nhóm!' });
        this.displayRoleDialog = false;
        this.savingRoles = false;
      },
      error: () => {
        this.savingRoles = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi vai trò nhóm thất bại.' });
      }
    });
  }
}
