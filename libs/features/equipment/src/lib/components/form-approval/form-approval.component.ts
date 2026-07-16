import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { CardModule } from 'primeng/card';
import { TextareaModule } from 'primeng/textarea';
import { Paginator } from 'primeng/paginator';
import { EavFormService, EavFormTemplate, AuthService } from '@sohoa.frontend/shared/core';
import { combineLatest, finalize } from 'rxjs';
import { Dialog } from 'primeng/dialog';
import { canApproveForm } from '../../utils/eav-form-permission.util';

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
  selector: 'app-form-approval',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    ToastModule, 
    ButtonModule,
    InputTextModule,
    Select,
    CheckboxModule,
    CardModule,
    TextareaModule,
    Paginator,
    Dialog,
    WfBreadcrumbComponent,
  ],
  providers: [MessageService],
  templateUrl: './form-approval.component.html',
  styleUrl: './form-approval.component.scss'
})
export class FormApprovalComponent implements OnInit {
  // Confirm dialog state variables
  showConfirmApprove = signal<boolean>(false);
  showConfirmReject = signal<boolean>(false);
  targetForm: EavFormTemplate | null = null;

  viewState = signal<'list' | 'preview'>('list');
  loading = signal<boolean>(false);
  forms = signal<EavFormTemplate[]>([]);
  pendingCount = computed(() => this.forms().filter(f => f.status === 'Chờ duyệt').length);
  searchKeyword = signal<string>('');
  activeTab = signal<'pending' | 'history'>('pending');
  selectedForm = signal<EavFormTemplate | null>(null);
  showVersionsDialog = signal<boolean>(false);
  versionList = signal<EavFormTemplate[]>([]);
  selectedTemplateForVersions = signal<EavFormTemplate | null>(null);
  showConfirmRestore = signal<boolean>(false);
  restoreTarget = signal<EavFormTemplate | null>(null);
  restoringVersion = signal<boolean>(false);

  // Preview properties
  fields = signal<FormField[]>([]);
  simulatedValues = signal<{ [key: string]: any }>({});

  // Pagination
  first = signal<number>(0);
  rows = signal<number>(10);

  private eavFormService = inject(EavFormService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  canApprove = computed(() => canApproveForm(this.authService));

  catalogOptionsMap = signal<{ [catalogCode: string]: string[] }>({});

  loadCatalogOptions(catalogCode: string) {
    if (!catalogCode || this.catalogOptionsMap()[catalogCode]) return;
    this.eavFormService.getApprovalCatalogOptions(catalogCode).subscribe({
      next: (items) => {
        const options = (items || []).map((item) => item.name || item.code);
        this.catalogOptionsMap.update(prev => ({
          ...prev,
          [catalogCode]: options
        }));
      },
      error: (err) => console.error(`Failed to load catalogs lookup for ${catalogCode}`, err)
    });
  }

  constructor() {
    effect(() => {
      this.searchKeyword();
      this.activeTab();
      this.first.set(0);
    });

    effect(() => {
      const currentFields = this.fields();
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
          this.selectedForm.set(null);
          this.fields.set([]);
          this.simulatedValues.set({});
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
    this.authService.loadPermissions();
  }

  private loadDetail(id: string, version: number | null) {
    this.loading.set(true);
    const request$ = version != null
      ? this.eavFormService.getApprovalTemplateByIdAndVersion(id, version)
      : this.eavFormService.getApprovalTemplateById(id);

    request$
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (detail) => this.applyPreview(detail),
        error: (err) => {
          console.error('Failed to load form detail', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải chi tiết form.'
          });
          this.router.navigate(['/equipment/form-approval']);
        }
      });
  }

