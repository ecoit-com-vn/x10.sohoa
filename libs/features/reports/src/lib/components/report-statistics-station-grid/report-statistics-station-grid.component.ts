import {
  Component,
  OnInit,
  computed,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { BhsCatalogColumn } from '@sohoa.frontend/features/dossier-management';
import { ReportStatisticsStationGridConfig } from '../../data-access/report-statistics.config';
import {
  ReportStatisticsStationGridItem,
  ReportStatisticsService
} from '../../data-access/report-statistics.service';
import { finalize } from 'rxjs';

/** View 3 gom theo infrastructure — không dùng nhãn BHS của hồ sơ. */
const STATION_GRID_LABELS = ['Mã trạm/đường dây', 'Tên trạm/đường dây'] as const;

@Component({
  selector: 'app-report-statistics-station-grid',
  standalone: true,
  imports: [CommonModule, TableModule],
  templateUrl: './report-statistics-station-grid.component.html',
  styleUrl: './report-statistics-station-grid.component.scss'
})
export class ReportStatisticsStationGridComponent implements OnInit {
  private statisticsService = inject(ReportStatisticsService);

  gridConfig = input.required<ReportStatisticsStationGridConfig>();
  filter = input<Record<string, string | number | null | undefined>>({});
  active = input(false);
  filterVersion = input(0);

  bhsColumns = signal<BhsCatalogColumn[]>([]);
  items = signal<ReportStatisticsStationGridItem[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(10);

  tableColSpan = computed(() => this.displayColumns().length + 5);

  displayColumns = computed((): BhsCatalogColumn[] => {
    const cols = this.bhsColumns();
    if (cols.length === 0) {
      return [
        { key: 'code', code: 'code', label: STATION_GRID_LABELS[0], priority: 1 },
        { key: 'name', code: 'name', label: STATION_GRID_LABELS[1], priority: 2 }
      ];
    }
    return cols.map((col, index) => ({
      ...col,
      label: STATION_GRID_LABELS[index] ?? col.label
    }));
  });

  constructor() {
    effect(() => {
      const cfg = this.gridConfig();
      const isActive = this.active();
      if (!cfg || !isActive) return;

      void this.filterVersion();
      void this.filter();
      void this.page();
      void this.pageSize();

      queueMicrotask(() => this.loadData());
    });
  }

  ngOnInit(): void {
    this.statisticsService.getBhsColumns().subscribe({
      next: (cols) => this.bhsColumns.set(cols || []),
      error: (err) => console.error('Lỗi tải cột BHS:', err)
    });
  }

  onPageChange(event: { first: number; rows: number }): void {
    this.page.set(Math.floor(event.first / event.rows) + 1);
    this.pageSize.set(event.rows);
  }

  resetPagination(): void {
    this.page.set(1);
  }

  reload(): void {
    this.loadData();
  }

  getCatalogValue(item: ReportStatisticsStationGridItem, col: BhsCatalogColumn): string {
    const data = item.catalogData ?? {};
    const value = data[col.key] ?? data[col.code];
    return value != null && String(value).trim() !== '' ? String(value) : '-';
  }

  private loadData(): void {
    const cfg = this.gridConfig();
    if (!cfg) return;

    this.loading.set(true);
    this.statisticsService
      .getStationGrid(cfg.gridSegment, {
        ...this.filter(),
        page: this.page(),
        pageSize: this.pageSize()
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          this.items.set(res?.items || []);
          this.totalCount.set(res?.totalCount || 0);
        },
        error: (err) => {
          console.error('Lỗi tải lưới hồ sơ theo trạm:', err);
          this.items.set([]);
          this.totalCount.set(0);
        }
      });
  }
}
