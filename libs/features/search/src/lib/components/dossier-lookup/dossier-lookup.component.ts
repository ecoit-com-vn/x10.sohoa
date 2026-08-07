import { Component, OnInit, signal, computed, inject } from '@angular/core';

import { EcoPaginatorComponent, WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { Router } from '@angular/router';

import { ToastModule } from 'primeng/toast';

import { TooltipModule } from 'primeng/tooltip';

import { MenuItem, MessageService, TreeNode } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { SelectModule } from 'primeng/select';
import { TreeSelectModule } from 'primeng/treeselect';
import { DatePickerModule } from 'primeng/datepicker';
import { Subscription } from 'rxjs';

import { AuthService } from '@sohoa.frontend/shared/core';

import { BhsCatalogColumn } from '@sohoa.frontend/features/dossier-management';

import {

  DossierByEquipmentFilter,

  DossierByEquipmentLookupItem,

  DossierByEquipmentService

} from '../../data-access/dossier-by-equipment.service';

import { LookupTrackingService } from '../../data-access/lookup-tracking.service';



@Component({

  selector: 'app-dossier-lookup',

  standalone: true,

  imports: [CommonModule, FormsModule, ToastModule, TooltipModule, MenuModule, SelectModule, TreeSelectModule, DatePickerModule, WfBreadcrumbComponent, EcoPaginatorComponent],

  providers: [MessageService],

  templateUrl: './dossier-lookup.component.html',

  styleUrl: './dossier-lookup.component.scss'

})

export class DossierLookupComponent implements OnInit {

  private dossierByEquipmentService = inject(DossierByEquipmentService);

  private lookupTrackingService = inject(LookupTrackingService);

  private messageService = inject(MessageService);

  private router = inject(Router);

  authService = inject(AuthService);



  items = signal<any[]>([]);

  loading = signal<boolean>(false);

  totalCount = signal<number>(0);

  currentPage = signal<number>(1);

  pageSize = signal<number>(10);



  searchKeyword = signal<string>('');

  createdDateFrom = signal<Date | null>(null);

  createdDateTo = signal<Date | null>(null);

  filterGridTypeId = signal<number | null>(null);

  filterInfrastructureId = signal<string | null>(null);

  filterEquipmentTypeId = signal<string | null>(null);

  filterEquipmentId = signal<string | null>(null);

  filterDossierTypeId = signal<string | null>(null);

  filterStorageNode = signal<TreeNode | null>(null);

  private appliedFilter = signal<DossierByEquipmentFilter>({});



  gridTypes = signal<DossierByEquipmentLookupItem[]>([]);

  infrastructures = signal<DossierByEquipmentLookupItem[]>([]);

  equipmentTypes = signal<DossierByEquipmentLookupItem[]>([]);

  equipments = signal<DossierByEquipmentLookupItem[]>([]);

  dossierTypes = signal<DossierByEquipmentLookupItem[]>([]);

  storageTree = signal<TreeNode[]>([]);

  bhsColumns = signal<BhsCatalogColumn[]>([]);
  actionMenuItems: MenuItem[] = [];
  private equipmentLookupSubscription?: Subscription;



  tableColSpan = computed(() => this.bhsColumns().length + 5);

  openActionMenu(item: any, event: MouseEvent, menu: Menu): void {
    this.actionMenuItems = [
      {
        label: 'Xem chi tiết',
        title: 'Xem chi tiết',
        icon: 'pi pi-eye color-teal',
        command: () => this.viewDetail(item),
      },
    ];
    menu.toggle(event);
  }



  ngOnInit() {

    this.loadStaticLookups();

    this.loadDependentLookups();

    this.loadData();

  }



  private draftFilter(): DossierByEquipmentFilter {

    return {

      keyword: this.searchKeyword().trim() || undefined,

      createdDateFrom: this.formatDateForApi(this.createdDateFrom()),

      createdDateTo: this.formatDateForApi(this.createdDateTo()),

      gridTypeId: this.filterGridTypeId(),

      infrastructureId: this.filterInfrastructureId(),

      equipmentTypeId: this.filterEquipmentTypeId(),

      equipmentId: this.filterEquipmentId(),

      dossierTypeId: this.filterDossierTypeId(),

      storageLevel: this.filterStorageNode()?.data?.level ?? null,

      storageId: this.filterStorageNode()?.data?.id ?? null

    };

  }



  private loadStaticLookups() {

    this.dossierByEquipmentService.getGridTypes().subscribe({

      next: (items) => this.gridTypes.set(items || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được loại lưới điện' })

    });

    this.dossierByEquipmentService.getBhsColumns().subscribe({

      next: (cols) => this.bhsColumns.set(cols || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được cột BHS' })

    });

    this.dossierByEquipmentService.getPhysicalStorageTree().subscribe({

      next: (items) => this.storageTree.set(this.buildStorageTree(items || [])),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được vị trí lưu' })

    });

  }



  loadDependentLookups() {

    const filter = this.draftFilter();



    this.dossierByEquipmentService.getInfrastructures(filter).subscribe({

      next: (res) => this.infrastructures.set(res || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được danh sách trạm/đường dây' })

    });



    this.equipmentLookupSubscription?.unsubscribe();

    this.dossierByEquipmentService.getEquipmentTypes(filter).subscribe({

      next: (res) => this.equipmentTypes.set(res || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được danh sách loại thiết bị' })

    });

    this.equipmentLookupSubscription = this.dossierByEquipmentService.getEquipments(filter).subscribe({

      next: (res) => this.equipments.set(res || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được danh sách thiết bị' })

    });



    this.dossierByEquipmentService.getDossierTypes(filter).subscribe({

      next: (res) => this.dossierTypes.set(res || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được loại hồ sơ' })

    });

  }



  onGridTypeChange() {

    this.filterInfrastructureId.set(null);

    this.filterEquipmentId.set(null);

    this.onSearch();

  }



  onInfrastructureChange() {

    this.filterEquipmentTypeId.set(null);

    this.filterEquipmentId.set(null);

    this.onSearch();

  }

  onEquipmentTypeChange() {

    this.filterEquipmentId.set(null);

    this.onSearch();

  }



  onSearch() {

    const keyword = this.searchKeyword().trim();

    const fromDate = this.createdDateFrom();

    const toDate = this.createdDateTo();

    if (fromDate && toDate && fromDate.getTime() > toDate.getTime()) {

      this.messageService.add({

        severity: 'warn',

        summary: 'Khoảng ngày không hợp lệ',

        detail: 'Từ ngày không được lớn hơn Đến ngày.'

      });

      return;

    }

    this.searchKeyword.set(keyword);

    this.appliedFilter.set({ ...this.draftFilter(), keyword: keyword || undefined });

    this.currentPage.set(1);

    this.loadDependentLookups();

    this.loadData();

  }



  onResetSearch() {

    this.searchKeyword.set('');

    this.createdDateFrom.set(null);

    this.createdDateTo.set(null);

    this.filterGridTypeId.set(null);

    this.filterInfrastructureId.set(null);

    this.filterEquipmentTypeId.set(null);

    this.filterEquipmentId.set(null);

    this.filterDossierTypeId.set(null);

    this.filterStorageNode.set(null);

    this.appliedFilter.set({});

    this.currentPage.set(1);

    this.loadDependentLookups();

    this.loadData();

  }

  private formatDateForApi(value: Date | null): string | undefined {
    if (!value) return undefined;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private buildStorageTree(items: any[]): TreeNode[] {
    return items.map(shelf => ({
      key: `shelf:${shelf.id}`,
      label: this.formatStorageLabel(shelf),
      data: { id: Number(shelf.id), level: 'shelf' },
      children: (shelf.floors || []).map((floor: any) => ({
        key: `floor:${floor.id}`,
        label: this.formatStorageLabel(floor),
        data: { id: Number(floor.id), level: 'floor' },
        children: (floor.boxes || []).map((box: any) => ({
          key: `box:${box.id}`,
          label: this.formatStorageLabel(box),
          data: { id: Number(box.id), level: 'box' }
        }))
      }))
    }));
  }

  private formatStorageLabel(item: any): string {
    const name = String(item?.name || '').trim();
    const code = String(item?.code || '').trim();
    return name && code && name !== code ? `${name} (${code})` : name || code;
  }



  loadData() {

    this.loading.set(true);

    this.items.set([]);



    this.dossierByEquipmentService.search({

      ...this.appliedFilter(),

      page: this.currentPage(),

      pageSize: this.pageSize()

    }).subscribe({

      next: (res) => {

        this.items.set(res?.items || []);

        this.totalCount.set(res?.totalCount || 0);

        this.loading.set(false);

      },

      error: (err) => {

        const msg = err?.error?.message || 'Không thể tải danh sách hồ sơ';

        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });

        this.loading.set(false);

      }

    });

  }



  getCatalogValue(item: any, col: BhsCatalogColumn): string {

    const data = item?.catalogData ?? item?.CatalogData ?? {};

    const value = data[col.key] ?? data[col.code];

    return value != null && String(value).trim() !== '' ? String(value) : '-';

  }



  getDossierTypeName(item: any): string {

    const name = item?.dossierTypeName ?? item?.DossierTypeName;

    return name != null && String(name).trim() !== '' ? String(name) : '-';

  }



  onListPageChange(event: { first?: number; rows?: number }) {
    const rows = Number(event.rows) || this.pageSize();
    const first = Number(event.first) || 0;

    this.pageSize.set(rows);
    this.currentPage.set(Math.floor(first / rows) + 1);
    this.loadData();
  }



  viewDetail(item: { id?: string; Id?: string }) {

    const id = item?.id ?? item?.Id;

    if (!id) {

      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Không xác định được mã hồ sơ' });

      return;

    }

    this.lookupTrackingService.recordView('DOSSIER', id);
    void this.router.navigate(['/search/dossier-by-equipment', id]);

  }

}


