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
import { TooltipModule } from 'primeng/tooltip';
import { TreeNode } from 'primeng/api';
import { WfBreadcrumbComponent, EcoInputTreeSelectComponent, EcoInputDateComponent } from '@sohoa.frontend/shared/layout';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  ReportDossierByMonthService,
  ReportMonthLookupItem,
  DossierByMonthFilter,
  DossierByMonthChartStat,
  DossierByMonthRatioStat
} from '../../data-access/report-dossier-by-month.service';
import {
  UnitLookupItem,
  ObjectTypeLookupItem
} from '../../data-access/report-dossier-by-year.service';
import { REPORT_STATISTICS_DOSSIER_LIST_CONFIGS, REPORT_STATISTICS_STATION_GRID_CONFIGS } from '../../data-access/report-statistics.config';
import { ReportStatisticsDossierListComponent } from '../report-statistics-dossier-list/report-statistics-dossier-list.component';
import { ReportStatisticsStationGridComponent } from '../report-statistics-station-grid/report-statistics-station-grid.component';
import { finalize, forkJoin } from 'rxjs';
import type { MainTabMode } from '../report-dossier-by-year/report-dossier-by-year.component';

interface OrgTreeNode {
  id: number;
  name: string;
  code?: string;
  parentId: number | null;
  children: OrgTreeNode[];
}

@Component({
  selector: 'app-report-dossier-by-month',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    SelectModule,
    TooltipModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    EcoInputDateComponent,
    ReportStatisticsDossierListComponent,
    ReportStatisticsStationGridComponent
  ],
  providers: [MessageService],
  templateUrl: './report-dossier-by-month.component.html',
  styleUrls: [
    '../report-dossier-by-year/report-dossier-by-year.component.scss',
    './report-dossier-by-month.component.scss'
  ]
})
export class ReportDossierByMonthComponent implements OnInit, AfterViewInit {
  private reportService = inject(ReportDossierByMonthService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private router = inject(Router);

  @ViewChild(ReportStatisticsDossierListComponent)
  dossierList?: ReportStatisticsDossierListComponent;

  @ViewChild(ReportStatisticsStationGridComponent)
  stationGrid?: ReportStatisticsStationGridComponent;

  readonly dossierListConfig = REPORT_STATISTICS_DOSSIER_LIST_CONFIGS.DOSSIER_BY_MONTH;
  readonly stationGridConfig = REPORT_STATISTICS_STATION_GRID_CONFIGS.DOSSIER_BY_MONTH;
  filterVersion = signal(0);

  reportFilter = computed(() => {
    const d = this.reportMonthDate();
    return {
      unitId: this.selectedUnitId(),
      objectType: this.selectedObjectType(),
      year: d?.getFullYear() ?? new Date().getFullYear(),
      month: d ? d.getMonth() + 1 : new Date().getMonth() + 1
    };
  });

  units = signal<UnitLookupItem[]>([]);
  objectTypes = signal<ObjectTypeLookupItem[]>([]);
  flatMonths = signal<ReportMonthLookupItem[]>([]);
  reportMonthDate = signal<Date | null>(null);

  /** Giới hạn lịch chọn tháng theo dữ liệu lookup */
  monthDateBounds = computed(() => {
    const months = this.flatMonths();
    if (months.length === 0) {
      return { min: undefined as Date | undefined, max: undefined as Date | undefined };
    }

    const sorted = [...months].sort((a, b) => a.year - b.year || a.month - b.month);
    const first = sorted[0];
    const last = sorted[sorted.length - 1];
    return {
      min: new Date(first.year, first.month - 1, 1),
      max: new Date(last.year, last.month - 1, 1)
    };
  });

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
  selectedObjectType = signal<number | null>(0);

  /** Chỉ cập nhật khi Tìm kiếm (trong loadStatsData) — dùng cho legend/donut, tránh đổi ngay khi user chỉnh dropdown. */
  appliedObjectType = signal<number | null>(0);

  activeTab = signal<MainTabMode>('stats');
  loading = signal<boolean>(false);
  exporting = signal<boolean>(false);

  chartStats = signal<DossierByMonthChartStat[]>([]);
  ratioStats = signal<DossierByMonthRatioStat[]>([]);

  ngOnInit(): void {
    this.loadLookups();
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => {
      if (this.activeTab() === 'stats') {
        this.stationGrid?.reload();
      }
    });
  }

