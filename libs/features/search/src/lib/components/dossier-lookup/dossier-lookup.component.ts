import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { AuthService } from '@sohoa.frontend/shared/core';
import { BhsCatalogColumn } from '@sohoa.frontend/features/dossier-management';
import {
  DossierByEquipmentFilter,
  DossierByEquipmentLookupItem,
  DossierByEquipmentService
} from '../../data-access/dossier-by-equipment.service';

@Component({
  selector: 'app-dossier-lookup',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, TooltipModule],
  providers: [MessageService],
  templateUrl: './dossier-lookup.component.html',
  styleUrl: './dossier-lookup.component.scss'
})
export class DossierLookupComponent implements OnInit {
  private dossierByEquipmentService = inject(DossierByEquipmentService);
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
  filterInfrastructureId = signal<string | null>(null);
  filterEquipmentTypeId = signal<string | null>(null);
  filterEquipmentId = signal<string | null>(null);
  filterDossierTypeId = signal<string | null>(null);

  infrastructures = signal<DossierByEquipmentLookupItem[]>([]);
  equipmentTypes = signal<DossierByEquipmentLookupItem[]>([]);
  equipments = signal<DossierByEquipmentLookupItem[]>([]);
  dossierTypes = signal<DossierByEquipmentLookupItem[]>([]);
  bhsColumns = signal<BhsCatalogColumn[]>([]);

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));
  tableColSpan = computed(() => this.bhsColumns().length + 4);

  ngOnInit() {
    this.loadLookups();
    this.loadData();
  }

  private currentFilter(): DossierByEquipmentFilter {
    return {
      keyword: this.searchKeyword().trim() || undefined,
      publishDateFrom: this.publishDateFrom() || undefined,
      publishDateTo: this.publishDateTo() || undefined,
      infrastructureId: this.filterInfrastructureId(),
      equipmentTypeId: this.filterEquipmentTypeId(),
      equipmentId: this.filterEquipmentId(),
      dossierTypeId: this.filterDossierTypeId()
    };
  }

  loadLookups() {
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

    this.dossierByEquipmentService.getBhsColumns().subscribe({
      next: (cols) => this.bhsColumns.set(cols || []),
      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được cột BHS' })
    });

    this.loadEquipments();
  }

  onFilterRelationChange() {
    this.filterEquipmentId.set(null);
    this.loadEquipments();
    this.loadLookups();
  }

  private loadEquipments() {
    const infraId = this.filterInfrastructureId();
    const typeId = this.filterEquipmentTypeId();

    if (!infraId && !typeId) {
      this.equipments.set([]);
      return;
    }

    this.dossierByEquipmentService.getEquipments(this.currentFilter()).subscribe({
      next: (res) => this.equipments.set(res || []),
      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được danh sách thiết bị' })
    });
  }

  onSearch() {
    this.currentPage.set(1);
    this.loadLookups();
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
    void this.router.navigate(['/search/dossier-by-equipment', id]);
  }
}
