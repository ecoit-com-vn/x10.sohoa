import {
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
import { TreeNode } from 'primeng/api';
import { WfBreadcrumbComponent, EcoInputTreeSelectComponent, EcoInputDateComponent } from '@sohoa.frontend/shared/layout';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  ReportDossierMostViewedService,
  DossierMostViewedFilter,
  DossierMostViewedSummaryStats
} from '../../data-access/report-dossier-most-viewed.service';
import { ObjectTypeLookupItem, UnitLookupItem } from '../../data-access/report-dossier-by-year.service';
import { REPORT_STATISTICS_DOSSIER_VIEW_GRID_CONFIGS } from '../../data-access/report-statistics.config';
import { ReportStatisticsDossierViewGridComponent } from '../report-statistics-dossier-view-grid/report-statistics-dossier-view-grid.component';
import { finalize } from 'rxjs';

interface OrgTreeNode {
  id: number;
  name: string;
  code?: string;
  parentId: number | null;
  children: OrgTreeNode[];
}

function toDateOnlyParam(date: Date | null): string | null {
  if (!date) return null;
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

@Component({
  selector: 'app-report-dossier-most-viewed',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    SelectModule,
    WfBreadcrumbComponent,
    EcoInputTreeSelectComponent,
    EcoInputDateComponent,
    ReportStatisticsDossierViewGridComponent
  ],
  providers: [MessageService],
  templateUrl: './report-dossier-most-viewed.component.html',
  styleUrls: [
    '../report-dossier-by-year/report-dossier-by-year.component.scss',
    './report-dossier-most-viewed.component.scss'
  ]
})
export class ReportDossierMostViewedComponent implements OnInit {
  private reportService = inject(ReportDossierMostViewedService);
  private messageService = inject(MessageService);
  private authService = inject(AuthService);
  private router = inject(Router);

  @ViewChild(ReportStatisticsDossierViewGridComponent)
  dossierViewGrid?: ReportStatisticsDossierViewGridComponent;

  readonly gridConfig = REPORT_STATISTICS_DOSSIER_VIEW_GRID_CONFIGS.DOSSIER_MOST_VIEWED;
  filterVersion = signal(0);

  reportFilter = computed(() => ({
    unitId: this.selectedUnitId(),
    objectType: this.selectedObjectType(),
    fromDate: toDateOnlyParam(this.selectedFromDate()),
    toDate: toDateOnlyParam(this.selectedToDate())
  }));

  units = signal<UnitLookupItem[]>([]);
  objectTypes = signal<ObjectTypeLookupItem[]>([]);

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
  selectedFromDate = signal<Date | null>(null);
  selectedToDate = signal<Date | null>(null);

  loading = signal<boolean>(false);
  exporting = signal<boolean>(false);

  summaryStats = signal<DossierMostViewedSummaryStats | null>(null);

  ngOnInit(): void {
    this.loadLookups();
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

    this.loadStatsData();
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

  onFromDateChange(date: Date | null): void {
    this.selectedFromDate.set(date);
  }

  onToDateChange(date: Date | null): void {
    this.selectedToDate.set(date);
  }

  onFilter(): void {
    this.filterVersion.update((v) => v + 1);
    this.dossierViewGrid?.resetPagination();
    this.loadStatsData();
  }

  loadStatsData(): void {
    const filter: DossierMostViewedFilter = {
      unitId: this.selectedUnitId(),
      objectType: this.selectedObjectType(),
      fromDate: toDateOnlyParam(this.selectedFromDate()),
      toDate: toDateOnlyParam(this.selectedToDate())
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

    queueMicrotask(() => this.dossierViewGrid?.reload());
  }

  exportExcel(): void {
    this.exporting.set(true);
    const filter: DossierMostViewedFilter = {
      unitId: this.selectedUnitId(),
      objectType: this.selectedObjectType(),
      fromDate: toDateOnlyParam(this.selectedFromDate()),
      toDate: toDateOnlyParam(this.selectedToDate())
    };

    this.reportService.exportExcel(filter)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `HoSoTraCuuNhieuNhat_${new Date().getTime()}.xlsx`;
          a.click();
          window.URL.revokeObjectURL(url);
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xuất dữ liệu ra Excel.' });
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
