import { Component, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MenuItem, MessageService } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { ToastModule } from 'primeng/toast';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { AuthService } from '@sohoa.frontend/shared/core';
import { BhsCatalogColumn } from '@sohoa.frontend/features/dossier-management';
import {
  ReportDossierConfig,
  ReportDossierType,
  getReportDossierConfig
} from '../../data-access/report-dossier.config';
import { ReportDossierLookupItem, ReportDossierService } from '../../data-access/report-dossier.service';

@Component({
  selector: 'app-report-dossier-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, MenuModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './report-dossier-list.component.html',
  styleUrl: './report-dossier-list.component.scss'
})
export class ReportDossierListComponent implements OnInit {
  private reportService = inject(ReportDossierService);
  private messageService = inject(MessageService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  authService = inject(AuthService);

  @ViewChild('actionMenu') actionMenu?: Menu;

  reportConfig = signal<ReportDossierConfig | null>(null);
  items = signal<any[]>([]);
  loading = signal(false);
  exporting = signal(false);
  totalCount = signal(0);
  currentPage = signal(1);
  pageSize = signal(10);

  units = signal<ReportDossierLookupItem[]>([]);
  secondaryOptions = signal<ReportDossierLookupItem[]>([]);
  bhsColumns = signal<BhsCatalogColumn[]>([]);

  filterUnitId = signal<string | null>(null);
  filterSecondaryId = signal<string | null>(null);

  actionMenuItems: MenuItem[] = [];
  private selectedItem: any = null;

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));
  tableColSpan = computed(() => this.bhsColumns().length + 4);
  canExport = computed(() => {
    const cfg = this.reportConfig();
    if (!cfg) return false;
    return (
      this.authService.hasPermission('SUPER_ADMIN') ||
      this.authService.hasPermission(cfg.exportPermission)
    );
  });

  ngOnInit() {
    const type = this.route.snapshot.data['reportType'] as ReportDossierType;
    this.reportConfig.set(getReportDossierConfig(type));
    this.loadLookups();
    this.loadData();
  }

  private currentFilter() {
    const cfg = this.reportConfig();
    if (!cfg) return {};

    const base = { unitId: this.filterUnitId() };
    switch (cfg.secondaryLookup) {
      case 'gridTypes':
        return { ...base, gridTypeId: this.filterSecondaryId() };
      case 'equipments':
        return { ...base, equipmentId: this.filterSecondaryId() };
      case 'stations':
      case 'lines':
        return { ...base, infrastructureId: this.filterSecondaryId() };
      default:
        return base;
    }
  }

  loadLookups() {
    const cfg = this.reportConfig();
    if (!cfg) return;

    this.reportService.getUnits(cfg).subscribe({
      next: (res) => this.units.set(res || []),
      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được danh sách đơn vị' })
    });

    this.reportService.getBhsColumns(cfg).subscribe({
      next: (cols) => this.bhsColumns.set(cols || []),
      error: () => this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không tải được cột BHS' })
    });

    this.loadSecondaryLookups();
  }

  private loadSecondaryLookups() {
    const cfg = this.reportConfig();
    if (!cfg) return;

    this.reportService.getSecondaryLookups(cfg, this.currentFilter()).subscribe({
      next: (res) => this.secondaryOptions.set(res || []),
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: `Không tải được ${cfg.secondaryFilterLabel.toLowerCase()}`
        })
    });
  }

  onUnitChange() {
    this.filterSecondaryId.set(null);
    this.loadSecondaryLookups();
    this.onSearch();
  }

  onSecondaryChange() {
    this.onSearch();
  }

  onSearch() {
    this.currentPage.set(1);
    this.loadData();
  }

  loadData() {
    const cfg = this.reportConfig();
    if (!cfg) return;

    this.loading.set(true);
    this.items.set([]);

    this.reportService
      .search(cfg, {
        ...this.currentFilter(),
        page: this.currentPage(),
        pageSize: this.pageSize()
      })
      .subscribe({
        next: (res) => {
          this.items.set(res?.items || []);
          this.totalCount.set(res?.totalCount || 0);
          this.loading.set(false);
        },
        error: (err) => {
          const msg = err?.error?.message || 'Không thể tải danh sách báo cáo';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
          this.loading.set(false);
        }
      });
  }

  changePage(page: number) {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
      this.loadData();
    }
  }

  getCatalogValue(item: any, col: BhsCatalogColumn): string {
    const data = item?.catalogData ?? item?.CatalogData ?? {};
    const value = data[col.key] ?? data[col.code];
    return value != null && String(value).trim() !== '' ? String(value) : '-';
  }

  getUnitName(item: any): string {
    const name = item?.unitName ?? item?.UnitName;
    return name != null && String(name).trim() !== '' ? String(name) : '-';
  }

  getDimensionValue(item: any): string {
    const cfg = this.reportConfig();
    if (!cfg) return '-';
    const field = cfg.dimensionField;
    const pascal = field.charAt(0).toUpperCase() + field.slice(1);
    const value = item?.[field] ?? item?.[pascal];
    return value != null && String(value).trim() !== '' ? String(value) : '-';
  }

  openActionMenu(event: Event, item: any) {
    event.stopPropagation();
    this.selectedItem = item;
    this.actionMenuItems = [
      {
        label: 'Xem chi tiết',
        icon: 'pi pi-eye',
        command: () => this.viewDetail(item)
      }
    ];
    this.actionMenu?.toggle(event);
  }

  viewDetail(item: { id?: string; Id?: string }) {
    const cfg = this.reportConfig();
    const id = item?.id ?? item?.Id;
    if (!cfg || !id) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Không xác định được mã hồ sơ' });
      return;
    }
    void this.router.navigate([cfg.listRoute, id]);
  }

  exportExcel() {
    const cfg = this.reportConfig();
    if (!cfg || this.exporting()) return;

    this.exporting.set(true);
    this.reportService.exportExcel(cfg, this.currentFilter()).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `bao_cao_${cfg.apiSegment}_${new Date().toISOString().slice(0, 10)}.xlsx`;
        link.click();
        window.URL.revokeObjectURL(url);
        this.exporting.set(false);
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể xuất file Excel' });
        this.exporting.set(false);
      }
    });
  }
}
