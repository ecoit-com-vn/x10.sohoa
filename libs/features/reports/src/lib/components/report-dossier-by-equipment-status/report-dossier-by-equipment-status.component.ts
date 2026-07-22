import {
  AfterViewInit,
  Component,
  ElementRef,
  OnInit,
  ViewChild,
  signal,
  computed,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { MultiSelectModule } from 'primeng/multiselect';
import { TooltipModule } from 'primeng/tooltip';
import { TreeNode } from 'primeng/api';
import { WfBreadcrumbComponent, EcoInputTreeSelectComponent } from '@sohoa.frontend/shared/layout';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  ReportDossierByEquipmentStatusService,
  EquipmentStatusLookupItem,
  DossierByEquipmentStatusFilter,
  DossierByEquipmentStatusChartStat
} from '../../data-access/report-dossier-by-equipment-status.service';
import { UnitLookupItem } from '../../data-access/report-dossier-by-year.service';
import {
  REPORT_STATISTICS_DOSSIER_LIST_CONFIGS,
  REPORT_STATISTICS_EQUIPMENT_STATUS_GRID_CONFIGS
} from '../../data-access/report-statistics.config';
import { ReportStatisticsDossierListComponent } from '../report-statistics-dossier-list/report-statistics-dossier-list.component';
import { ReportStatisticsEquipmentStatusGridComponent } from '../report-statistics-equipment-status-grid/report-statistics-equipment-status-grid.component';
import { finalize } from 'rxjs';
import type { MainTabMode } from '../report-dossier-by-year/report-dossier-by-year.component';

interface OrgTreeNode {
  id: number;
  name: string;
  code?: string;
  parentId: number | null;
  children: OrgTreeNode[];
}

interface StationOrLineOption {
  id: string;
  name: string;
}

interface EquipmentStatusOption {
  id: string;
  name: string;
}

@Component({
  selector: 'app-report-dossier-by-equipment-status',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    MultiSelectModule,
    TooltipModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    ReportStatisticsDossierListComponent,
    ReportStatisticsEquipmentStatusGridComponent
  ],
  providers: [MessageService],
  templateUrl: './report-dossier-by-equipment-status.component.html',
  styleUrls: [
    '../report-dossier-by-year/report-dossier-by-year.component.scss',
    './report-dossier-by-equipment-status.component.scss'
  ]
})
export class ReportDossierByEquipmentStatusComponent implements OnInit, AfterViewInit {
  private reportService = inject(ReportDossierByEquipmentStatusService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private router = inject(Router);

  @ViewChild(ReportStatisticsDossierListComponent)
  dossierList?: ReportStatisticsDossierListComponent;

  @ViewChild(ReportStatisticsEquipmentStatusGridComponent)
  equipmentStatusGrid?: ReportStatisticsEquipmentStatusGridComponent;

  @ViewChild('chartBarsScroll') chartBarsScroll?: ElementRef<HTMLElement>;
  @ViewChild('chartRowsScroll') chartRowsScroll?: ElementRef<HTMLElement>;

  readonly dossierListConfig = REPORT_STATISTICS_DOSSIER_LIST_CONFIGS.DOSSIER_BY_EQUIPMENT_STATUS;
  readonly equipmentStatusGridConfig = REPORT_STATISTICS_EQUIPMENT_STATUS_GRID_CONFIGS.DOSSIER_BY_EQUIPMENT_STATUS;
  filterVersion = signal(0);

  reportFilter = computed(() => ({
    unitId: this.selectedUnitId(),
    stationIds: this.selectedStationIds().filter((id) => id?.trim()),
    equipmentStatusIds: this.selectedEquipmentStatusIds().filter((id) => id?.trim())
  }));

  units = signal<UnitLookupItem[]>([]);
  stationOrLineOptions = signal<StationOrLineOption[]>([]);
  equipmentStatusOptions = signal<EquipmentStatusOption[]>([]);

  orgUnitTree = computed(() => this.buildOrgTree(this.units()));
  primengOrgUnitTree = computed((): TreeNode[] => {
    const buildNodes = (nodes: OrgTreeNode[]): TreeNode[] =>
      nodes.map((n) => ({
        key: String(n.id),
        label: n.name,
        data: n,
        selectable: true,
        leaf: !n.children?.length,
        children: n.children?.length ? buildNodes(n.children) : undefined
      }));
    return buildNodes(this.orgUnitTree());
  });

  selectedUnitId = signal<number | null>(null);
  selectedStationIds = signal<string[]>([]);
  selectedEquipmentStatusIds = signal<string[]>([]);

  activeTab = signal<MainTabMode>('stats');
  loading = signal<boolean>(false);
  exporting = signal<boolean>(false);

  chartStats = signal<DossierByEquipmentStatusChartStat[]>([]);

  ngOnInit(): void {
    this.loadLookups();
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => {
      if (this.activeTab() === 'stats') {
        this.equipmentStatusGrid?.reload();
      }
    });
  }

