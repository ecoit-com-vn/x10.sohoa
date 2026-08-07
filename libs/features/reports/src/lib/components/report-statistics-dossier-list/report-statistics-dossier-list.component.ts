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
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { BhsCatalogColumn } from '@sohoa.frontend/features/dossier-management';
import { ReportStatisticsDossierListConfig } from '../../data-access/report-statistics.config';
import {
  ReportStatisticsDossierListItem,
  ReportStatisticsService
} from '../../data-access/report-statistics.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-report-statistics-dossier-list',
  standalone: true,
  imports: [CommonModule, TableModule],
  templateUrl: './report-statistics-dossier-list.component.html',
  styleUrl: './report-statistics-dossier-list.component.scss'
})
export class ReportStatisticsDossierListComponent implements OnInit {
  private statisticsService = inject(ReportStatisticsService);
  private router = inject(Router);

  /** Cấu hình báo cáo */
  listConfig = input.required<ReportStatisticsDossierListConfig>();
  /** Filter từ màn cha */
  filter = input<Record<string, string | number | string[] | null | undefined>>({});
  /** Tab đang active */
  active = input(false);
  /** Tăng khi bấm Tìm kiếm */
  filterVersion = input(0);

  bhsColumns = signal<BhsCatalogColumn[]>([]);
  items = signal<ReportStatisticsDossierListItem[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(10);

  fixedColumns = computed(() => this.listConfig().columnMode === 'fixed-dossier');
  tableColSpan = computed(() => this.fixedColumns() ? 6 : this.bhsColumns().length + 4);

  constructor() {
    effect(() => {
      const cfg = this.listConfig();
      const isActive = this.active();
      if (!cfg || !isActive) return;

      void this.filterVersion();
      void this.page();
      void this.pageSize();

      queueMicrotask(() => this.loadData());
    });
  }

  ngOnInit(): void {
    if (this.fixedColumns()) return;

    this.statisticsService.getBhsColumns().subscribe({
      next: (cols) => this.bhsColumns.set(cols || []),
      error: (err) => console.error('Lỗi tải cột BHS:', err)
    });
  }

  onPageChange(event: { first: number; rows: number }): void {
    const newPage = Math.floor(event.first / event.rows) + 1;
    this.page.set(newPage);
    this.pageSize.set(event.rows);
  }

  resetPagination(): void {
    this.page.set(1);
  }

  reload(): void {
    this.loadData();
  }

  getCatalogValue(item: ReportStatisticsDossierListItem, col: BhsCatalogColumn): string {
    const data = item.catalogData ?? {};
    const value = data[col.key] ?? data[col.code];
    return value != null && String(value).trim() !== '' ? String(value) : '-';
  }

  openDetail(item: ReportStatisticsDossierListItem): void {
    if (!item.dossierId) return;
    const url = this.router.serializeUrl(
      this.router.createUrlTree(['/search/dossier/detail', item.dossierId], { queryParams: { from: 'report' } })
    );
    window.open(`/#${url}`, '_blank');
  }

  private loadData(): void {
    const cfg = this.listConfig();
    if (!cfg) return;

    this.loading.set(true);
    this.statisticsService
      .getDossierList(cfg.listSegment, {
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
          console.error('Lỗi tải danh sách hồ sơ báo cáo:', err);
          this.items.set([]);
          this.totalCount.set(0);
        }
      });
  }
}
