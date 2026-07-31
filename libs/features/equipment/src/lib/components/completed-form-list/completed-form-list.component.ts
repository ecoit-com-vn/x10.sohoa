import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import {
    DeleteConfirmDialogComponent,
    EcoPaginatorComponent,
    WfBreadcrumbComponent
} from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ToastModule } from 'primeng/toast';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { PaginatorModule } from 'primeng/paginator';
import { DialogModule } from 'primeng/dialog';
import { combineLatest, finalize } from 'rxjs';
import { LoadingService, EavFormService, EavFormTemplate, AuthService } from '@sohoa.frontend/shared/core';
import {
  canDeleteCompletedForm,
  canEditForm,
  canManageCompletedForm,
} from '../../utils/eav-form-permission.util';

@Component({
    selector: 'app-completed-form-list',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ToastModule,
        MenuModule,
        ButtonModule,
        InputTextModule,
        TextareaModule,
        SelectModule,
        PaginatorModule,
        EcoPaginatorComponent,
        DialogModule,
        WfBreadcrumbComponent,
        DeleteConfirmDialogComponent,
    ],
    providers: [MessageService],
    templateUrl: './completed-form-list.component.html',
    styleUrl: './completed-form-list.component.scss'
})
export class CompletedFormListComponent implements OnInit {
    private messageService = inject(MessageService);
    private loadingService = inject(LoadingService);
    private eavFormService = inject(EavFormService);
    private router = inject(Router);
    private route = inject(ActivatedRoute);
    private authService = inject(AuthService);

    canEdit = computed(() => canEditForm(this.authService));
    canManage = computed(() => canManageCompletedForm(this.authService));
    canDelete = computed(() => canDeleteCompletedForm(this.authService));

    showConfirmLock = signal<boolean>(false);
    showConfirmUnlock = signal<boolean>(false);
    showConfirmDelete = signal<boolean>(false);
    deleteTarget = signal<EavFormTemplate | null>(null);
    deleteLoading = signal<boolean>(false);
    // Tách target xóa khỏi state khóa/mở khóa và chuẩn hóa tên cho popup dùng chung.
    readonly deleteTargetLabel = computed(() => this.deleteTarget()?.name ?? '');
    viewState = signal<'list' | 'detail'>('list');
    targetForm: EavFormTemplate | null = null;
    selectedForm = signal<EavFormTemplate | null>(null);

    showVersionsDialog = signal<boolean>(false);
    versionList = signal<EavFormTemplate[]>([]);
    selectedTemplate = signal<EavFormTemplate | null>(null);
    showConfirmRestore = signal<boolean>(false);
    restoreTarget = signal<EavFormTemplate | null>(null);
    restoringVersion = signal<boolean>(false);

    catalogOptionsMap = signal<{ [catalogCode: string]: string[] }>({});
    actionMenuItems: MenuItem[] = [];

    openActionMenu(form: EavFormTemplate, event: Event, menu: Menu): void {
        event.stopPropagation();
        this.actionMenuItems = [
            { label: 'Xem chi tiết', title: 'Xem chi tiết', icon: 'pi pi-eye color-teal', command: () => this.viewFormDetail(form) },
            { label: 'Lịch sử phiên bản', title: 'Lịch sử phiên bản', icon: 'pi pi-history color-blue', command: () => this.viewVersions(form) },
            ...(this.canEdit() ? [{ label: 'Chỉnh sửa', title: 'Chỉnh sửa', icon: 'pi pi-pencil color-blue', command: () => this.onEdit(form) }] : []),
            ...(this.canManage() && !this.isFormLocked(form) ? [{ label: 'Khóa form', title: 'Khóa form', icon: 'pi pi-lock color-red', command: () => this.lockForm(form) }] : []),
            ...(this.canManage() && this.isFormLocked(form) ? [{ label: 'Mở khóa form', title: 'Mở khóa form', icon: 'pi pi-lock-open color-teal', command: () => this.unlockForm(form) }] : []),
            ...(this.canDelete() ? [{ label: 'Xóa', title: 'Xóa', icon: 'pi pi-trash color-red', command: () => this.deactivateForm(form) }] : []),
        ];
        menu.toggle(event);
    }

    loadCatalogOptions(catalogCode: string) {
        if (!catalogCode || this.catalogOptionsMap()[catalogCode]) return;
        this.eavFormService.getCompletedCatalogOptions(catalogCode).subscribe({
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

    loadCatalogOptionsEffect = effect(() => {
        const currentFields = this.formFields();
        if (!currentFields) return;
        currentFields.forEach((f: any) => {
            if (f.dataSourceType === 'catalog' && f.catalogType) {
                this.loadCatalogOptions(f.catalogType);
            }
        });
    });

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
        const form = this.selectedForm();
        if (!form?.formSchema) {
            return [];
        }
        return this.parseFormSchema(form.formSchema);
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

    constructor() {
        combineLatest([this.route.paramMap, this.route.queryParamMap])
            .pipe(takeUntilDestroyed())
            .subscribe(([params, query]) => {
                const id = params.get('id');
                if (!id) {
                    this.viewState.set('list');
                    this.selectedForm.set(null);
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
        this.loadingService.show();
        const request$ = version != null
            ? this.eavFormService.getCompletedTemplateByIdAndVersion(id, version)
            : this.eavFormService.getCompletedTemplateById(id);

        request$
            .pipe(finalize(() => this.loadingService.hide()))
            .subscribe({
                next: (detail) => {
                    this.selectedForm.set(detail);
                    this.viewState.set('detail');
                },
                error: (err) => {
                    console.error('Failed to load form detail', err);
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Lỗi',
                        detail: 'Không thể tải chi tiết form.'
                    });
                    this.router.navigate(['/equipment/completed-forms']);
                }
            });
    }

    loadForms(keyword?: string) {
        this.loading.set(true);
        this.eavFormService.getCompletedTemplatesForm()
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
        this.eavFormService.lockCompletedTemplate(this.targetForm.id)
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
        this.eavFormService.unlockCompletedTemplate(this.targetForm.id)
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
        this.router.navigate(['/equipment/completed-forms', form.id]);
    }

    goToList() {
        this.router.navigate(['/equipment/completed-forms']);
    }

    getCategoryName(form: EavFormTemplate): string {
        return form.categoryName || form.category || '';
    }

    onEdit(form: EavFormTemplate) {
        this.router.navigate(['/equipment/completed-forms', form.id, 'edit']);
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

    onConfirmDelete() {
        const target = this.deleteTarget();

        // Chặn target không hợp lệ hoặc request xóa bị gửi trùng.
        if (!target || this.deleteLoading()) return;

        this.deleteLoading.set(true);
        this.eavFormService.deleteCompletedTemplate(target.id)
            .pipe(finalize(() => this.deleteLoading.set(false)))
            .subscribe({
                next: () => {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Thành công',
                        detail: 'Đã xóa form thành công!'
                    });
                    this.showConfirmDelete.set(false);
                    this.deleteTarget.set(null);
                    this.loadForms();
                },
                error: (err) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Lỗi',
                        detail: err?.error?.message || err?.error?.Message || 'Không thể xóa form.'
                    });
                }
            });
    }

    viewVersions(form: EavFormTemplate) {
        this.loadingService.show();
        this.eavFormService.getCompletedTemplateVersions(form.code)
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
        this.router.navigate(['/equipment/completed-forms', ver.id], {
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
        this.eavFormService.restoreCompletedTemplateVersion(ver.id, ver.version)
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
}
