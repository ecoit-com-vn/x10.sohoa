import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
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
import { EquipmentTypeService } from '../../data-access/equipment-type.service';

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
    private equipmentTypeService = inject(EquipmentTypeService);
    private router = inject(Router);

    showConfirmLock = signal<boolean>(false);
    showConfirmUnlock = signal<boolean>(false);
    showConfirmDelete = signal<boolean>(false);
    viewState = signal<'list' | 'detail'>('list');
    targetForm: EavFormTemplate | null = null;
    selectedForm: EavFormTemplate | null = null;

    equipmentTypes = signal<any[]>([]);
    gridTypes = signal<any[]>([]);
    categories = signal<any[]>([]);

    forms = signal<EavFormTemplate[]>([]);
    searchKeyword = signal<string>('');
    loading = signal<boolean>(false);

    first = signal<number>(0);
    rows = signal<number>(10);

    filteredForms = computed(() => {
        const keyword = this.searchKeyword().trim().toLowerCase();
        const allForms = this.forms().filter(form => !form.isDeleted && form.formType === 'FORM');

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

        return list
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
        this.loadEquipmentTypes();
        this.loadGridTypes();
        this.loadHmadCategories();
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
                        this.targetForm.isActive = false;
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
                        this.targetForm.isActive = true;
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
        return !form.isActive;
    }

    getFieldType(field: any): string {
        const type = (field?.type || 'text').toString().toLowerCase();
        if (type === 'dropdown' || type === 'select') {
            return 'dropdown';
        }
        if (type === 'textarea') {
            return 'textarea';
        }
        if (type === 'checkbox') {
            return 'checkbox';
        }
        if (type === 'radio') {
            return 'radio';
        }
        if (type === 'date') {
            return 'date';
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
        this.viewState.set('detail');
    }

    goToList() {
        this.viewState.set('list');
        this.selectedForm = null;
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

    loadHmadCategories() {
        this.eavFormService.getCatalogTypeByCode('HMAD').subscribe({
            next: (catalogType) => {
                if (catalogType && catalogType.id) {
                    this.eavFormService.getCatalogsLookup(catalogType.id).subscribe({
                        next: (catalogs) => {
                            this.categories.set(catalogs || []);
                        },
                        error: (err) => {
                            console.error('Failed to load catalogs for HMAD', err);
                        }
                    });
                }
            },
            error: (err) => {
                console.error('Failed to load CatalogType HMAD', err);
            }
        });
    }

    getCategoryName(code: string): string {
        const cat = this.categories().find(c => c.code === code || c.id === code || c.id?.toString() === code?.toString());
        return cat ? cat.name : code || '';
    }

    getGridTypeName(gridTypeId?: number): string {
        if (!gridTypeId) return '';
        const gt = this.gridTypes().find(g => g.id === gridTypeId);
        return gt ? gt.name : `Loại ${gridTypeId}`;
    }

    onEdit(form: EavFormTemplate) {
        this.router.navigate(['/equipment/completed-forms/edit'], { queryParams: { id: form.id } });
    }

    deactivateForm(form: EavFormTemplate) {
        this.targetForm = form;
        this.showConfirmDelete.set(true);
    }

    onConfirmDelete() {
        if (!this.targetForm) return;
        this.loadingService.show();
        this.eavFormService.deleteTemplate(this.targetForm.id)
            .pipe(finalize(() => this.loadingService.hide()))
            .subscribe({
                next: () => {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Thành công',
                        detail: 'Đã xóa form thành công!'
                    });
                    this.showConfirmDelete.set(false);
                    this.targetForm = null;
                    this.loadForms();
                },
                error: () => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Lỗi',
                        detail: 'Không thể xóa form.'
                    });
                    this.showConfirmDelete.set(false);
                    this.targetForm = null;
                }
            });
    }
}
