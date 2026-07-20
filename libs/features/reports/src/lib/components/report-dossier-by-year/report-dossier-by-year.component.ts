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
import { WfBreadcrumbComponent, EcoInputTreeSelectComponent } from '@sohoa.frontend/shared/layout';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  ReportDossierByYearService,
  UnitLookupItem,
  ObjectTypeLookupItem,
  DossierByYearFilter,
  DossierByYearChartStat,
  DossierByYearRatioStat
} from '../../data-access/report-dossier-by-year.service';
import { REPORT_STATISTICS_DOSSIER_LIST_CONFIGS, REPORT_STATISTICS_STATION_GRID_CONFIGS } from '../../data-access/report-statistics.config';
import { ReportStatisticsDossierListComponent } from '../report-statistics-dossier-list/report-statistics-dossier-list.component';
import { ReportStatisticsStationGridComponent } from '../report-statistics-station-grid/report-statistics-station-grid.component';
import { finalize, forkJoin } from 'rxjs';

export type MainTabMode = 'stats' | 'list';

interface OrgTreeNode {
  id: number;
  name: string;
  code?: string;
  parentId: number | null;
  children: OrgTreeNode[];
}

@Component({
  selector: 'app-report-dossier-by-year',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    SelectModule,
    TooltipModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    ReportStatisticsDossierListComponent,
    ReportStatisticsStationGridComponent
  ],
  providers: [MessageService],
  templateUrl: './report-dossier-by-year.component.html',
  styleUrls: ['./report-dossier-by-year.component.scss']
})
export class ReportDossierByYearComponent implements OnInit, AfterViewInit {
  private reportService = inject(ReportDossierByYearService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private router = inject(Router);

  @ViewChild(ReportStatisticsDossierListComponent)
  dossierList?: ReportStatisticsDossierListComponent;

  @ViewChild(ReportStatisticsStationGridComponent)
  stationGrid?: ReportStatisticsStationGridComponent;

  readonly dossierListConfig = REPORT_STATISTICS_DOSSIER_LIST_CONFIGS.DOSSIER_BY_YEAR;
  readonly stationGridConfig = REPORT_STATISTICS_STATION_GRID_CONFIGS.DOSSIER_BY_YEAR;
  filterVersion = signal(0);

  reportFilter = computed(() => ({
    unitId: this.selectedUnitId(),
    objectType: this.selectedObjectType(),
    year: this.selectedYear()
  }));

  // Lookups
  units = signal<UnitLookupItem[]>([]);
  objectTypes = signal<ObjectTypeLookupItem[]>([]);
  years = signal<number[]>([]);

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

  // Filter state
  selectedUnitId = signal<number | null>(null);
  selectedObjectType = signal<number | null>(0);
  selectedYear = signal<number>(new Date().getFullYear());

  // Active Tab: 'stats' (Báo cáo thống kê) | 'list' (Danh sách hồ sơ)
  activeTab = signal<MainTabMode>('stats');
  loading = signal<boolean>(false);
  exporting = signal<boolean>(false);

  // Data states
  chartStats = signal<DossierByYearChartStat[]>([]);
  ratioStats = signal<DossierByYearRatioStat[]>([]);

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

    this.reportService.getYearsLookup().subscribe({
      next: (years) => {
        this.years.set(years || [new Date().getFullYear()]);
        if (years && years.length > 0) {
          this.selectedYear.set(years[0]);
        }
        this.loadStatsData();
      },
      error: (err) => {
        console.error('Lỗi tải danh sách năm:', err);
        this.years.set([new Date().getFullYear()]);
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
    const filter: DossierByYearFilter = {
      unitId: this.selectedUnitId(),
      objectType: this.selectedObjectType(),
      year: this.selectedYear()
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
    const filter: DossierByYearFilter = {
      unitId: this.selectedUnitId(),
      objectType: this.selectedObjectType(),
      year: this.selectedYear()
    };

    this.reportService.exportExcel(filter)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `DanhSachHoSo_Nam_${this.selectedYear()}_${new Date().getTime()}.xlsx`;
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

  // --- Computed helpers for grouped Bar Chart & Donut Chart ---

  // Max value among all chart metrics for scaling Y-axis
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

  // Calculate pixel height for a bar (max 200px)
  getBarHeightPx(val: number): number {
    const max = this.maxChartValue();
    if (!max || max <= 0 || !val || val <= 0) return 0;
    const maxPx = 200;
    return Math.min(maxPx, Math.max(4, Math.round((val / max) * maxPx)));
  }

  // Total dossiers for Donut Chart
  totalDossierCount = computed(() => {
    return this.chartStats().reduce((sum, s) => sum + s.dossierCount, 0);
  });

  // Individual Group Stats
  stationStat = computed(() => {
    return this.chartStats().find(s => s.groupCode === 'STATION') || { groupName: 'Trạm biến áp', groupCode: 'STATION', dossierCount: 0, documentCount: 0, pageCount: 0 };
  });

  lineStat = computed(() => {
    return this.chartStats().find(s => s.groupCode === 'LINE') || { groupName: 'Đường dây', groupCode: 'LINE', dossierCount: 0, documentCount: 0, pageCount: 0 };
  });

  equipmentStat = computed(() => {
    return this.chartStats().find(s => s.groupCode === 'EQUIPMENT') || { groupName: 'Thiết bị', groupCode: 'EQUIPMENT', dossierCount: 0, documentCount: 0, pageCount: 0 };
  });

  // Individual Ratio Stats
  stationRatio = computed(() => {
    return this.ratioStats().find(r => r.groupCode === 'STATION') || { groupName: 'Trạm biến áp', groupCode: 'STATION', dossierCount: 0, percentage: 0 };
  });

  lineRatio = computed(() => {
    return this.ratioStats().find(r => r.groupCode === 'LINE') || { groupName: 'Đường dây', groupCode: 'LINE', dossierCount: 0, percentage: 0 };
  });

  equipmentRatio = computed(() => {
    return this.ratioStats().find(r => r.groupCode === 'EQUIPMENT') || { groupName: 'Thiết bị', groupCode: 'EQUIPMENT', dossierCount: 0, percentage: 0 };
  });

  // Conic gradient — chỉ vẽ segment có % > 0; tránh tô cam phần còn lại khi lọc Trạm/ĐZ
  donutConicGradient = computed(() => {
    // Phụ thuộc rõ filter + ratio để buộc vẽ lại vòng tròn khi Tìm kiếm
    this.filterVersion();
    this.ratioStats();
    this.selectedObjectType();

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

  /** Ẩn legend nhóm không thuộc filter loại đối tượng đang chọn. */
  showStationRatio = computed(() => {
    const t = this.selectedObjectType();
    return t == null || t === 0 || t === 1;
  });

  showLineRatio = computed(() => {
    const t = this.selectedObjectType();
    return t == null || t === 0 || t === 2;
  });

  showEquipmentRatio = computed(() => {
    const t = this.selectedObjectType();
    return t == null || t === 0 || t === 3;
  });

  private showError(msg: string, err: any): void {
    console.error(msg, err);
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi',
      detail: err?.error?.message || msg
    });
  }
}
