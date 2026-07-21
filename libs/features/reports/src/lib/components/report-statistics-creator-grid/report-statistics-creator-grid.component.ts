import {
  Component,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ReportStatisticsCreatorGridConfig } from '../../data-access/report-statistics.config';
import {
  ReportStatisticsCreatorGridItem,
  ReportStatisticsService
} from '../../data-access/report-statistics.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-report-statistics-creator-grid',
  standalone: true,
  imports: [CommonModule, TableModule],
  templateUrl: './report-statistics-creator-grid.component.html',
  styleUrl: './report-statistics-creator-grid.component.scss'
})
export class ReportStatisticsCreatorGridComponent {
  private statisticsService = inject(ReportStatisticsService);

  gridConfig = input.required<ReportStatisticsCreatorGridConfig>();
  filter = input<Record<string, string | number | null | undefined>>({});
  active = input(false);
  filterVersion = input(0);

  items = signal<ReportStatisticsCreatorGridItem[]>([]);
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
      .getCreatorGrid(cfg.gridSegment, {
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
          console.error('Lỗi tải lưới hồ sơ theo người tạo:', err);
          this.items.set([]);
          this.totalCount.set(0);
        }
      });
  }
}