  loadForms() {
    this.loading.set(true);
    this.eavFormService.getApprovalTemplatesForm()
      .pipe(finalize(() => this.loading.set(false)))
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

  filteredForms = computed(() => {
    const keyword = this.searchKeyword().trim().toLowerCase();
    const tab = this.activeTab();
    const allForms = this.forms().filter(f => !f.isDeleted);

    // Group forms by code and select the latest version for each unique code
    const latestFormsMap = new Map<string, EavFormTemplate>();
    for (const f of allForms) {
      const code = f.code || '';
      const existing = latestFormsMap.get(code);
      if (!existing || f.version > existing.version) {
        latestFormsMap.set(code, f);
      }
    }
    let list = Array.from(latestFormsMap.values());

    // Filter by tab
    if (tab === 'pending') {
      list = list.filter(f => f.status === 'Chờ duyệt');
    } else {
      list = list.filter(f => f.status === 'Hoàn thành' || f.status === 'Từ chối');
    }

    // Filter by keyword
    if (keyword) {
      list = list.filter(f =>
        (f.name?.toLowerCase().includes(keyword) ?? false) ||
        (f.code?.toLowerCase().includes(keyword) ?? false) ||
        (f.id?.toLowerCase().includes(keyword) ?? false)
      );
    }
    return list;
  });

  paginatedForms = computed(() => {
    const start = this.first();
    const end = start + this.rows();
    return this.filteredForms().slice(start, end);
  });

  onPageChange(event: any) {
    this.first.set(event.first);
    this.rows.set(event.rows);
  }

  onSearch() {
    // Handled reactively by computed filteredForms
  }

  switchTab(tab: 'pending' | 'history') {
    this.activeTab.set(tab);
  }

  onPreview(form: EavFormTemplate) {
    this.router.navigate(['/equipment/form-approval', form.id]);
  }

  private applyPreview(form: EavFormTemplate) {
    this.selectedForm.set(form);
    this.viewState.set('preview');

    try {
      const parsedFields = JSON.parse(form.formSchema || '[]') || [];
      this.fields.set(parsedFields);

      const initialSimulated: { [key: string]: any } = {};
      parsedFields.forEach((f: FormField) => {
        if (f.type === 'checkbox') {
          initialSimulated[f.name] = false;
        } else {
          initialSimulated[f.name] = '';
        }
      });
      this.simulatedValues.set(initialSimulated);
    } catch (e) {
      console.error('Failed to parse form schema', e);
      this.fields.set([]);
      this.simulatedValues.set({});
    }
  }

  goToList() {
    this.router.navigate(['/equipment/form-approval']);
  }

  viewVersions(form: EavFormTemplate) {
    this.loading.set(true);
    this.eavFormService.getApprovalTemplateVersions(form.code)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (versions) => {
          this.versionList.set(versions || []);
          this.selectedTemplateForVersions.set(form);
          this.showVersionsDialog.set(true);
        },
        error: (err) => {
          console.error('Failed to load template versions', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải danh sách phiên bản của form.'
          });
        }
      });
  }

  viewVersionDetail(ver: EavFormTemplate) {
    this.showVersionsDialog.set(false);
    this.router.navigate(['/equipment/form-approval', ver.id], {
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
    const parent = this.selectedTemplateForVersions();
    if (!ver || !parent) return;

    this.restoringVersion.set(true);
    this.eavFormService.restoreApprovalTemplateVersion(ver.id, ver.version)
      .pipe(finalize(() => this.restoringVersion.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã khôi phục form về phiên bản v${ver.version}.0.`
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

  approveForm(form: EavFormTemplate) {
    this.targetForm = form;
    this.showConfirmApprove.set(true);
  }

  rejectForm(form: EavFormTemplate) {
    this.targetForm = form;
    this.showConfirmReject.set(true);
  }

  onConfirmApprove() {
    if (!this.targetForm) return;
    const form = this.targetForm;
    this.loading.set(true);
    this.eavFormService.approveTemplate(form.id)
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã duyệt biểu mẫu "${form.name}" thành công!`
          });
          this.showConfirmApprove.set(false);
          this.targetForm = null;
          this.loadForms();
          this.goToList();
        },
        error: (err) => {
          this.loading.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.Message || 'Không thể phê duyệt biểu mẫu.'
          });
          this.showConfirmApprove.set(false);
          this.targetForm = null;
        }
      });
  }

  onConfirmReject() {
    if (!this.targetForm) return;
    const form = this.targetForm;
    this.loading.set(true);
    this.eavFormService.rejectTemplate(form.id)
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Từ chối thành công',
            detail: `Đã từ chối biểu mẫu "${form.name}".`
          });
          this.showConfirmReject.set(false);
          this.targetForm = null;
          this.loadForms();
          this.goToList();
        },
        error: (err) => {
          this.loading.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.Message || 'Không thể từ chối biểu mẫu.'
          });
          this.showConfirmReject.set(false);
          this.targetForm = null;
        }
      });
  }

  getCategoryName(form: EavFormTemplate): string {
    return form.categoryName || form.category || '(Chưa chọn)';
  }

  updateSimulatedValue(name: string, value: any) {
    this.simulatedValues.update(prev => ({
      ...prev,
      [name]: value
    }));
  }

  isAllChecked(field: any): boolean {
    if (!field.options || field.options.length === 0) return false;
    return field.options.every((opt: string) => this.simulatedValues()[field.name + '_' + opt] === true);
  }

  toggleSelectAll(field: any, checked: boolean) {
    if (!field.options) return;
    field.options.forEach((opt: string) => {
      this.updateSimulatedValue(field.name + '_' + opt, checked);
    });
  }

  onSimulateSubmit() {
    const missingFields: string[] = [];
    const vals = this.simulatedValues();
    this.fields().forEach(f => {
      if (f.required) {
        const val = vals[f.name];
        if (val === undefined || val === null || val === '') {
          missingFields.push(f.label);
        }
      }
    });

    if (missingFields.length > 0) {
      this.messageService.add({
        severity: 'error',
        summary: 'Kiểm nghiệm lỗi',
        detail: `Vui lòng điền các trường bắt buộc: ${missingFields.join(', ')}`
      });
    } else {
      this.messageService.add({
        severity: 'success',
        summary: 'Kiểm nghiệm thành công',
        detail: 'Cơ cấu dữ liệu mô phỏng hoàn toàn chính xác!'
      });
    }
  }
}
