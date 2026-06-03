import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-role-management',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule],
  providers: [MessageService],
  templateUrl: './role-management.component.html',
  styleUrl: './role-management.component.scss'
})
export class RoleManagement implements OnInit {
  roles = signal<any[]>([]);
  searchKeyword = signal<string>('');
  
  displayDialog = signal<boolean>(false);
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentRole = signal<any>({});

  displayPermissionDialog = signal<boolean>(false);
  permissionDialogHeader = signal<string>('');
  activeRoleForPermission = signal<any>(null);
  systemPermissions = signal<any[]>([]);
  selectedPermissionCodes = signal<string[]>([]);
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  savingPermissions = signal<boolean>(false);

  menus = signal<any[]>([]);
  menuPermissionTree = signal<any[]>([]);

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/roles`;

  // Computed signal for filteredRoles
  filteredRoles = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const allRoles = this.roles() || [];
    if (!kw) {
      return [...allRoles];
    }
    return allRoles.filter(r => 
      (r.code?.toLowerCase().includes(kw) ?? false) || 
      (r.name?.toLowerCase().includes(kw) ?? false) || 
      (r.description?.toLowerCase().includes(kw) ?? false)
    );
  });

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    private confirmationService: ConfirmationService,
    public authService: AuthService
  ) {}

  ngOnInit() {
    this.loadRoles();
    this.loadMenus();
    this.loadSystemPermissions();
  }

  loadRoles() {
    this.loading.set(true);
    this.http.get<any>(this.apiUrl)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          this.roles.set(Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []));
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải danh sách nhóm quyền.' });
        }
      });
  }

  loadMenus() {
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/menus`).subscribe({
      next: (res) => {
        this.menus.set(Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []));
        this.updateTree();
      },
      error: (err) => {
        console.error('Không thể tải danh sách menu:', err);
      }
    });
  }

  loadSystemPermissions() {
    this.http.get<any>(`${this.apiUrl}/permissions/all`).subscribe({
      next: (res) => {
        this.systemPermissions.set(Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []));
        this.updateTree();
      },
      error: (err) => {
        console.error('Không thể tải danh sách quyền hệ thống:', err);
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
      const targetUrl = this.getMenuTargetForPermission(p.code);
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
      case 'CATALOG': return '/catalog/unit-of-measurement';
      case 'EAV_FORM_TEMPLATE':
      case 'EQUIPMENT_TYPE':
      case 'EQUIPMENT': return '/equipment/form-management';
      case 'VIRTUAL_FOLDER': return '/digitization/virtual-folders';
      case 'OCR_TRAINING_DATA': return '/digitization/ocr-training';
      case 'DIGITIZATION_TASK': return '/digitization/ocr-upload'; 
      case 'DIGITIZATION': return '/ocr-correction';
      case 'WORKFLOW':
      case 'WORKFLOW_DEFINITION': return '/workflow/borrow-return';
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

  onSearch() {
    // Tự động thông qua computed
  }

  onAddNew() {
    this.isEdit.set(false);
    this.currentRole.set({ code: '', name: '', description: '' });
    this.dialogHeader.set('Thêm mới vai trò');
    this.displayDialog.set(true);
  }

  onEdit(role: any) {
    this.isEdit.set(true);
    this.currentRole.set({ ...role });
    this.dialogHeader.set('Chỉnh sửa vai trò');
    this.displayDialog.set(true);
  }

  onSaveRole() {
    const roleDraft = this.currentRole();
    if (!roleDraft.code || !roleDraft.name) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập Mã và Tên vai trò.' });
      return;
    }

    this.saving.set(true);
    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${roleDraft.id}`, roleDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật thông tin vai trò thành công!' });
            this.loadRoles();
            this.displayDialog.set(false);
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Cập nhật vai trò thất bại.' });
          }
        });
    } else {
      this.http.post<any>(this.apiUrl, roleDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: (created) => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo vai trò mới thành công!' });
            this.loadRoles();
            this.displayDialog.set(false);
          },
          error: (err) => {
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
    this.activeRoleForPermission.set(role);
    this.permissionDialogHeader.set(`Phân quyền vai trò: ${role.name}`);
    this.selectedPermissionCodes.set([]);
    
    // Load existing permissions of role
    this.http.get<any>(`${this.apiUrl}/${role.id}/permissions`).subscribe({
      next: (res) => {
        const list = Array.isArray(res) ? res : (res && Array.isArray(res.value) ? res.value : []);
        this.selectedPermissionCodes.set(list);
        this.displayPermissionDialog.set(true);
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải quyền đã gán.' });
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
    const activeRole = this.activeRoleForPermission();
    if (!activeRole) return;
    
    this.savingPermissions.set(true);
    this.http.post(`${this.apiUrl}/${activeRole.id}/permissions`, this.selectedPermissionCodes())
      .pipe(finalize(() => this.savingPermissions.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Phân quyền thành công', detail: 'Đã lưu thay đổi phân quyền hệ thống!' });
          this.displayPermissionDialog.set(false);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Lưu phân quyền vai trò thất bại.' });
        }
      });
  }
}
