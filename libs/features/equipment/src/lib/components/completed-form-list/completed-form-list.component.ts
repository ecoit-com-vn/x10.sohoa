import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { PaginatorModule } from 'primeng/paginator';
import { DialogModule } from 'primeng/dialog';
import { finalize } from 'rxjs';
import { LoadingService, EavFormService, EavFormTemplate } from '@sohoa.frontend/shared/core';

@Component({
    selector: 'app-completed-form-list',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ToastModule,
        ButtonModule,
        InputTextModule,
        TextareaModule,
        SelectModule,
        PaginatorModule,
        DialogModule
    ],
    providers: [MessageService],
    templateUrl: './completed-form-list.component.html',
    styleUrl: './completed-form-list.component.scss'
})
export class CompletedFormListComponent implements OnInit {
    private messageService = inject(MessageService);
    private loadingService = inject(LoadingService);
    private eavFormService = inject(EavFormService);

    showConfirmLock = signal<boolean>(false);
    showConfirmUnlock = signal<boolean>(false);
    showFormDetail = signal<boolean>(false);
    targetForm: EavFormTemplate | null = null;
    selectedForm: EavFormTemplate | null = null;

    forms = signal<EavFormTemplate[]>([]);
    searchKeyword = signal<string>('');
    loading = signal<boolean>(false);

    first = signal<number>(0);
    rows = signal<number>(10);

    filteredForms = computed(() => {
        const keyword = this.searchKeyword().trim().toLowerCase();
        return this.forms()
            .filter(form => form.status === 'Hoàn thành')
            .filter(form => {
                if (!keyword) {
                    return true;
                }
                return (
                    (form.name?.toLowerCase().includes(keyword) ?? false) ||
                    (form.code?.toLowerCase().includes(keyword) ?? false) ||
                    (form.id?.toLowerCase().includes(keyword) ?? false)
                );
            });
    });

    paginatedForms = computed(() => {
        const start = this.first();
        const end = start + this.rows();
        return this.filteredForms().slice(start, end);
    });

    formFields = computed(() => {
        if (!this.selectedForm?.formSchema) {
            return [];
        }
        return this.parseFormSchema(this.selectedForm.formSchema);
    });

    private parseFormSchema(schema: unknown): any[] {
        if (!schema) {
            return [];
        }

        if (Array.isArray(schema)) {
            return schema;
        }

        if (typeof schema === 'string') {
            const trimmed = schema.trim();
            if (!trimmed) {
                return [];
            }

            try {
                return this.parseFormSchema(JSON.parse(trimmed));
            } catch (error) {
                console.error('Error parsing form schema string', error);
                return [];
            }
        }

        if (typeof schema === 'object') {
            const record = schema as Record<string, unknown>;
            if (Array.isArray(record['fields'])) {
                return record['fields'] as any[];
            }
        }

        return [];
    }

    ngOnInit() {
        this.loadForms();
    }

    loadForms(keyword?: string) {
        this.loading.set(true);
        this.eavFormService.getTemplates()
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: (data) => {
                    this.forms.set(data || []);
                },
                error: (err) => {
                    console.error('Error loading completed forms', err);
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Lỗi tải dữ liệu',
                        detail: 'Không thể tải danh sách biểu mẫu hoàn thành.'
                    });
                    this.forms.set([]);
                }
            });
    }

    onSearch() {
        this.first.set(0);
        this.loadForms(this.searchKeyword());
    }

    onPageChange(event: any) {
        this.first.set(event.first);
        this.rows.set(event.rows);
    }

    lockForm(form: EavFormTemplate) {
        this.targetForm = form;
        this.showConfirmLock.set(true);
    }

    unlockForm(form: EavFormTemplate) {
        this.targetForm = form;
        this.showConfirmUnlock.set(true);
    }

    onConfirmLock() {
        if (!this.targetForm) {
            return;
        }
        this.loadingService.show();
        this.eavFormService.lockTemplate(this.targetForm.id)
            .pipe(finalize(() => this.loadingService.hide()))
            .subscribe({
                next: () => {
                    if (this.targetForm) {
                        this.targetForm.isLocked = true;
                    }
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Thành công',
                        detail: `Đã khóa biểu mẫu ${this.targetForm?.name} thành công.`
                    });
                    this.showConfirmLock.set(false);
                    this.targetForm = null;
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
        if (!this.targetForm) {
            return;
        }
        this.loadingService.show();
        this.eavFormService.unlockTemplate(this.targetForm.id)
            .pipe(finalize(() => this.loadingService.hide()))
            .subscribe({
                next: () => {
                    if (this.targetForm) {
                        this.targetForm.isLocked = false;
                    }
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Thành công',
                        detail: `Đã mở khóa biểu mẫu ${this.targetForm?.name} thành công.`
                    });
                    this.showConfirmUnlock.set(false);
                    this.targetForm = null;
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

    isFormLocked(form: EavFormTemplate): boolean {
        return form.isLocked === true;
    }

    getFieldType(field: any): string {
        const type = (field?.type || 'text').toString().toLowerCase();
        if (type === 'dropdown') {
            return 'dropdown';
        }
        if (type === 'textarea') {
            return 'textarea';
        }
        if (type === 'checkbox') {
            return 'checkbox';
        }
        if (type === 'number') {
            return 'number';
        }
        return 'text';
    }

    getDropdownOptions(field: any): string[] {
        if (Array.isArray(field?.options)) {
            return field.options;
        }
        return ['Option 1', 'Option 2'];
    }

    viewFormDetail(form: EavFormTemplate) {
        this.selectedForm = form;
        this.showFormDetail.set(true);
    }
}
