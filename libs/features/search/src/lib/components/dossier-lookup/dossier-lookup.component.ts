import { Component, OnInit, signal, computed, inject } from '@angular/core';

import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { Router } from '@angular/router';

import { ToastModule } from 'primeng/toast';

import { TooltipModule } from 'primeng/tooltip';

import { MenuItem, MessageService } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';

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

  imports: [CommonModule, FormsModule, ToastModule, TooltipModule, MenuModule, WfBreadcrumbComponent],

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

  publishDateFrom = signal<string>('');

  publishDateTo = signal<string>('');

  filterGridTypeId = signal<number | null>(null);

  filterInfrastructureId = signal<string | null>(null);

  filterEquipmentTypeId = signal<string | null>(null);

  filterDossierTypeId = signal<string | null>(null);



  gridTypes = signal<DossierByEquipmentLookupItem[]>([]);

  infrastructures = signal<DossierByEquipmentLookupItem[]>([]);

  equipmentTypes = signal<DossierByEquipmentLookupItem[]>([]);

  dossierTypes = signal<DossierByEquipmentLookupItem[]>([]);

  bhsColumns = signal<BhsCatalogColumn[]>([]);
  actionMenuItems: MenuItem[] = [];



  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

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



  private currentFilter(): DossierByEquipmentFilter {

    return {

      keyword: this.searchKeyword().trim() || undefined,

      publishDateFrom: this.publishDateFrom() || undefined,

      publishDateTo: this.publishDateTo() || undefined,

      gridTypeId: this.filterGridTypeId(),

      infrastructureId: this.filterInfrastructureId(),

      equipmentTypeId: this.filterEquipmentTypeId(),

      dossierTypeId: this.filterDossierTypeId()

    };

  }



  private loadStaticLookups() {

    this.dossierByEquipmentService.getGridTypes().subscribe({

      next: (res) => this.gridTypes.set(res || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được loại lưới điện' })

    });



    this.dossierByEquipmentService.getBhsColumns().subscribe({

      next: (cols) => this.bhsColumns.set(cols || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được cột BHS' })

    });

  }



  loadDependentLookups() {

    const filter = this.currentFilter();



    this.dossierByEquipmentService.getInfrastructures(filter).subscribe({

      next: (res) => this.infrastructures.set(res || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được danh sách trạm/đường dây' })

    });



    this.dossierByEquipmentService.getEquipmentTypes(filter).subscribe({

      next: (res) => this.equipmentTypes.set(res || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được loại thiết bị' })

    });



    this.dossierByEquipmentService.getDossierTypes(filter).subscribe({

      next: (res) => this.dossierTypes.set(res || []),

      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được loại hồ sơ' })

    });

  }



  onGridTypeChange() {

    this.filterInfrastructureId.set(null);

    this.filterEquipmentTypeId.set(null);

    this.filterDossierTypeId.set(null);

    this.loadDependentLookups();

    this.onSearch();

  }



  onInfrastructureChange() {

    this.filterEquipmentTypeId.set(null);

    this.filterDossierTypeId.set(null);

    this.loadDependentLookups();

    this.onSearch();

  }



  onEquipmentTypeChange() {

    this.filterDossierTypeId.set(null);

    this.loadDependentLookups();

    this.onSearch();

  }



  onSearch() {

    this.currentPage.set(1);

    this.loadDependentLookups();

    this.loadData();

  }



  loadData() {

    this.loading.set(true);

    this.items.set([]);



    this.dossierByEquipmentService.search({

      ...this.currentFilter(),

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



  changePage(page: number) {

    if (page >= 1 && page <= this.totalPages()) {

      this.currentPage.set(page);

      this.loadData();

    }

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