  loadLookups(): void {
    this.loading.set(true);

    this.reportService.getUnitsLookup().subscribe({
      next: (units) => {
        this.units.set(units || []);
        this.applyDefaultUnitFilter();
        this.loadStationOrLineOptions();
        this.loadEquipmentStatusOptions();
      },
      error: (err) => {
        console.error('Lỗi tải danh sách đơn vị:', err);
        this.loadStationOrLineOptions();
        this.loadEquipmentStatusOptions();
      }
    });
  }

  private loadStationOrLineOptions(): void {
    const unitId = this.selectedUnitId();

    this.reportService.getStationsLookup(unitId).subscribe({
      next: (stations) => {
        const stationOptions: StationOrLineOption[] = (stations || []).map((s) => ({
          id: String(s.id),
          name: s.code ? `${s.code} — ${s.name}` : s.name
        }));

        this.reportService.getLinesLookup(unitId).subscribe({
          next: (lines) => {
            const lineOptions: StationOrLineOption[] = (lines || []).map((l) => ({
              id: String(l.id),
              name: l.code ? `${l.code} — ${l.name}` : l.name
            }));
            const options = [...stationOptions, ...lineOptions];
            this.stationOrLineOptions.set(options);
            const validIds = new Set(options.map((o) => o.id));
            this.selectedStationIds.update((ids) => ids.filter((id) => validIds.has(id)));
          },
          error: (err) => console.error('Lỗi tải danh sách đường dây:', err)
        });
      },
      error: (err) => console.error('Lỗi tải danh sách trạm:', err)
    });
  }

  private loadEquipmentStatusOptions(): void {
    this.reportService.getEquipmentStatusesLookup().subscribe({
      next: (statuses) => {
        const options: EquipmentStatusOption[] = (statuses || []).map((s: EquipmentStatusLookupItem) => ({
          id: String(s.id),
          name: s.name
        }));
        this.equipmentStatusOptions.set(options);
        this.loadStatsData();
      },
      error: (err) => {
        console.error('Lỗi tải danh sách tình trạng thiết bị:', err);
        this.equipmentStatusOptions.set([]);
        this.loadStatsData();
      }
    });
  }

  onUnitChange(unitId: number | null): void {
    this.selectedUnitId.set(unitId);
    this.loadStationOrLineOptions();
  }

  private applyDefaultUnitFilter(): void {
    const userUnitId = this.authService.getUserUnitId();
    if (!userUnitId) return;

    const exists = this.units().some((u) => Number(u.id) === userUnitId);
    if (exists) {
      this.selectedUnitId.set(userUnitId);
    }
  }