  loadLookups(): void {
    this.loading.set(true);

    this.reportService.getUnitsLookup().subscribe({
      next: (units) => {
        this.units.set(units || []);
        this.applyDefaultUnitFilter();
        this.loadSecondaryLookups();
      },
      error: (err) => {
        console.error('Lỗi tải danh sách đơn vị:', err);
        this.loadSecondaryLookups();
      }
    });
  }

  private loadSecondaryLookups(): void {
    this.reportService.getObjectTypesLookup().subscribe({
      next: (types) => this.objectTypes.set(types || []),
      error: (err) => console.error('Lỗi tải loại đối tượng:', err)
    });

    this.reportService.getMonthsLookup().subscribe({
      next: (months) => {
        const list = months || [];
        this.flatMonths.set(list);
        this.applyDefaultMonthFilter(list);
        this.loadStatsData();
      },
      error: (err) => {
        console.error('Lỗi tải danh sách tháng:', err);
        const now = new Date();
        this.flatMonths.set([{
          year: now.getFullYear(),
          month: now.getMonth() + 1,
          label: `Tháng ${String(now.getMonth() + 1).padStart(2, '0')}/${now.getFullYear()}`
        }]);
        this.applyDefaultMonthFilter(this.flatMonths());
        this.loadStatsData();
      }
    });
  }

  /** Chọn tháng mới nhất có dữ liệu */
  private applyDefaultMonthFilter(months: ReportMonthLookupItem[]): void {
    if (months.length === 0) {
      const now = new Date();
      this.reportMonthDate.set(new Date(now.getFullYear(), now.getMonth(), 1));
      return;
    }

    const latest = months[0];
    this.reportMonthDate.set(new Date(latest.year, latest.month - 1, 1));
  }

  onReportMonthChange(date: Date | null): void {
    this.reportMonthDate.set(date);
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
    this.stationGrid?.resetPagination();
    this.loadStatsData();
  }

  switchTab(tab: MainTabMode): void {
    this.activeTab.set(tab);
    queueMicrotask(() => {
      if (tab === 'list') {
        this.dossierList?.reload();
      } else {
        this.stationGrid?.reload();
      }
    });
  }

