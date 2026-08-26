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
import { TreeSelectModule } from 'primeng/treeselect';
import { TooltipModule } from 'primeng/tooltip';
import { TreeNode } from 'primeng/api';
import { WfBreadcrumbComponent, EcoInputTreeSelectComponent } from '@sohoa.frontend/shared/layout';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  ReportDossierByBoxService,
  BoxLookupItem,
  DossierByBoxFilter,
  DossierByBoxChartStat
} from '../../data-access/report-dossier-by-box.service';
import { UnitLookupItem } from '../../data-access/report-dossier-by-year.service';
import {
  REPORT_STATISTICS_DOSSIER_LIST_CONFIGS,
  REPORT_STATISTICS_BOX_GRID_CONFIGS
} from '../../data-access/report-statistics.config';
import {
  buildYearOptions,
  yearToFilterParam,
  buildExportYearSuffix
} from '../../data-access/report-year-filter.util';
import { ReportStatisticsDossierListComponent } from '../report-statistics-dossier-list/report-statistics-dossier-list.component';
import { ReportStatisticsBoxGridComponent } from '../report-statistics-box-grid/report-statistics-box-grid.component';
import { finalize } from 'rxjs';
import type { MainTabMode } from '../report-dossier-by-year/report-dossier-by-year.component';

interface OrgTreeNode {
  id: number;
  name: string;
  code?: string;
  parentId: number | null;
  children: OrgTreeNode[];
}

@Component({
  selector: 'app-report-dossier-by-box',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    SelectModule,
    TreeSelectModule,
    TooltipModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    ReportStatisticsDossierListComponent,
    ReportStatisticsBoxGridComponent
  ],
  providers: [MessageService],
  templateUrl: './report-dossier-by-box.component.html',
  styleUrls: [
    '../report-dossier-by-year/report-dossier-by-year.component.scss',
    './report-dossier-by-box.component.scss'
  ]
})
export class ReportDossierByBoxComponent implements OnInit, AfterViewInit {
  private reportService = inject(ReportDossierByBoxService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private router = inject(Router);

  @ViewChild(ReportStatisticsDossierListComponent)
  dossierList?: ReportStatisticsDossierListComponent;

  @ViewChild(ReportStatisticsBoxGridComponent)
  boxGrid?: ReportStatisticsBoxGridComponent;

  @ViewChild('chartBarsScroll') chartBarsScroll?: ElementRef<HTMLElement>;
  @ViewChild('chartRowsScroll') chartRowsScroll?: ElementRef<HTMLElement>;

  readonly dossierListConfig = REPORT_STATISTICS_DOSSIER_LIST_CONFIGS.DOSSIER_BY_BOX;
  readonly boxGridConfig = REPORT_STATISTICS_BOX_GRID_CONFIGS.DOSSIER_BY_BOX;
  filterVersion = signal(0);

  reportFilter = computed(() => ({
    unitId: this.selectedUnitId(),
    boxIds: this.selectedBoxIds()
      ? Object.keys(this.selectedBoxIds()).filter(key => !key.startsWith('shelf-') && !key.startsWith('floor-'))
      : [],
    ...yearToFilterParam(this.selectedYear())
  }));

  units = signal<UnitLookupItem[]>([]);
  boxOptions = signal<TreeNode[]>([]);
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
  selectedBoxIds = signal<any>(null); // Changed type to any for p-treeSelect object
  selectedYear = signal<number>(new Date().getFullYear());

  activeTab = signal<MainTabMode>('stats');
  loading = signal<boolean>(false);
  exporting = signal<boolean>(false);

  chartStats = signal<DossierByBoxChartStat[]>([]);

  ngOnInit(): void {
    this.loadLookups();
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => {
      if (this.activeTab() === 'stats') {
        this.boxGrid?.reload();
      }
    });
  }

  loadLookups(): void {
    this.loading.set(true);

    this.reportService.getUnitsLookup(1).subscribe({
      next: (units) => {
        this.units.set(units || []);
        this.applyDefaultUnitFilter();
        this.loadBoxOptions();
        this.loadYearsLookup();
      },
      error: (err) => {
        console.error('Lỗi tải danh sách đơn vị:', err);
        this.loadBoxOptions();
        this.loadYearsLookup();
      }
    });
  }

  onUnitChange(unitId: number | null): void {
    this.selectedUnitId.set(unitId);
    this.selectedBoxIds.set(null);
    this.loadBoxOptions();
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
    this.boxGrid?.resetPagination();
    this.loadStatsData();
  }