  private buildOrgTree(units: UnitLookupItem[]): OrgTreeNode[] {
    const map = new Map<number, OrgTreeNode>();
    const roots: OrgTreeNode[] = [];

    units.forEach((u) => {
      const id = Number(u.id);
      map.set(id, { id, name: u.name, code: u.code, parentId: u.parentId ?? null, children: [] });
    });

    map.forEach((node) => {
      if (node.parentId && map.has(node.parentId)) {
        map.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });

    return roots;
  }

  onFilter(): void {
    this.filterVersion.update((v) => v + 1);
    this.dossierList?.resetPagination();
    this.equipmentStatusGrid?.resetPagination();
    this.loadStatsData();
  }

  switchTab(tab: MainTabMode): void {
    this.activeTab.set(tab);
    queueMicrotask(() => {
      if (tab === 'list') {
        this.dossierList?.reload();
      } else {
        this.equipmentStatusGrid?.reload();
      }
    });
  }

  loadStatsData(): void {
    const filter: DossierByEquipmentStatusFilter = {
      unitId: this.selectedUnitId(),
      stationIds: this.selectedStationIds(),
      equipmentStatusIds: this.selectedEquipmentStatusIds()
    };

    this.loading.set(true);

    this.reportService.getChartStats(filter)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => this.chartStats.set(data || []),
        error: (err) => console.error('Lỗi tải chart stats:', err)
      });

    queueMicrotask(() => {
      this.equipmentStatusGrid?.reload();
      if (this.activeTab() === 'list') {
        this.dossierList?.reload();
      }
    });
  }

  exportExcel(): void {
    if (this.activeTab() !== 'list') {
      this.messageService.add({
        severity: 'info',
        summary: 'Thông báo',
        detail: 'Xuất Excel áp dụng cho tab Danh sách hồ sơ.'
      });
      return;
    }

    this.exporting.set(true);
    const filter: DossierByEquipmentStatusFilter = {
      unitId: this.selectedUnitId(),
      stationIds: this.selectedStationIds(),
      equipmentStatusIds: this.selectedEquipmentStatusIds()
    };

    this.reportService.exportExcel(filter)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `DanhSachHoSo_TinhTrangThietBi_${new Date().getTime()}.xlsx`;
          a.click();
          window.URL.revokeObjectURL(url);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xuất danh sách hồ sơ ra Excel.' });
        },
        error: (err) => this.showError('Xuất file Excel thất bại.', err)
      });
  }

  goBack(): void {
    this.router.navigate(['/reports/statistics']);
  }

  maxChartValue = computed(() => {
    let max = 10;
    for (const s of this.chartStats()) {
      if (s.dossierCount > max) max = s.dossierCount;
      if (s.documentCount > max) max = s.documentCount;
      if (s.pageCount > max) max = s.pageCount;
    }
    return Math.ceil(max / 10) * 10;
  });

  getBarWidthPercent(val: number): number {
    const max = this.maxChartValue();
    if (!max || max <= 0 || !val || val <= 0) return 0;
    return Math.min(100, Math.round((val / max) * 1000) / 10);
  }

  getTypeLabel(stat: DossierByEquipmentStatusChartStat): string {
    const name = stat.equipmentTypeName?.trim();
    if (name) return name;
    const code = stat.equipmentTypeCode?.trim();
    return code || '—';
  }

  syncRowScroll(source: 'bars' | 'labels', event: Event): void {
    const el = event.target as HTMLElement;
    const bars = this.chartBarsScroll?.nativeElement;
    const labels = this.chartRowsScroll?.nativeElement;
    if (!bars || !labels) return;
    if (source === 'bars' && labels.scrollTop !== el.scrollTop) {
      labels.scrollTop = el.scrollTop;
    }
    if (source === 'labels' && bars.scrollTop !== el.scrollTop) {
      bars.scrollTop = el.scrollTop;
    }
  }

  private showError(msg: string, err: unknown): void {
    console.error(msg, err);
    const detail = (err as { error?: { message?: string } })?.error?.message || msg;
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi',
      detail
    });
  }
}
