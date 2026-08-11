import {
  Component,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { EcoPaginatorComponent } from '@sohoa.frontend/shared/layout';
import { TableModule } from 'primeng/table';
import { ReportStatisticsBoxGridConfig } from '../../data-access/report-statistics.config';
import {
  ReportStatisticsBoxGridItem,
  ReportStatisticsService
} from '../../data-access/report-statistics.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-report-statistics-box-grid',
  standalone: true,
  imports: [CommonModule, TableModule, EcoPaginatorComponent],
  templateUrl: './report-statistics-box-grid.component.html',
  styleUrl: './report-statistics-box-grid.component.scss'
})
export class ReportStatisticsBoxGridComponent {
  private statisticsService = inject(ReportStatisticsService);

  gridConfig = input.required<ReportStatisticsBoxGridConfig>();
  filter = input<Record<string, string | number | string[] | null | undefined>>({});
  active = input(false);
  filterVersion = input(0);

  items = signal<ReportStatisticsBoxGridItem[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(10);

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

  private loadData(): void {
    const cfg = this.gridConfig();
    if (!cfg) return;

    this.loading.set(true);
    this.statisticsService
      .getBoxGrid(cfg.gridSegment, {
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
          console.error('Lỗi tải lưới hồ sơ theo hộp lưu trữ:', err);
          this.items.set([]);
          this.totalCount.set(0);
        }
      });
  }
}
