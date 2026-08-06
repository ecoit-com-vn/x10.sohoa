import { Component, OnInit, signal, computed } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  EcoInputTreeSelectComponent,
  EcoPaginatorComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
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
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ToastModule,
    MenuModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    EcoPaginatorComponent,
    DeleteConfirmDialogComponent
  ],
  providers: [MessageService],
  templateUrl: './upload-config.component.html',
  styleUrl: './upload-config.component.scss'
})
export class UploadConfigComponent implements OnInit {
  configs = signal<any[]>([]);
  filteredConfigs = signal<any[]>([]); // computed instead
  searchKeyword = signal<string>('');
  searchTypeFile = signal<string>('');
  appliedKeyword = signal<string>('');
  appliedTypeFile = signal<string>('');
  searchUnitId = signal<number | null>(null);
  searchStatus = signal<boolean | null>(null);
  currentPage = signal(1);
  pageSize = signal(10);
  orgUnits = signal<any[]>([]);
  orgUnitTree = computed(() => this.buildOrgTree(this.orgUnits()));
  primengOrgUnitTree = computed(() => {
    const buildPrimeNGNodes = (nodes: any[]): any[] => {
      return nodes.map((n) => ({
        key: n.id,
        label: n.name,
        data: n,
        children: n.children && n.children.length ? buildPrimeNGNodes(n.children) : []
      }));
    };

    return buildPrimeNGNodes(this.orgUnitTree());
  });

  displayDialog = signal<boolean>(false);
  dialogHeader = signal<string>('');
  isEdit = signal<boolean>(false);
  currentConfig = signal<any>({});

  // Custom inline delete confirm dialog
  showDeleteConfirm = signal<boolean>(false);
  deleteTarget = signal<any>(null);
  deleting = signal<boolean>(false);

