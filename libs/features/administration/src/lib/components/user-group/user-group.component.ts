// E:\ecoit\sohoax10\sohoa.frontend\apps\admin-portal\src\app\features\administration\user-group.component.ts
import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { PickListModule } from 'primeng/picklist';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-user-group',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, PickListModule],
  providers: [MessageService],
  templateUrl: './user-group.component.html',
  styleUrl: './user-group.component.scss'
})
export class UserGroupComponent implements OnInit {
  groups = signal<any[]>([]);
  searchKeyword = signal<string>('');

  displayDialog = signal<boolean>(false);
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentGroup = signal<any>({});
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  // Thành viên
  displayMemberDialog = signal<boolean>(false);
  memberDialogHeader = signal<string>('');
  activeGroupForMember = signal<any>(null);
  sourceUsers = signal<any[]>([]);
  targetUsers = signal<any[]>([]);
  savingMembers = signal<boolean>(false);

  // Vai trò
  displayRoleDialog = signal<boolean>(false);
  roleDialogHeader = signal<string>('');
  activeGroupForRole = signal<any>(null);
  sourceRoles = signal<any[]>([]);
  targetRoles = signal<any[]>([]);
  savingRoles = signal<boolean>(false);

  // Quyền trực tiếp nhóm
  displayPermissionDialog = signal<boolean>(false);
  permissionDialogHeader = signal<string>('');
  activeGroupForPermission = signal<any>(null);
  systemPermissions = signal<any[]>([]);
  selectedPermissionCodes = signal<string[]>([]);
  savingPermissions = signal<boolean>(false);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/user-groups`;

  // Computed signal for filteredGroups
  filteredGroups = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const allGroups = this.groups() || [];
    if (!kw) {
      return [...allGroups];
    }
    return allGroups.filter(g => 
      (g.name?.toLowerCase().includes(kw) ?? false) || 
      (g.description?.toLowerCase().includes(kw) ?? false)
    );
  });

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit() {
    this.loadGroups();
    this.loadSystemPermissions();
  }

  loadGroups() {
    this.loading.set(true);
    this.http.get<any[]>(this.apiUrl)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          this.groups.set(data || []);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách nhóm.' });
        }
      });
  }

  onSearch() {
    // Tự động thông qua computed
  }

  onAddNew() {
    this.isEdit.set(false);
    this.currentGroup.set({ name: '', description: '' });
    this.dialogHeader.set('Thêm mới nhóm người dùng');
    this.displayDialog.set(true);
  }

  onEdit(group: any) {
    this.isEdit.set(true);
    this.currentGroup.set({ ...group });
    this.dialogHeader.set('Chỉnh sửa nhóm người dùng');
    this.displayDialog.set(true);
  }

  onSaveGroup() {
    const groupDraft = this.currentGroup();
    if (!groupDraft.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Tên nhóm người dùng là bắt buộc.' });
      return;
    }

    this.saving.set(true);
    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${groupDraft.id}`, groupDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật nhóm thành công!' });
            this.loadGroups();
            this.displayDialog.set(false);
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật nhóm.' });
          }
        });
    } else {
      this.http.post(this.apiUrl, groupDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Thêm mới nhóm thành công!' });
            this.loadGroups();
            this.displayDialog.set(false);
          },
          error: (err) => {
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
    this.activeGroupForMember.set(group);
    this.memberDialogHeader.set(`Quản lý thành viên nhóm: ${group.name}`);
    this.sourceUsers.set([]);
    this.targetUsers.set([]);

    // Load tất cả người dùng
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/users`).subscribe({
      next: (allUsers) => {
        // Load thành viên của nhóm
        this.http.get<any[]>(`${this.apiUrl}/${group.id}/members`).subscribe({
          next: (members) => {
            const memberIds = new Set(members.map(m => m.id));
            this.targetUsers.set((allUsers || []).filter(u => memberIds.has(u.id)));
            this.sourceUsers.set((allUsers || []).filter(u => !memberIds.has(u.id)));
            this.displayMemberDialog.set(true);
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
          this.displayMemberDialog.set(false);
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi thành viên nhóm thất bại.' });
        }
      });
  }

  onManageRoles(group: any) {
    this.activeGroupForRole.set(group);
    this.roleDialogHeader.set(`Quản lý vai trò nhóm: ${group.name}`);
    this.sourceRoles.set([]);
    this.targetRoles.set([]);

    // Load tất cả vai trò
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/roles`).subscribe({
      next: (allRoles) => {
        // Load vai trò của nhóm
        this.http.get<any[]>(`${this.apiUrl}/${group.id}/roles`).subscribe({
          next: (roles) => {
            const roleIds = new Set(roles.map(r => r.id));
            this.targetRoles.set((allRoles || []).filter(r => roleIds.has(r.id)));
            this.sourceRoles.set((allRoles || []).filter(r => !roleIds.has(r.id)));
            this.displayRoleDialog.set(true);
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
          this.displayRoleDialog.set(false);
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi vai trò nhóm thất bại.' });
        }
      });
  }

  loadSystemPermissions() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/roles/permissions/all`).subscribe({
      next: (res: any) => {
        this.systemPermissions.set(Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []));
      },
      error: (err) => {
        console.error('Không thể tải danh sách quyền hệ thống:', err);
      }
    });
  }

  onManagePermissions(group: any) {
    this.activeGroupForPermission.set(group);
    this.permissionDialogHeader.set(`Phân quyền trực tiếp cho nhóm: ${group?.name || ''}`);
    this.selectedPermissionCodes.set([]);
    
    this.http.get<any>(`${this.apiUrl}/${group.id}/permissions`).subscribe({
      next: (res: any) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.selectedPermissionCodes.set(list);
        this.displayPermissionDialog.set(true);
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
          this.displayPermissionDialog.set(false);
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu thay đổi phân quyền trực tiếp thất bại.' });
        }
      });
  }
}
