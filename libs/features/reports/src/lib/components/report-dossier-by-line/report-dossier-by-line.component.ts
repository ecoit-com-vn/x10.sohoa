import {
  AfterViewInit,
  Component,
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
import { TreeNode } from 'primeng/api';
import { WfBreadcrumbComponent, EcoInputTreeSelectComponent } from '@sohoa.frontend/shared/layout';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  ReportDossierByLineService,
  LineLookupItem,
  DossierByLineFilter,
  DossierByLineSummaryStats
} from '../../data-access/report-dossier-by-line.service';
import { UnitLookupItem } from '../../data-access/report-dossier-by-year.service';
import {
  REPORT_STATISTICS_DOSSIER_LIST_CONFIGS,
  REPORT_STATISTICS_STATION_GRID_CONFIGS
} from '../../data-access/report-statistics.config';
import { ReportStatisticsDossierListComponent } from '../report-statistics-dossier-list/report-statistics-dossier-list.component';
import { ReportStatisticsStationGridComponent } from '../report-statistics-station-grid/report-statistics-station-grid.component';
import { finalize } from 'rxjs';
import type { MainTabMode } from '../report-dossier-by-year/report-dossier-by-year.component';

interface OrgTreeNode {
  id: number;
  name: string;
  code?: string;
  parentId: number | null;
  children: OrgTreeNode[];
}

interface LineOption {
  id: string;
  name: string;
}

interface YearOption {
  label: string;
  value: number;
}

const ALL_YEARS_VALUE = 0;

@Component({
  selector: 'app-report-dossier-by-line',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    SelectModule,
    MultiSelectModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    ReportStatisticsDossierListComponent,
    ReportStatisticsStationGridComponent
  ],
  providers: [MessageService],
  templateUrl: './report-dossier-by-line.component.html',
  styleUrls: [
    '../report-dossier-by-year/report-dossier-by-year.component.scss',
    './report-dossier-by-line.component.scss'
  ]
})
export class ReportDossierByLineComponent implements OnInit, AfterViewInit {
  private reportService = inject(ReportDossierByLineService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private router = inject(Router);

  @ViewChild(ReportStatisticsDossierListComponent)
  dossierList?: ReportStatisticsDossierListComponent;

  @ViewChild(ReportStatisticsStationGridComponent)
  lineGrid?: ReportStatisticsStationGridComponent;

  readonly dossierListConfig = REPORT_STATISTICS_DOSSIER_LIST_CONFIGS.DOSSIER_BY_LINE;
  readonly lineGridConfig = REPORT_STATISTICS_STATION_GRID_CONFIGS.DOSSIER_BY_LINE;
  filterVersion = signal(0);

  reportFilter = computed(() => {
    const year = this.selectedYear();
    return {
      unitId: this.selectedUnitId(),
      lineIds: this.selectedLineIds().filter((id) => id?.trim()),
      ...(year > 0 ? { year } : {})
    };
  });

  units = signal<UnitLookupItem[]>([]);
  lineOptions = signal<LineOption[]>([]);
  years = signal<number[]>([]);

  yearOptions = computed((): YearOption[] => [
    { label: 'Tất cả các năm', value: ALL_YEARS_VALUE },
    ...this.years().map((year) => ({ label: String(year), value: year }))
  ]);

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
  selectedLineIds = signal<string[]>([]);
  selectedYear = signal<number>(new Date().getFullYear());

  activeTab = signal<MainTabMode>('stats');
  loading = signal<boolean>(false);
  exporting = signal<boolean>(false);

  summaryStats = signal<DossierByLineSummaryStats | null>(null);

  ngOnInit(): void {
    this.loadLookups();
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => {
      if (this.activeTab() === 'stats') {
        this.lineGrid?.reload();
      }
    });
  }

  loadLookups(): void {
    this.loading.set(true);

    this.reportService.getUnitsLookup().subscribe({
      next: (units) => {
        this.units.set(units || []);
        this.applyDefaultUnitFilter();
        this.loadLineOptions();
        this.loadYearsLookup();
      },
      error: (err) => {
        console.error('Lỗi tải danh sách đơn vị:', err);
        this.loadLineOptions();
        this.loadYearsLookup();
      }
    });
  }

  private loadLineOptions(): void {
    this.reportService.getLinesLookup(this.selectedUnitId()).subscribe({
      next: (lines) => {
        const options: LineOption[] = (lines || []).map((line: LineLookupItem) => ({
          id: String(line.id),
          name: line.code ? `${line.code} — ${line.name}` : line.name
        }));
        this.lineOptions.set(options);
        const validIds = new Set(options.map((o) => o.id));
        this.selectedLineIds.update((ids) => ids.filter((id) => validIds.has(id)));
      },
      error: (err) => console.error('Lỗi tải danh sách đường dây:', err)
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

  onUnitChange(unitId: number | null): void {
    this.selectedUnitId.set(unitId);
    this.loadLineOptions();
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
    this.lineGrid?.resetPagination();
    this.loadStatsData();
  }

    onResetSearch(): void {
    this.selectedUnitId.set(null);
    this.selectedLineIds.set([]);
    this.selectedYear.set(new Date().getFullYear());
    this.applyDefaultUnitFilter();
    this.onFilter();
  }

  switchTab(tab: MainTabMode): void {
    this.activeTab.set(tab);
    queueMicrotask(() => {
      if (tab === 'list') {
        this.dossierList?.reload();
      } else {
        this.lineGrid?.reload();
      }
    });
  }

  loadStatsData(): void {
    const year = this.selectedYear();
    const filter: DossierByLineFilter = {
      unitId: this.selectedUnitId(),
      lineIds: this.selectedLineIds(),
      ...(year > 0 ? { year } : {})
    };

    this.loading.set(true);

    this.reportService.getSummaryStats(filter)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => this.summaryStats.set(data),
        error: (err) => {
          console.error('Lỗi tải summary stats:', err);
          this.summaryStats.set(null);
        }
      });

    queueMicrotask(() => {
      this.lineGrid?.reload();
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
    const year = this.selectedYear();
    const filter: DossierByLineFilter = {
      unitId: this.selectedUnitId(),
      lineIds: this.selectedLineIds(),
      ...(year > 0 ? { year } : {})
    };

    this.reportService.exportExcel(filter)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = this.buildExportFileName();
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

  formatGrowth(value: number | null | undefined): string {
    if (value == null) return '—';
    const prefix = value > 0 ? '+' : '';
    return `${prefix}${value}%`;
  }

  growthClass(value: number | null | undefined): string {
    if (value == null || value === 0) return 'neutral';
    return value > 0 ? 'up' : 'down';
  }

  private buildExportFileName(): string {
    const year = this.selectedYear();
    const suffix = year > 0 ? `Nam_${year}` : 'TatCaCacNam';
    return `DanhSachHoSo_DuongDay_${suffix}_${new Date().getTime()}.xlsx`;
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
