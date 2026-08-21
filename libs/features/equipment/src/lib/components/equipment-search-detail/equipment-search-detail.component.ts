import { Component, OnInit, signal, effect, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { WfBreadcrumbComponent, EcoPaginatorComponent } from '@sohoa.frontend/shared/layout';
import { EquipmentService } from '../../data-access/equipment.service';
import { DossierManagementService } from '@sohoa.frontend/features/dossier-management';
import { EquipmentDocumentsComponent } from '../equipment-documents/equipment-documents.component';

/**
 * Màn xem chi tiết thiết bị mở từ phân hệ Tra cứu (Trạm/Đường dây, Hồ sơ theo thiết bị).
 * Tách riêng khỏi EquipmentComponent (màn quản lý thiết bị chính thức, có CRUD) để tránh
 * lẫn lộn quyền chỉnh sửa và lỗi hiển thị do dùng chung 1 component cho 2 ngữ cảnh khác nhau.
 * Component này CHỈ ĐỌC - không có bất kỳ hành động sửa/xóa/chuyển thiết bị nào.
 */
@Component({
  selector: 'app-equipment-search-detail',
  standalone: true,
  imports: [
    CommonModule,
    ToastModule,
    WfBreadcrumbComponent,
    EcoPaginatorComponent,
    EquipmentDocumentsComponent
  ],
  providers: [MessageService],
  templateUrl: './equipment-search-detail.component.html',
  styleUrls: ['../equipment/equipment.component.css']
})
export class EquipmentSearchDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private equipmentService = inject(EquipmentService);
  private dossierService = inject(DossierManagementService);
  private messageService = inject(MessageService);

  activeTab = signal<'info' | 'profileDocs' | 'related'>('info');

  isLoadingDetail = signal<boolean>(true);
  currentItem = signal<any>({});
  eavTemplate = signal<any>(null);
  eavFields = signal<any[]>([]);
  formValuesObj = signal<any>({});

  // Hồ sơ liên quan
  dossierItems = signal<any[]>([]);
  dossierTotalCount = signal<number>(0);
  dossierPage = signal<number>(1);
  dossierPageSize = signal<number>(10);
  dossierColumns = signal<any[]>([]);
  isLoadingDossiers = signal<boolean>(false);

  private searchContext: 'substation' | 'dossier' = 'dossier';
  private substationId: string | null = null;
  private dossierId: string | null = null;
  private equipmentId: string | null = null;

  constructor() {
    // Tải hồ sơ liên quan khi mở tab hoặc đổi trang, chỉ khi đã có thiết bị.
    effect(() => {
      const tab = this.activeTab();
      this.dossierPage();
      this.dossierPageSize();
      const item = this.currentItem();
      if (tab === 'related' && item?.id) {
        this.loadDossiers();
      }
    });
  }

  ngOnInit(): void {
    this.substationId = this.route.snapshot.paramMap.get('substationId');
    this.dossierId = this.route.snapshot.paramMap.get('dossierId');
    this.equipmentId = this.route.snapshot.paramMap.get('id');
    this.searchContext = this.route.snapshot.data['searchContext'] === 'substation' ? 'substation' : 'dossier';

    if (!this.equipmentId) {
      this.isLoadingDetail.set(false);
      return;
    }

    this.loadDetail(this.equipmentId);
  }

  breadcrumbItems(): { label: string; url?: string }[] {
    if (this.searchContext === 'substation') {
      return [
        { label: 'Tra cứu tìm kiếm' },
        { label: 'Tra cứu tìm kiếm Trạm biến áp', url: '/search/substation' },
        {
          label: 'Chi tiết',
          url: this.substationId ? `/search/substation/${this.substationId}` : '/search/substation'
        },
        { label: 'Thiết bị' }
      ];
    }

    return [
      { label: 'Tra cứu tìm kiếm' },
      { label: 'Tra cứu hồ sơ theo thiết bị', url: '/search/dossier-by-equipment' },
      {
        label: 'Chi tiết',
        url: this.dossierId ? `/search/dossier-by-equipment/${this.dossierId}` : '/search/dossier-by-equipment'
      },
      { label: 'Thiết bị' }
    ];
  }

  private loadDetail(id: string): void {
    this.isLoadingDetail.set(true);

    const request = this.searchContext === 'substation'
      ? this.equipmentService.getSubstationSearchById(id)
      : this.equipmentService.getDossierEquipmentSearchById(this.dossierId as string, id);

    request.subscribe({
      next: (res) => {
        if (res) {
          this.currentItem.set({
            id: res.id,
            equipmentTypeId: res.equipmentTypeId,
            name: res.name,
            code: res.code,
            unitId: res.unitId,
            infrastructureId: res.infrastructureId,
            manufactureYear: res.manufactureYear,
            equipmentStatusId: res.equipmentStatusId,
            gridTypeId: res.gridTypeId,
            isActive: res.isActive === 1 || res.isActive === true,
            formValues: res.formValues,
            equipmentTypeName: res.equipmentTypeName,
            equipmentTypeCode: res.equipmentTypeCode,
            gridTypeName: res.gridTypeName,
            infrastructureName: res.infrastructureName,
            unitName: res.unitName,
            equipmentStatusName: res.equipmentStatusName,
            creator: res.creator,
            createdBy: res.createdBy
          });

          let parsedFields: any[] = [];
          if (res.formSchema) {
            this.eavTemplate.set({ name: res.formTemplateName, formSchema: res.formSchema });
            try {
              parsedFields = JSON.parse(res.formSchema) || [];
            } catch {
              parsedFields = [];
            }
          } else {
            this.eavTemplate.set(null);
          }
          this.eavFields.set(parsedFields);

          try {
            const parsed = res.formValues ? JSON.parse(res.formValues) : {};
            this.formValuesObj.set(this.initEavFormValues(parsedFields, parsed));
          } catch {
            this.formValuesObj.set(this.initEavFormValues(parsedFields, {}));
          }
        }
        this.isLoadingDetail.set(false);
      },
      error: () => {
        this.isLoadingDetail.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải chi tiết thiết bị'
        });
        this.goBack();
      }
    });
  }

  reloadDetail(id: string | null | undefined): void {
    if (!id) return;
    this.loadDetail(id);
  }

  loadDossiers(): void {
    const item = this.currentItem();
    if (!item?.id) return;

    this.isLoadingDossiers.set(true);
    this.dossierService.getDossiersByEquipment(item.id, this.dossierPage(), this.dossierPageSize()).subscribe({
      next: (res) => {
        if (res) {
          this.dossierItems.set(res.items || []);
          this.dossierTotalCount.set(res.totalCount || 0);
          this.dossierColumns.set(res.columns || []);
        }
        this.isLoadingDossiers.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể tải danh sách hồ sơ liên quan'
        });
        this.isLoadingDossiers.set(false);
      }
    });
  }

  onRelatedDossierPageChange(event: { page: number; rows: number }): void {
    this.dossierPage.set(event.page + 1);
    this.dossierPageSize.set(event.rows);
  }

  getDossierCatalogValue(item: any, column: any): string {
    const columnCode = String(column?.code ?? '').trim().toUpperCase();
    if (columnCode === 'CODE') {
      const dossierCode = item?.dossierCode ?? item?.DossierCode;
      if (dossierCode != null && String(dossierCode).trim() !== '') {
        return String(dossierCode);
      }
    }

    const catalogData = item?.catalogData ?? item?.CatalogData ?? {};
    const value = catalogData[column?.key] ?? catalogData[column?.code];
    return value != null && String(value).trim() !== '' ? String(value) : '---';
  }

  viewDossierDetail(dossier: any): void {
    const id = dossier?.id ?? dossier?.Id;
    if (!id) return;
    const serialized = this.router.serializeUrl(
      this.router.createUrlTree(['/search/dossier-by-equipment', id])
    );
    window.open(`/#${serialized}`, '_blank');
  }

  /** Khóa lưu giá trị EAV: ưu tiên name, fallback id để tránh trùng khi name rỗng */
  private getEavFieldKey(field: any): string {
    const name = (field?.name ?? '').trim();
    return name || field?.id || '';
  }

  private getEavFieldValue(field: any): any {
    const key = this.getEavFieldKey(field);
    if (!key) return field.type === 'checkbox' ? false : '';
    const val = this.formValuesObj()[key];
    if (val === undefined || val === null || val === '') {
      return field.type === 'checkbox' ? false : '';
    }
    if (field.type === 'date') {
      const d = val instanceof Date ? val : new Date(val);
      return isNaN(d.getTime()) ? null : d;
    }
    return val;
  }

  private initEavFormValues(fields: any[], existing: Record<string, any> = {}): Record<string, any> {
    const values: Record<string, any> = { ...existing };
    for (const field of fields) {
      const key = this.getEavFieldKey(field);
      if (!key) continue;
      if (values[key] === undefined) {
        values[key] = field.type === 'checkbox' ? false : '';
      }
    }
    return values;
  }

  isNumberField(field: any): boolean {
    if (field.type === 'number') return true;
    const val = this.getEavFieldValue(field);
    if (val === null || val === undefined || val === '') return false;
    return !isNaN(Number(val));
  }

  hasValue(field: any): boolean {
    const val = this.getEavFieldValue(field);
    return val !== null && val !== undefined && val !== '';
  }

  getFormattedValue(field: any): string {
    const val = this.getEavFieldValue(field);
    if (val === null || val === undefined || val === '') return '';
    if (field.type === 'checkbox') {
      return val === true || val === 'true' ? 'Có' : 'Không';
    }
    if (field.type === 'date') {
      const d = val instanceof Date ? val : new Date(val);
      return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString('vi-VN');
    }
    return String(val);
  }

  goBack(): void {
    if (this.searchContext === 'substation') {
      this.router.navigate(this.substationId
        ? ['/search/substation', this.substationId]
        : ['/search/substation']);
      return;
    }

    this.router.navigate(this.dossierId
      ? ['/search/dossier-by-equipment', this.dossierId]
      : ['/search/dossier-by-equipment']);
  }
}
