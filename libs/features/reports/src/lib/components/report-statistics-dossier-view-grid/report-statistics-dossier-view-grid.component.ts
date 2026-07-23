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
import { ReportStatisticsDossierViewGridConfig } from '../../data-access/report-statistics.config';
import {
  ReportStatisticsDossierViewGridItem,
  ReportStatisticsService
} from '../../data-access/report-statistics.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-report-statistics-dossier-view-grid',
  standalone: true,
  imports: [CommonModule, TableModule],
  templateUrl: './report-statistics-dossier-view-grid.component.html',
  styleUrl: './report-statistics-dossier-view-grid.component.scss'
})
export class ReportStatisticsDossierViewGridComponent implements OnInit {
  private statisticsService = inject(ReportStatisticsService);
  private router = inject(Router);

  gridConfig = input.required<ReportStatisticsDossierViewGridConfig>();
  filter = input<Record<string, string | number | string[] | null | undefined>>({});
  active = input(false);
  filterVersion = input(0);

  bhsColumns = signal<BhsCatalogColumn[]>([]);
  items = signal<ReportStatisticsDossierViewGridItem[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(10);

  tableColSpan = computed(() => this.bhsColumns().length + 3);

  constructor() {
    effect(() => {
      const cfg = this.gridConfig();
      const isActive = this.active();
      if (!cfg || !isActive) return;

      void this.filterVersion();
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

  getCatalogValue(item: ReportStatisticsDossierViewGridItem, col: BhsCatalogColumn): string {
    const data = item.catalogData ?? {};
    const value = data[col.key] ?? data[col.code];
    return value != null && String(value).trim() !== '' ? String(value) : '-';
  }

  openDetail(item: ReportStatisticsDossierViewGridItem): void {
    if (!item.dossierId) return;
    const url = this.router.serializeUrl(
      this.router.createUrlTree(['/dossier-management/publish', item.dossierId])
    );
    window.open(url, '_blank');
  }

  private loadData(): void {
    const cfg = this.gridConfig();
    if (!cfg) return;

    this.loading.set(true);
    this.statisticsService
      .getDossierViewGrid(cfg.gridSegment, {
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
          console.error('Lỗi tải lưới hồ sơ được tra cứu:', err);
          this.items.set([]);
          this.totalCount.set(0);
        }
      });
  }
}
