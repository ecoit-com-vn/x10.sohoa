import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
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
import { finalize } from 'rxjs';
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

  // Preview properties
  fields = signal<FormField[]>([]);
  simulatedValues = signal<{ [key: string]: any }>({});

  // Pagination
  first = signal<number>(0);
  rows = signal<number>(10);

  categories = [
    { code: 'MAY_BIEN_AP', name: 'Máy biến áp' },
    { code: 'MAY_CAT', name: 'Máy cắt' },
    { code: 'DAO_CACH_LY', name: 'Dao cách ly' },
    { code: 'BIEN_DIEN_AP', name: 'Biến điện áp (TU)' },
    { code: 'BIEN_DONG_DIEN', name: 'Biến dòng điện (TI)' },
    { code: 'CAP_DIEN_LUC', name: 'Cáp điện lực' },
    { code: 'TU_TRUNG_THE', name: 'Tủ trung thế' },
    { code: 'THIET_BI_DO_LUONG', name: 'Thiết bị đo lường' },
    { code: 'KHAC', name: 'Hạng mục khác' }
  ];

  private eavFormService = inject(EavFormService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);

  canApprove = computed(() => canApproveForm(this.authService));

  constructor() {
    effect(() => {
      this.searchKeyword();
      this.activeTab();
      this.first.set(0);
    });
  }



  ngOnInit() {
    this.authService.loadPermissions();
    this.loadForms();
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
    this.selectedForm.set(form);
    this.viewState.set('preview');
    
    try {
      const parsedFields = JSON.parse(form.formSchema) || [];
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
    this.viewState.set('list');
    this.selectedForm.set(null);
    this.fields.set([]);
    this.simulatedValues.set({});
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

  getCategoryName(code: string): string {
    const cat = this.categories.find(c => c.code === code);
    return cat ? cat.name : code || '(Chưa chọn)';
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