  loadStatsData(): void {
    this.appliedObjectType.set(this.selectedObjectType());

    const filter: DossierByMonthFilter = {
      unitId: this.selectedUnitId(),
      objectType: this.selectedObjectType(),
      year: this.reportFilter().year,
      month: this.reportFilter().month
    };

    this.loading.set(true);

    forkJoin({
      chart: this.reportService.getChartStats(filter),
      ratio: this.reportService.getRatioStats(filter)
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ chart, ratio }) => {
          this.chartStats.set(chart || []);
          this.ratioStats.set(ratio || []);
        },
        error: (err) => console.error('Lỗi tải dữ liệu thống kê:', err)
      });

    queueMicrotask(() => {
      this.stationGrid?.reload();
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
    const filter: DossierByMonthFilter = {
      unitId: this.selectedUnitId(),
      objectType: this.selectedObjectType(),
      year: this.reportFilter().year,
      month: this.reportFilter().month
    };

    const monthLabel = `${String(filter.month).padStart(2, '0')}_${filter.year}`;

    this.reportService.exportExcel(filter)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `DanhSachHoSo_Thang_${monthLabel}_${new Date().getTime()}.xlsx`;
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
    this.filterVersion();
    this.chartStats();

    let max = 10;
    const stats = [
      this.showStationRatio() ? this.stationStat() : null,
      this.showLineRatio() ? this.lineStat() : null,
      this.showEquipmentRatio() ? this.equipmentStat() : null
    ];

    for (const s of stats) {
      if (!s) continue;
      if (s.dossierCount > max) max = s.dossierCount;
      if (s.documentCount > max) max = s.documentCount;
      if (s.pageCount > max) max = s.pageCount;
    }
    return Math.ceil(max / 10) * 10;
  });

  getBarHeightPx(val: number): number {
    const max = this.maxChartValue();
    if (!max || max <= 0 || !val || val <= 0) return 0;
    const maxPx = 200;
    return Math.min(maxPx, Math.max(4, Math.round((val / max) * maxPx)));
  }

  totalDossierCount = computed(() => {
    return this.chartStats().reduce((sum, s) => sum + s.dossierCount, 0);
  });

  stationStat = computed(() => {
    return this.chartStats().find(s => s.groupCode === 'STATION') || { groupName: 'Trạm biến áp', groupCode: 'STATION', dossierCount: 0, documentCount: 0, pageCount: 0 };
  });

  lineStat = computed(() => {
    return this.chartStats().find(s => s.groupCode === 'LINE') || { groupName: 'Đường dây', groupCode: 'LINE', dossierCount: 0, documentCount: 0, pageCount: 0 };
  });

  equipmentStat = computed(() => {
    return this.chartStats().find(s => s.groupCode === 'EQUIPMENT') || { groupName: 'Thiết bị', groupCode: 'EQUIPMENT', dossierCount: 0, documentCount: 0, pageCount: 0 };
  });

  stationRatio = computed(() => {
    return this.ratioStats().find(r => r.groupCode === 'STATION') || { groupName: 'Trạm biến áp', groupCode: 'STATION', dossierCount: 0, percentage: 0 };
  });

  lineRatio = computed(() => {
    return this.ratioStats().find(r => r.groupCode === 'LINE') || { groupName: 'Đường dây', groupCode: 'LINE', dossierCount: 0, percentage: 0 };
  });

  equipmentRatio = computed(() => {
    return this.ratioStats().find(r => r.groupCode === 'EQUIPMENT') || { groupName: 'Thiết bị', groupCode: 'EQUIPMENT', dossierCount: 0, percentage: 0 };
  });

  donutConicGradient = computed(() => {
    this.filterVersion();
    this.ratioStats();
    this.appliedObjectType();

    const station = this.showStationRatio() ? Number(this.stationRatio().dossierCount) || 0 : 0;
    const line = this.showLineRatio() ? Number(this.lineRatio().dossierCount) || 0 : 0;
    const equipment = this.showEquipmentRatio() ? Number(this.equipmentRatio().dossierCount) || 0 : 0;
    const total = station + line + equipment;

    if (total <= 0) {
      return 'conic-gradient(#e2e8f0 0% 100%)';
    }

    const segments: string[] = [];
    let cursor = 0;

    const addSegment = (count: number, color: string) => {
      if (count <= 0) return;
      const end = cursor + (count / total) * 100;
      segments.push(`${color} ${cursor}% ${end}%`);
      cursor = end;
    };

    addSegment(station, '#1d6bf3');
    addSegment(line, '#10b981');
    addSegment(equipment, '#f59e0b');

    return segments.length > 0
      ? `conic-gradient(${segments.join(', ')})`
      : 'conic-gradient(#e2e8f0 0% 100%)';
  });

  showStationRatio = computed(() => {
    const t = this.appliedObjectType();
    return t == null || t === 0 || t === 1;
  });

  showLineRatio = computed(() => {
    const t = this.appliedObjectType();
    return t == null || t === 0 || t === 2;
  });

  showEquipmentRatio = computed(() => {
    const t = this.appliedObjectType();
    return t == null || t === 0 || t === 3;
  });

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