  onResetSearch(): void {
    this.selectedBoxIds.set(null); // Reset for p-treeSelect
    this.selectedUnitId.set(null);

    this.applyDefaultUnitFilter();

    const currentYear = new Date().getFullYear();
    const years = this.years();

    this.selectedYear.set(
      years.includes(currentYear)
        ? currentYear
        : years[0] ?? currentYear
    );

    this.dossierList?.resetPagination();
    this.boxGrid?.resetPagination();

    this.filterVersion.update((v) => v + 1);

    this.loadStatsData();
  }

  switchTab(tab: MainTabMode): void {
    this.activeTab.set(tab);
    queueMicrotask(() => {
      if (tab === 'list') {
        this.dossierList?.reload();
      } else {
        this.boxGrid?.reload();
      }
    });
  }

  loadStatsData(): void {
    const filter: DossierByBoxFilter = {
      unitId: this.selectedUnitId(),
      boxIds: this.selectedBoxIds()
        ? Object.keys(this.selectedBoxIds()).filter(key => !key.startsWith('shelf-') && !key.startsWith('floor-'))
        : [],
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
      this.boxGrid?.reload();
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
    const filter: DossierByBoxFilter = {
      unitId: this.selectedUnitId(),
      boxIds: this.selectedBoxIds()
        ? Object.keys(this.selectedBoxIds()).filter(key => !key.startsWith('shelf-') && !key.startsWith('floor-'))
        : [],
      ...yearToFilterParam(this.selectedYear())
    };

    this.reportService.exportExcel(filter)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `DanhSachHoSo_HopLuuTru_${buildExportYearSuffix(this.selectedYear())}_${new Date().getTime()}.xlsx`;
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

  getBoxLabel(stat: DossierByBoxChartStat): string {
    const name = stat.boxName?.trim();
    if (name) return name;
    const code = stat.boxCode?.trim();
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

  // New method to build tree nodes for boxes
  private buildBoxTreeNodes(boxes: BoxLookupItem[]): TreeNode[] {
    const shelfMap = new Map<string, TreeNode>(); // Key: shelfCode
    const floorMap = new Map<string, TreeNode>(); // Key: shelfCode_floorCode

    boxes.forEach(box => {
      if (!box.shelfCode || !box.floorCode) {
        // Handle boxes without shelf/floor codes if necessary, e.g., add them as root nodes or ignore
        // For now, we'll skip them if they don't fit the desired hierarchy
        return;
      }

      // Get or create Shelf Node
      let shelfNode = shelfMap.get(box.shelfCode);
      if (!shelfNode) {
        shelfNode = {
          key: `shelf-${box.shelfCode}`, // Unique key for shelf
          label: box.shelfName ? `${box.shelfCode} - ${box.shelfName}` : box.shelfCode,
          data: { type: 'shelf', code: box.shelfCode, name: box.shelfName },
          children: []
        };
        shelfMap.set(box.shelfCode, shelfNode);
      }

      // Get or create Floor Node
      const floorKey = `${box.shelfCode}-${box.floorCode}`;
      let floorNode = floorMap.get(floorKey);
      if (!floorNode) {
        floorNode = {
          key: `floor-${floorKey}`, // Unique key for floor
          label: box.floorName ? `${box.floorCode} - ${box.floorName}` : box.floorCode,
          data: { type: 'floor', code: box.floorCode, name: box.floorName },
          children: []
        };
        floorMap.set(floorKey, floorNode);
        shelfNode.children?.push(floorNode); // Add floor to shelf
      }

      // Create Box Node
      const boxNode: TreeNode = {
        key: box.id, // Actual box ID for selection
        label: box.code ? `${box.code} - ${box.name}` : box.name,
        data: { type: 'box', id: box.id, code: box.code, name: box.name },
        icon: 'pi pi-box', // Optional icon
        leaf: true
      };
      floorNode.children?.push(boxNode); // Add box to floor
    });

    // Sort children for better presentation
    shelfMap.forEach(shelfNode => {
      shelfNode.children?.sort((a, b) => (a.label || '').localeCompare(b.label || ''));
      shelfNode.children?.forEach(floorNode => {
        floorNode.children?.sort((a, b) => (a.label || '').localeCompare(b.label || ''));
      });
    });

    const rootNodes = Array.from(shelfMap.values());
    rootNodes.sort((a, b) => (a.label || '').localeCompare(b.label || ''));
    return rootNodes;
  }

  // Modified loadBoxOptions to use the new tree builder
  private loadBoxOptions(): void {
    this.reportService.getBoxesLookup(this.selectedUnitId()).subscribe({
      next: (boxes) => {
        const treeNodes = this.buildBoxTreeNodes(boxes || []);
        this.boxOptions.set(treeNodes);
      },
      error: (err) => console.error('Lỗi tải hộp hồ sơ:', err)
    });
  }
}
