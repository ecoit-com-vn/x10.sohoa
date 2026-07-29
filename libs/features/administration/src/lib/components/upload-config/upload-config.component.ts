import { Component, OnInit, signal, computed } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { MessageService } from 'primeng/api';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-upload-config',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ToastModule, MenuModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './upload-config.component.html',
  styleUrl: './upload-config.component.scss'
})
export class UploadConfigComponent implements OnInit {
  configs = signal<any[]>([]);
  filteredConfigs = signal<any[]>([]); // computed instead
  searchKeyword = signal<string>('');
  searchUnitId = signal<number | null>(null);
  orgUnits = signal<any[]>([]);

  displayDialog = signal<boolean>(false);
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentConfig = signal<any>({});

  // Custom inline delete confirm dialog
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);

  // Custom inline lock/unlock confirm dialog
  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);
  
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  actionMenuItems: MenuItem[] = [];

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/upload-configs`;

  // Computed signal for filteredConfigs
  computedFilteredConfigs = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const unitId = this.searchUnitId();
    const allConfigs = this.configs() || [];
    
    return allConfigs.filter(c => {
      const matchesKeyword = !kw || 
        (c.name?.toLowerCase().includes(kw) ?? false) || 
        (c.allowedExtensions?.toLowerCase().includes(kw) ?? false);
        
      const matchesUnit = unitId === null || unitId === undefined || String(unitId) === 'null' || String(unitId) === '' ||
        c.organizationUnitId === Number(unitId);
        
      return matchesKeyword && matchesUnit;
    });
  });

  constructor(
    private http: HttpClient,
    private messageService: MessageService,
    public authService: AuthService
  ) {}

  ngOnInit() {
    this.loadConfigs();
    this.loadOrgUnits();
  }

  openActionMenu(config: any, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      ...(this.authService.hasPermission('UPLOAD_CONFIG_EDIT') ? [{ label: config.isActive ? 'Khóa cấu hình' : 'Mở khóa cấu hình', title: config.isActive ? 'Khóa cấu hình' : 'Mở khóa cấu hình', icon: config.isActive ? 'pi pi-lock color-red' : 'pi pi-lock-open color-blue', command: () => this.onToggleStatusRequest(config) }] : []),
      ...(this.authService.hasPermission('UPLOAD_CONFIG_EDIT') ? [{ label: 'Chỉnh sửa', title:'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(config) }] : []),
      ...(this.authService.hasPermission('UPLOAD_CONFIG_DELETE') ? [{ label: 'Xóa', title:'Xóa', icon: 'pi pi-trash color-red', command: () => this.onDelete(config) }] : []),
    ];
    menu.toggle(event);
  }

  loadConfigs() {
    this.loading.set(true);
    this.http.get<any[]>(this.apiUrl)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          this.configs.set(data || []);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải dữ liệu', detail: 'Không thể tải cấu hình tải lên.' });
        }
      });
  }

  loadOrgUnits() {
    const orgUnitsUrl = `${environment.apiGatewayUrl}/api/v1/organization-units/lookup`;
    this.http.get<any[]>(orgUnitsUrl).subscribe({
      next: (data) => {
        this.orgUnits.set(data || []);
      },
      error: (err) => {
        this.messageService.add({ severity: 'error'});
      }
    });
  }

  onSearch() {
    // Tự động thông qua computed
  }

  splitExtensions(allowedExtensions: string): string[] {
    if (!allowedExtensions) return [];
    return allowedExtensions.split(',').map(e => e.trim().toUpperCase());
  }

  onAddNew() {
    this.isEdit.set(false);
    this.currentConfig.set({ 
      name: '', 
      allowedExtensions: 'pdf,docx,xlsx,jpg,png', 
      maxFileSizeMb: 10, 
      organizationUnitId: null, 
      isActive: true 
    });
    this.dialogHeader.set('Thêm mới cấu hình Upload');
    this.displayDialog.set(true);
  }

  onToggleStatusRequest(config: any) {
    this.lockUnlockTarget.set(config);
    this.showLockUnlockConfirm.set(true);
  }

  onConfirmLockUnlock() {
    const config = this.lockUnlockTarget();
    if (!config) return;
    if (!this.authService.hasPermission('UPLOAD_CONFIG_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa cấu hình.' });
      return;
    }
    const updated = { ...config, isActive: !config.isActive };
    this.lockUnlockLoading.set(true);
    this.http.put(`${this.apiUrl}/${config.id}`, updated)
      .pipe(finalize(() => this.lockUnlockLoading.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `${config.isActive ? 'Khóa' : 'Mở khóa'} cấu hình upload thành công!`
          });
          this.showLockUnlockConfirm.set(false);
          this.lockUnlockTarget.set(null);
          this.loadConfigs();
        },
        error: (err) => {
          const detailMsg = err?.error?.message || err?.message || `Không thể ${config.isActive ? 'khóa' : 'mở khóa'} cấu hình upload.`;
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: detailMsg });
        }
      });
  }

  onCancelLockUnlock() {
    this.showLockUnlockConfirm.set(false);
    this.lockUnlockTarget.set(null);
  }

  onEdit(config: any) {
    this.isEdit.set(true);
    this.currentConfig.set({ ...config });
    this.dialogHeader.set('Chỉnh sửa cấu hình Upload');
    this.displayDialog.set(true);
  }

  onSaveConfig() {
    const configDraft = this.currentConfig();
    if (!configDraft.name || !configDraft.allowedExtensions || !configDraft.maxFileSizeMb) {
      this.messageService.add({ severity: 'error', summary: 'Thiếu thông tin', detail: 'Vui lòng nhập đầy đủ thông tin bắt buộc.' });
      return;
    }

    if (configDraft.maxFileSizeMb <= 0) {
      this.messageService.add({ severity: 'error', summary: 'Giá trị không hợp lệ', detail: 'Dung lượng tối đa phải lớn hơn 0 MB.' });
      return;
    }

    if (configDraft.organizationUnitId === 'null' || configDraft.organizationUnitId === '') {
      configDraft.organizationUnitId = null;
    } else if (configDraft.organizationUnitId !== null && configDraft.organizationUnitId !== undefined) {
      configDraft.organizationUnitId = Number(configDraft.organizationUnitId);
    }

    this.saving.set(true);
    if (this.isEdit()) {
      this.http.put(`${this.apiUrl}/${configDraft.id}`, configDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Cập nhật', detail: 'Cập nhật cấu hình thành công!' });
            this.loadConfigs();
            this.displayDialog.set(false);
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể cập nhật cấu hình.' });
          }
        });
    } else {
      this.http.post(this.apiUrl, configDraft)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Thêm mới', detail: 'Tạo cấu hình mới thành công!' });
            this.loadConfigs();
            this.displayDialog.set(false);
          },
          error: (err) => {
            this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Tạo cấu hình mới thất bại.' });
          }
        });
    }
  }

  onDelete(config: any) {
    this.deleteTarget.set(config);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete() {
    const config = this.deleteTarget();
    if (!config) return;
    this.deleting.set(true);
    this.http.delete(`${this.apiUrl}/${config.id}`)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: `Đã xóa cấu hình "${config.name}" thành công!` });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadConfigs();
        },
        error: () => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể xóa cấu hình này.' });
          this.showDeleteConfirm.set(false);
        }
      });
  }

  onCancelDelete() {
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }
}
