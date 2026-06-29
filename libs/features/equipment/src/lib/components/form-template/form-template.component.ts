import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { CardModule } from 'primeng/card';
import { TextareaModule } from 'primeng/textarea';
import { Paginator } from 'primeng/paginator';
import { Dialog } from 'primeng/dialog';
import { Router } from '@angular/router';
import { FormTemplateService, EavFormTemplate } from '../../data-access/form-template.service';
import { EquipmentTypeService } from '../../data-access/equipment-type.service';
import { finalize } from 'rxjs';
import { LoadingService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-form-template',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    ButtonModule,
    InputTextModule,
    CheckboxModule,
    CardModule,
    TextareaModule,
    Paginator,
    Dialog
  ],
  providers: [MessageService],
  templateUrl: './form-template.component.html',
  styleUrl: './form-template.component.scss'
})
export class FormTemplateComponent implements OnInit {
  private loadingService = inject(LoadingService);
  private router = inject(Router);
  private formTemplateService = inject(FormTemplateService);
  private equipmentTypeService = inject(EquipmentTypeService);
  private messageService = inject(MessageService);

  showConfirmDelete = signal<boolean>(false);
  showConfirmLock = signal<boolean>(false);
  showConfirmUnlock = signal<boolean>(false);
  targetForm: EavFormTemplate | null = null;
  lockAction = signal<'lock' | 'unlock' | null>(null);

  forms = signal<EavFormTemplate[]>([]);
  searchKeyword = signal<string>('');
  loading = signal<boolean>(false);

  first = signal<number>(0);
  rows = signal<number>(10);

  equipmentTypes = signal<any[]>([]);

  gridTypes = signal<any[]>([]);

  filteredForms = computed(() => {
    const keyword = this.searchKeyword().trim().toLowerCase();
    const allForms = this.forms();
    if (!keyword) {
      return allForms;
    }
    return allForms.filter(f =>
      (f.name?.toLowerCase().includes(keyword) ?? false) ||
      (f.code?.toLowerCase().includes(keyword) ?? false) ||
      (f.id?.toLowerCase().includes(keyword) ?? false)
    );
  });

  paginatedForms = computed(() => {
    const start = this.first();
    const end = start + this.rows();
    return this.filteredForms().slice(start, end);
  });

  constructor() {
    effect(() => {
      this.searchKeyword();
      this.first.set(0);
    });
  }

  ngOnInit() {
    this.loadEquipmentTypes();
    this.loadGridTypes();
    this.loadForms();
  }



  loadEquipmentTypes() {
    this.equipmentTypeService.getEquipmentTypes(1, 1000, undefined, undefined, undefined, true).subscribe({
      next: (res) => {
        if (res && res.items) {
          this.equipmentTypes.set(res.items);
        }
      },
      error: (err) => {
        console.error('Failed to load equipment types', err);
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
    this.targetForm = form;
    this.showConfirmDelete.set(true);
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
    if (!this.targetForm) return;
    this.loadingService.show();
    this.formTemplateService.deleteTemplate(this.targetForm.id)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã vô hiệu hóa biểu mẫu thành công!`
          });
          this.showConfirmDelete.set(false);
          this.targetForm = null;
          this.loadForms();
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể vô hiệu hóa biểu mẫu.'
          });
          this.showConfirmDelete.set(false);
          this.targetForm = null;
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
}
