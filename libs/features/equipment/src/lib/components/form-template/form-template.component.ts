import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import {
  DeleteConfirmDialogComponent,
  EcoPaginatorComponent,
  WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { CardModule } from 'primeng/card';
import { TextareaModule } from 'primeng/textarea';
import { Dialog } from 'primeng/dialog';
import { Select } from 'primeng/select';
import { ActivatedRoute, Router } from '@angular/router';
import { FormTemplateService, EavFormTemplate } from '../../data-access/form-template.service';
import { EquipmentTypeService } from '../../data-access/equipment-type.service';
import { LoadingService, EavFormService } from '@sohoa.frontend/shared/core';
import { finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { combineLatest } from 'rxjs';

interface FormField {
  id: string;
  name: string;
  label: string;
  type: string;
  placeholder?: string;
  required: boolean;
  options?: string[];
  helpText?: string;
  width: number;
  dataSourceType?: 'manual' | 'catalog';
  catalogType?: string;
  description?: string;
  selectAll?: boolean;
  active?: boolean;
}

@Component({
  selector: 'app-form-template',
  standalone: true,
  imports: [
    CommonModule,
    EcoPaginatorComponent,
    FormsModule,
    ToastModule,
    MenuModule,
    ButtonModule,
    InputTextModule,
    CheckboxModule,
    CardModule,
    TextareaModule,
    Dialog,
    Select,
    WfBreadcrumbComponent,
    DeleteConfirmDialogComponent,
  ],
  providers: [MessageService],
  templateUrl: './form-template.component.html',
  styleUrl: './form-template.component.scss'
})
export class FormTemplateComponent implements OnInit {
  private loadingService = inject(LoadingService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private formTemplateService = inject(FormTemplateService);
  private equipmentTypeService = inject(EquipmentTypeService);
  private messageService = inject(MessageService);
  private eavFormService = inject(EavFormService);

  showConfirmDelete = signal<boolean>(false);
  deleteTarget = signal<EavFormTemplate | null>(null);
  deleteLoading = signal<boolean>(false);
  // Tách target xóa khỏi state khóa/mở khóa và chuẩn hóa tên cho popup dùng chung.
  readonly deleteTargetLabel = computed(() => this.deleteTarget()?.name ?? '');
  showConfirmLock = signal<boolean>(false);
  showConfirmUnlock = signal<boolean>(false);
  targetForm: EavFormTemplate | null = null;
  lockAction = signal<'lock' | 'unlock' | null>(null);

  forms = signal<EavFormTemplate[]>([]);
  searchKeyword = signal<string>('');
  selectedGridTypeId = signal<number | null>(null);
  selectedEquipmentTypeId = signal<string>('');
  selectedStatus = signal<boolean | null>(null);

  viewState = signal<'list' | 'detail'>('list');
  showVersionsDialog = signal<boolean>(false);
  selectedTemplate = signal<EavFormTemplate | null>(null);
  versionList = signal<EavFormTemplate[]>([]);
  showConfirmRestore = signal<boolean>(false);
  restoreTarget = signal<EavFormTemplate | null>(null);
  restoringVersion = signal<boolean>(false);

  loading = signal<boolean>(false);
  actionMenuItems: MenuItem[] = [];

  first = signal<number>(0);
  rows = signal<number>(10);

  openActionMenu(form: EavFormTemplate, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      { label: 'Xem chi tiết', title: 'Xem chi tiết', icon: 'pi pi-eye color-teal', command: () => this.viewDetails(form) },
      { label: 'Xem phiên bản', title: 'Xem phiên bản', icon: 'pi pi-history color-blue', command: () => this.viewVersions(form) },
      { label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(form) },
      ...(form.isActive ? [{ label: 'Khóa biểu mẫu', title: 'Khóa biểu mẫu', icon: 'pi pi-lock color-red', command: () => this.lockForm(form) }] : [{ label: 'Mở khóa biểu mẫu', icon: 'pi pi-lock-open color-teal', command: () => this.unlockForm(form) }]),
      { label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.deactivateForm(form) },
    ];
    menu.toggle(event);
  }

  equipmentTypes = signal<any[]>([]);
  gridTypes = signal<any[]>([]);

  filteredForms = computed(() => {
    const allTemplates = this.forms().filter(f => !f.isDeleted);

    // Group templates by code and select the latest version for each unique code
    const latestTemplatesMap = new Map<string, EavFormTemplate>();
    for (const t of allTemplates) {
      const code = t.code || '';
      const existing = latestTemplatesMap.get(code);
      if (!existing || t.version > existing.version) {
        latestTemplatesMap.set(code, t);
      }
    }
    let result = Array.from(latestTemplatesMap.values());

    const keyword = this.searchKeyword().trim().toLowerCase();
    if (keyword) {
      result = result.filter(f =>
        (f.name?.toLowerCase().includes(keyword) ?? false) ||
        (f.code?.toLowerCase().includes(keyword) ?? false) ||
        (f.id?.toLowerCase().includes(keyword) ?? false)
      );
    }

    const gridId = this.selectedGridTypeId();
    if (gridId !== null && gridId !== undefined && gridId.toString() !== '' && gridId.toString() !== 'null') {
      const targetGridId = Number(gridId);
      result = result.filter(f => Number(f.gridTypeId) === targetGridId);
    }

    const eqTypeId = this.selectedEquipmentTypeId();
    if (eqTypeId && eqTypeId !== 'null') {
      result = result.filter(f => f.category === eqTypeId);
    }

    const status = this.selectedStatus();
    if (status !== null && status !== undefined && status.toString() !== '' && status.toString() !== 'null') {
      const statusBool = status.toString() === 'true';
      result = result.filter(f => f.isActive === statusBool);
    }

    result = [...result].sort((a, b) => {
      const timeA = a.createdAt ? new Date(a.createdAt).getTime() : 0;
      const timeB = b.createdAt ? new Date(b.createdAt).getTime() : 0;
      return timeB - timeA;
    });

    return result;
  });

  paginatedForms = computed(() => {
    const start = this.first();
    const end = start + this.rows();
    return this.filteredForms().slice(start, end);
  });

  detailFields = computed(() => {
    const template = this.selectedTemplate();
    if (!template || !template.formSchema) return [];
    try {
      const parsed = JSON.parse(template.formSchema);
      return Array.isArray(parsed) ? parsed : (parsed.fields && Array.isArray(parsed.fields) ? parsed.fields : []);
    } catch (e) {
      console.error('Failed to parse form schema', e);
      return [];
    }
  });

  catalogOptionsMap = signal<{ [catalogCode: string]: string[] }>({});

  loadCatalogOptions(catalogCode: string) {
    if (!catalogCode || this.catalogOptionsMap()[catalogCode]) return;
    this.eavFormService.getCatalogTypeByCode(catalogCode).subscribe({
      next: (catalogType) => {
        if (catalogType && catalogType.id) {
          this.eavFormService.getCatalogsLookup(catalogType.id).subscribe({
            next: (items) => {
              const options = (items || []).map((item: any) => item.name || item.code);
              this.catalogOptionsMap.update(prev => ({
                ...prev,
                [catalogCode]: options
              }));
            },
            error: (err) => console.error(`Failed to load catalogs lookup for ${catalogCode}`, err)
          });
        }
      },
      error: (err) => console.error(`Failed to load catalog type for ${catalogCode}`, err)
    });
  }

  constructor() {
    effect(() => {
      this.searchKeyword();
      this.selectedGridTypeId();
      this.selectedEquipmentTypeId();
      this.selectedStatus();
      this.first.set(0);
    }, { allowSignalWrites: true });

    effect(() => {
      const currentFields = this.detailFields();
      if (!currentFields) return;
      currentFields.forEach((f: FormField) => {
        if (f.dataSourceType === 'catalog' && f.catalogType) {
          this.loadCatalogOptions(f.catalogType);
        }
      });
    });

    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed())
      .subscribe(([params, query]) => {
        const id = params.get('id');
        if (!id) {
          this.viewState.set('list');
          this.selectedTemplate.set(null);
          if (this.forms().length === 0) {
            this.loadForms();
          }
          return;
        }
        const versionRaw = query.get('version');
        const version = versionRaw ? Number(versionRaw) : null;
        this.loadDetail(id, version && version > 0 ? version : null);
      });
  }

  ngOnInit() {
    this.loadEquipmentTypes();
    this.loadGridTypes();
  }

  getExtractionPositionLabel(val?: string): string {
    switch (val) {
      case 'all': return 'Tất cả';
      case 'first': return 'Trang đầu';
      case 'last': return 'Trang cuối';
      case 'first_last': return 'Trang đầu + trang cuối';
      default: return 'Tất cả';
    }
  }

  private loadDetail(id: string, version: number | null) {
    this.loadingService.show();
    const request$ = version != null
      ? this.formTemplateService.getTemplateByIdAndVersion(id, version)
      : this.formTemplateService.getTemplateById(id);

    request$
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: (detail) => {
          this.selectedTemplate.set(detail);
          this.viewState.set('detail');
        },
        error: (err) => {
          console.error('Failed to load template detail', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải chi tiết biểu mẫu.'
          });
          this.router.navigate(['/equipment/form-template']);
        }
      });
  }

  onGridTypeChange(gridTypeId: number | null) {
    this.selectedGridTypeId.set(gridTypeId);
    this.selectedEquipmentTypeId.set('');
    this.loadEquipmentTypes(gridTypeId ?? undefined);
  }

  loadEquipmentTypes(gridTypeId?: number) {
    this.equipmentTypeService.getEquipmentTypes(1, 1000, undefined, undefined, gridTypeId, true).subscribe({
      next: (res) => {
        if (res && res.items) {
          const mapped = res.items.map((item: any) => ({
            ...item,
            value: item.code || item.id
          }));
          this.equipmentTypes.set(mapped);
        } else {
          this.equipmentTypes.set([]);
        }
      },
      error: (err) => {
        console.error('Failed to load equipment types', err);
        this.equipmentTypes.set([]);
      }
    });
  }

  loadGridTypes() {
    this.equipmentTypeService.getGridTypesLookup().subscribe({
      next: (types) => {
        this.gridTypes.set(types || []);
      },
      error: (err) => {
        console.error('Failed to load grid types', err);
      }
    });
  }

  loadForms() {
    this.loadingService.show();
    this.formTemplateService.getTemplates()
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: (data) => {
          this.forms.set(data || []);
        },
        error: (err) => {
          console.error('Error loading forms', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi tải dữ liệu',
            detail: 'Không thể kết nối đến API Gateway để tải biểu mẫu.'
          });
          this.forms.set([]);
        }
      });
  }

  onPageChange(event: any) {
    this.first.set(event.first);
    this.rows.set(event.rows);
  }

  onSearch() { }

  onAddNew() {
    this.router.navigate(['/equipment/form-builder']);
  }

  onEdit(form: EavFormTemplate) {
    this.router.navigate(['/equipment/form-builder'], { queryParams: { id: form.id } });
  }

  deactivateForm(form: EavFormTemplate) {
    this.deleteTarget.set(form);
    this.showConfirmDelete.set(true);
  }

  onCancelDelete(): void {
    // Không đóng popup khi request xóa đang được xử lý.
    if (this.deleteLoading()) return;

    this.showConfirmDelete.set(false);
    this.deleteTarget.set(null);
  }

  lockForm(form: EavFormTemplate) {
    this.targetForm = form;
    this.lockAction.set('lock');
    this.showConfirmLock.set(true);
  }

  unlockForm(form: EavFormTemplate) {
    this.targetForm = form;
    this.lockAction.set('unlock');
    this.showConfirmUnlock.set(true);
  }

  onConfirmLock() {
    if (!this.targetForm) return;
    this.loadingService.show();
    this.formTemplateService.lockTemplate(this.targetForm.id)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã khóa biểu mẫu ${this.targetForm?.name} thành công!`
          });
          this.showConfirmLock.set(false);
          this.targetForm = null;
          this.loadForms();
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể khóa biểu mẫu.'
          });
          this.showConfirmLock.set(false);
          this.targetForm = null;
        }
      });
  }

  onConfirmUnlock() {
    if (!this.targetForm) return;
    this.loadingService.show();
    this.formTemplateService.unlockTemplate(this.targetForm.id)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã mở khóa biểu mẫu ${this.targetForm?.name} thành công!`
          });
          this.showConfirmUnlock.set(false);
          this.targetForm = null;
          this.loadForms();
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể mở khóa biểu mẫu.'
          });
          this.showConfirmUnlock.set(false);
          this.targetForm = null;
        }
      });
  }

  onConfirmDelete() {
    const target = this.deleteTarget();

    // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
    if (!target || this.deleteLoading()) return;

    this.deleteLoading.set(true);
    this.formTemplateService.deleteTemplate(target.id)
      .pipe(finalize(() => this.deleteLoading.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã xóa biểu mẫu thành công!`
          });
          this.showConfirmDelete.set(false);
          this.deleteTarget.set(null);
          this.loadForms();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || err?.error?.Message || 'Không thể xóa biểu mẫu.'
          });
        }
      });
  }

  getCategoryName(code: string): string {
    const eqType = this.equipmentTypes().find(t => t.code === code || t.id === code);
    return eqType ? eqType.name : code || '';
  }

  getGridTypeName(gridTypeId?: number): string {
    if (!gridTypeId) return '';
    const gt = this.gridTypes().find(g => g.id === gridTypeId);
    return gt ? gt.name : `Loại ${gridTypeId}`;
  }

  viewDetails(form: EavFormTemplate) {
    this.router.navigate(['/equipment/form-template', form.id]);
  }

  goToList() {
    this.router.navigate(['/equipment/form-template']);
  }

  viewVersions(form: EavFormTemplate) {
    this.loadingService.show();
    this.formTemplateService.getTemplateVersions(form.code)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: (versions) => {
          this.versionList.set(versions || []);
          this.selectedTemplate.set(form);
          this.showVersionsDialog.set(true);
        },
        error: (err) => {
          console.error('Failed to load template versions', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải danh sách phiên bản của biểu mẫu.'
          });
        }
      });
  }

  viewVersionDetail(ver: EavFormTemplate) {
    this.showVersionsDialog.set(false);
    this.router.navigate(['/equipment/form-template', ver.id], {
      queryParams: { version: ver.version }
    });
  }

  confirmRestoreVersion(ver: EavFormTemplate) {
    if (ver.isActive || this.restoringVersion()) return;
    this.restoreTarget.set(ver);
    this.showConfirmRestore.set(true);
  }

  onConfirmRestoreVersion() {
    const ver = this.restoreTarget();
    const parent = this.selectedTemplate();
    if (!ver || !parent) return;

    this.restoringVersion.set(true);
    this.formTemplateService.restoreTemplateVersion(ver.id, ver.version)
      .pipe(finalize(() => this.restoringVersion.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã khôi phục biểu mẫu về phiên bản v${ver.version}.0.`
          });
          this.showConfirmRestore.set(false);
          this.restoreTarget.set(null);
          this.viewVersions(parent);
          this.loadForms();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.Message || 'Không thể khôi phục phiên bản.'
          });
        }
      });
  }

  onActivateVersion(ver: any) {
    if (!ver || !ver.id) return;
    this.loadingService.show();
    this.formTemplateService.activateTemplateVersion(ver.id)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: res.message || `Đã kích hoạt phiên bản v${ver.version}.0 thành công!`
          });
          // Reload versions list
          if (this.selectedTemplate()) {
            this.viewVersions(this.selectedTemplate()!);
          }
          this.loadForms();
        },
        error: (err) => {
          console.error('Failed to activate template version', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể kích hoạt phiên bản biểu mẫu.'
          });
        }
      });
  }

  exportExcel() {
    this.messageService.add({
      severity: 'info',
      summary: 'Xuất dữ liệu',
      detail: 'Đang chuẩn bị xuất Excel...'
    });

    const headers = [
      'STT',
      'Mã biểu mẫu',
      'Tên biểu mẫu',
      'Loại lưới điện',
      'Loại thiết bị',
      'Phiên bản',
      'Người tạo',
      'Cập nhật lần cuối',
      'Trạng thái'
    ];

    const rows = this.filteredForms().map((form, index) => [
      index + 1,
      form.code,
      form.name,
      form.gridTypeName || this.getGridTypeName(form.gridTypeId),
      this.getCategoryName(form.category),
      `v${form.version}.0`,
      form.createdBy,
      form.createdAt ? new Date(form.createdAt).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '',
      form.isActive ? 'Đang hoạt động' : 'Ngừng hoạt động'
    ]);

    import('xlsx').then(XLSX => {
      const workbook = XLSX.utils.book_new();
      const worksheetData = [
        ['Danh sách biểu mẫu'],
        [],
        headers,
        ...rows
      ];
      const worksheet = XLSX.utils.aoa_to_sheet(worksheetData);

      worksheet['!merges'] = [
        { s: { r: 0, c: 0 }, e: { r: 0, c: headers.length - 1 } }
      ];

      const borderStyle = { style: 'thin', color: { rgb: '000000' } };
      const titleCell = worksheet[XLSX.utils.encode_cell({ r: 0, c: 0 })];
      if (titleCell) {
        titleCell.s = {
          font: { bold: true, sz: 16 },
          alignment: { horizontal: 'center', vertical: 'center' },
          border: {
            top: borderStyle,
            bottom: borderStyle,
            left: borderStyle,
            right: borderStyle
          }
        };
      }

      const headerRow = 2;
      for (let col = 0; col < headers.length; col++) {
        const cell = worksheet[XLSX.utils.encode_cell({ r: headerRow, c: col })];
        if (cell) {
          cell.s = {
            font: { bold: true },
            alignment: { horizontal: 'center', vertical: 'center' },
            border: {
              top: borderStyle,
              bottom: borderStyle,
              left: borderStyle,
              right: borderStyle
            },
            fill: { fgColor: { rgb: 'FFF2F2F2' } }
          };
        }
      }

      for (let row = headerRow + 1; row < worksheetData.length; row++) {
        for (let col = 0; col < headers.length; col++) {
          const cell = worksheet[XLSX.utils.encode_cell({ r: row, c: col })];
          if (cell) {
            cell.s = {
              alignment: { horizontal: col === 0 ? 'center' : 'left', vertical: 'center' },
              border: {
                top: borderStyle,
                bottom: borderStyle,
                left: borderStyle,
                right: borderStyle
              }
            };
          }
        }
      }

      worksheet['!cols'] = [
        { wch: 6 },
        { wch: 18 },
        { wch: 40 },
        { wch: 24 },
        { wch: 24 },
        { wch: 10 },
        { wch: 20 },
        { wch: 22 },
        { wch: 18 }
      ];

      XLSX.utils.book_append_sheet(workbook, worksheet, 'Danh sách biểu mẫu');
      const workbookBlob = new Blob([XLSX.write(workbook, { bookType: 'xlsx', type: 'array' })], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      });

      const url = URL.createObjectURL(workbookBlob);
      const link = document.createElement('a');
      link.setAttribute('href', url);
      link.setAttribute('download', `DanhSachBieuMau_${new Date().getTime()}.xlsx`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      this.messageService.add({
        severity: 'success',
        summary: 'Thành công',
        detail: 'Đã xuất và tải về danh sách biểu mẫu thành công!'
      });
    });
  }
}
