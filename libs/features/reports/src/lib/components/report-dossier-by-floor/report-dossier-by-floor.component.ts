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
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { TooltipModule } from 'primeng/tooltip';
import { TreeNode } from 'primeng/api';
import { WfBreadcrumbComponent, EcoInputTreeSelectComponent } from '@sohoa.frontend/shared/layout';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  ReportDossierByFloorService,
  FloorLookupItem,
  DossierByFloorFilter,
  DossierByFloorChartStat
} from '../../data-access/report-dossier-by-floor.service';
import { UnitLookupItem } from '../../data-access/report-dossier-by-year.service';
import {
  REPORT_STATISTICS_DOSSIER_LIST_CONFIGS,
  REPORT_STATISTICS_FLOOR_GRID_CONFIGS
} from '../../data-access/report-statistics.config';
import {
  buildYearOptions,
  yearToFilterParam,
  buildExportYearSuffix
} from '../../data-access/report-year-filter.util';
import { ReportStatisticsDossierListComponent } from '../report-statistics-dossier-list/report-statistics-dossier-list.component';
import { ReportStatisticsFloorGridComponent } from '../report-statistics-floor-grid/report-statistics-floor-grid.component';
import { finalize } from 'rxjs';
import type { MainTabMode } from '../report-dossier-by-year/report-dossier-by-year.component';

interface OrgTreeNode {
  id: number;
  name: string;
  code?: string;
  parentId: number | null;
  children: OrgTreeNode[];
}

interface FloorOption {
  id: string;
  name: string;
}

@Component({
  selector: 'app-report-dossier-by-floor',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    SelectModule,
    MultiSelectModule,
    TooltipModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    ReportStatisticsDossierListComponent,
    ReportStatisticsFloorGridComponent
  ],
  providers: [MessageService],
  templateUrl: './report-dossier-by-floor.component.html',
  styleUrls: [
    '../report-dossier-by-year/report-dossier-by-year.component.scss',
    './report-dossier-by-floor.component.scss'
  ]
})
export class ReportDossierByFloorComponent implements OnInit, AfterViewInit {
  private reportService = inject(ReportDossierByFloorService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private router = inject(Router);

  @ViewChild(ReportStatisticsDossierListComponent)
  dossierList?: ReportStatisticsDossierListComponent;

  @ViewChild(ReportStatisticsFloorGridComponent)
  floorGrid?: ReportStatisticsFloorGridComponent;

  @ViewChild('chartBarsScroll') chartBarsScroll?: ElementRef<HTMLElement>;
  @ViewChild('chartRowsScroll') chartRowsScroll?: ElementRef<HTMLElement>;

  readonly dossierListConfig = REPORT_STATISTICS_DOSSIER_LIST_CONFIGS.DOSSIER_BY_FLOOR;
  readonly floorGridConfig = REPORT_STATISTICS_FLOOR_GRID_CONFIGS.DOSSIER_BY_FLOOR;
  filterVersion = signal(0);

  reportFilter = computed(() => ({
    unitId: this.selectedUnitId(),
    floorIds: this.selectedFloorIds().filter((id) => id?.trim()),
    ...yearToFilterParam(this.selectedYear())
  }));

  units = signal<UnitLookupItem[]>([]);
  floorOptions = signal<FloorOption[]>([]);
  years = signal<number[]>([]);
  yearOptions = computed(() => buildYearOptions(this.years()));

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
  selectedFloorIds = signal<string[]>([]);
  selectedYear = signal<number>(new Date().getFullYear());

  activeTab = signal<MainTabMode>('stats');
  loading = signal<boolean>(false);
  exporting = signal<boolean>(false);

  chartStats = signal<DossierByFloorChartStat[]>([]);

  ngOnInit(): void {
    this.loadLookups();
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => {
      if (this.activeTab() === 'stats') {
        this.floorGrid?.reload();
      }
    });
  }

  loadLookups(): void {
    this.loading.set(true);

    this.reportService.getUnitsLookup().subscribe({
      next: (units) => {
        this.units.set(units || []);
        this.applyDefaultUnitFilter();
        this.loadFloorOptions();
        this.loadYearsLookup();
      },
      error: (err) => {
        console.error('Lỗi tải danh sách đơn vị:', err);
        this.loadFloorOptions();
        this.loadYearsLookup();
      }
    });
  }

  onUnitChange(unitId: number | null): void {
    this.selectedUnitId.set(unitId);
    this.selectedFloorIds.set([]);
    this.loadFloorOptions();
  }

  private loadFloorOptions(): void {
    this.reportService.getFloorsLookup(this.selectedUnitId()).subscribe({
      next: (floors) => {
        const options: FloorOption[] = (floors || []).map((f: FloorLookupItem) => ({
          id: String(f.id),
          name: f.code ? `${f.code} — ${f.name}` : f.name
        }));
        this.floorOptions.set(options);
      },
      error: (err) => console.error('Lỗi tải tầng hồ sơ:', err)
    });
  }

  private loadYearsLookup(): void {
    this.reportService.getYearsLookup().subscribe({
      next: (years) => {
        const availableYears = years || [new Date().getFullYear()];
        this.years.set(availableYears);
        const currentYear = new Date().getFullYear();
        const defaultYear = availableYears.includes(currentYear) ? currentYear : availableYears[0];
        this.selectedYear.set(defaultYear);
        this.loadStatsData();
      },
      error: (err) => {
        console.error('Lỗi tải danh sách năm:', err);
        this.years.set([new Date().getFullYear()]);
        this.selectedYear.set(new Date().getFullYear());
        this.loadStatsData();
      }
    });
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
    this.floorGrid?.resetPagination();
    this.loadStatsData();
  }

  switchTab(tab: MainTabMode): void {
    this.activeTab.set(tab);
    queueMicrotask(() => {
      if (tab === 'list') {
        this.dossierList?.reload();
      } else {
        this.floorGrid?.reload();
      }
    });
  }

  loadStatsData(): void {
    const filter: DossierByFloorFilter = {
      unitId: this.selectedUnitId(),
      floorIds: this.selectedFloorIds(),
      ...yearToFilterParam(this.selectedYear())
    };

    this.loading.set(true);

    this.reportService.getChartStats(filter)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => this.chartStats.set(data || []),
        error: (err) => console.error('Lỗi tải chart stats:', err)
      });

    queueMicrotask(() => {
      this.floorGrid?.reload();
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
    const filter: DossierByFloorFilter = {
      unitId: this.selectedUnitId(),
      floorIds: this.selectedFloorIds(),
      ...yearToFilterParam(this.selectedYear())
    };

    this.reportService.exportExcel(filter)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `DanhSachHoSo_TangLuuTru_${buildExportYearSuffix(this.selectedYear())}_${new Date().getTime()}.xlsx`;
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

  getFloorLabel(stat: DossierByFloorChartStat): string {
    const name = stat.floorName?.trim();
    if (name) return name;
    const code = stat.floorCode?.trim();
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