  // Chuẩn hóa tên cấu hình hiển thị trong popup xác nhận xóa dùng chung.
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.name || '');

  // Custom inline lock/unlock confirm dialog
  showLockUnlockConfirm = signal<boolean>(false);
  lockUnlockTarget = signal<any>(null);
  lockUnlockLoading = signal<boolean>(false);

  loading = signal<boolean>(false);
  saving = signal<boolean>(false);
  actionMenuItems: MenuItem[] = [];
  serverErrors = signal<any>({});
  formSubmitted = signal<boolean>(false);
  touchedFields = signal<Record<string, boolean>>({});

  onFieldTouched(field: string) {
    this.touchedFields.update(fields => ({ ...fields, [field]: true }));
  }

  configNameError = computed(() => {
    const isTouched = this.touchedFields()['name'];
    if ((this.formSubmitted() || isTouched) && !this.currentConfig().name) return 'Tên cấu hình là bắt buộc';
    return this.serverErrors().name || this.serverErrors().Name || '';
  });

  configMaxFileSizeMbError = computed(() => {
    const isTouched = this.touchedFields()['maxFileSizeMb'];
    if ((this.formSubmitted() || isTouched) && !this.currentConfig().maxFileSizeMb) return 'Dung lượng tối đa là bắt buộc';
    return this.serverErrors().maxFileSizeMb || this.serverErrors().MaxFileSizeMb || '';
  });

  configAllowedExtensionsError = computed(() => {
    const isTouched = this.touchedFields()['allowedExtensions'];
    if ((this.formSubmitted() || isTouched) && !this.currentConfig().allowedExtensions) return 'Định dạng file được phép là bắt buộc';
    return this.serverErrors().allowedExtensions || this.serverErrors().AllowedExtensions || '';
  });

  formHasErrors = computed(() => {
    return !!this.configNameError() || !!this.configMaxFileSizeMbError() || !!this.configAllowedExtensionsError();
  });

  private apiUrl = `${environment.apiGatewayUrl}/api/v1/upload-configs`;

  // Computed signal for filteredConfigs
  computedFilteredConfigs = computed(() => {
    const kw = this.appliedKeyword().toLowerCase().trim();
    const type = this.appliedTypeFile().toLowerCase().trim();
    const unitId = this.searchUnitId();
    const status = this.searchStatus();
    const allConfigs = this.configs() || [];

    return allConfigs.filter(c => {
      const matchesKeyword = !kw ||
        (c.name?.toLowerCase().includes(kw) ?? false) ||
        (c.allowedExtensions?.toLowerCase().includes(kw) ?? false);
      const matchesTypeFile = !type ||
        (c.allowedExtensions?.toLowerCase().includes(type) ?? false) ||
        (c.allowedExtensions?.toLowerCase().includes(type) ?? false);

      const matchesUnit = unitId === null || unitId === undefined || String(unitId) === 'null' || String(unitId) === '' ||
        c.organizationUnitId === Number(unitId);

      const matchesStatus = status === null || c.isActive === status;

      return matchesKeyword && matchesTypeFile && matchesUnit && matchesStatus;
    });
  });

  paginatedConfigs = computed(() => {
    const first = (this.currentPage() - 1) * this.pageSize();
    return this.computedFilteredConfigs().slice(first, first + this.pageSize());
  });

  totalFilteredConfigs = computed(() => this.computedFilteredConfigs().length);

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
      ...(this.authService.hasPermission('UPLOAD_CONFIG_EDIT') ? [{ label: 'Chỉnh sửa', title:'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(config) }] : []),
      ...(this.authService.hasPermission('UPLOAD_CONFIG_EDIT') ? [{ label: config.isActive ? 'Khóa cấu hình' : 'Mở khóa cấu hình', title: config.isActive ? 'Khóa cấu hình' : 'Mở khóa cấu hình', icon: config.isActive ? 'pi pi-lock color-red' : 'pi pi-lock-open color-blue', command: () => this.onToggleStatusRequest(config) }] : []),
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
          const totalPages = Math.max(1, Math.ceil(this.totalFilteredConfigs() / this.pageSize()));
          if (this.currentPage() > totalPages) {
            this.currentPage.set(totalPages);
          }
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
    this.appliedKeyword.set(this.searchKeyword().trim());
    this.appliedTypeFile.set(this.searchTypeFile().trim());
    this.currentPage.set(1);
  }

  onResetSearch(): void {
    this.searchKeyword.set('');
    this.appliedKeyword.set('');
    this.searchTypeFile.set('');
    this.appliedTypeFile.set('');
    this.searchUnitId.set(null);
    this.searchStatus.set(null);
    this.currentPage.set(1);
    this.loadConfigs();
  }

  onUnitFilterChange(unitId: number | null): void {
    this.searchUnitId.set(unitId);
    this.currentPage.set(1);
    this.loadConfigs();
  }

  onStatusFilterChange(status: string | boolean | null): void {
    const normalizedStatus =
      status === true || status === 'true'
        ? true
        : status === false || status === 'false'
          ? false
          : null;

    this.searchStatus.set(normalizedStatus);
    this.currentPage.set(1);
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize.set(pageSize);
    this.currentPage.set(1);
  }

  buildOrgTree(units: any[]): any[] {
    const map = new Map<number, any>();
    const roots: any[] = [];

    units.forEach((u) => map.set(u.id, { ...u, children: [] }));
    map.forEach((node) => {
      if (node.parentId && map.has(node.parentId)) {
        map.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });

    return roots;
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
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.touchedFields.set({});
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
    this.formSubmitted.set(false);
    this.serverErrors.set({});
    this.touchedFields.set({});
    this.dialogHeader.set('Chỉnh sửa cấu hình Upload');
    this.displayDialog.set(true);
  }

  updateCurrentConfig(field: string, value: any) {
    this.currentConfig.update(config => ({ ...config, [field]: value }));
    this.serverErrors.update(errors => {
      const copy = { ...errors };
      delete copy[field];
      const capitalized = field.charAt(0).toUpperCase() + field.slice(1);
      delete copy[capitalized];
      return copy;
    });
  }

  onSaveConfig() {
    this.formSubmitted.set(true);
    this.serverErrors.set({});

    if (this.formHasErrors()) {
      return;
    }

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

  onDelete(config: any): void {
    this.deleteTarget.set(config);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete(): void {
    const config = this.deleteTarget();

    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!config || this.deleting()) {
      return;
    }

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
        error: (err) => {
          const detail =
            err?.error?.message ||
            err?.message ||
            'Không thể xóa cấu hình này.';

          // Giữ popup mở để hiển thị lỗi backend và cho phép thử lại.
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail });
        }
      });
  }

  onCancelDelete(): void {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.deleting()) {
      return;
    }

    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }
}
