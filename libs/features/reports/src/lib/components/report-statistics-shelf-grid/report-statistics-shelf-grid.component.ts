import {
  Component,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { EcoPaginatorComponent } from '@sohoa.frontend/shared/layout';
import { ReportStatisticsShelfGridConfig } from '../../data-access/report-statistics.config';
import {
  ReportStatisticsShelfGridItem,
  ReportStatisticsService
} from '../../data-access/report-statistics.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-report-statistics-shelf-grid',
  standalone: true,
  imports: [CommonModule, TableModule, EcoPaginatorComponent],
  templateUrl: './report-statistics-shelf-grid.component.html',
  styleUrl: './report-statistics-shelf-grid.component.scss'
})
export class ReportStatisticsShelfGridComponent {
  private statisticsService = inject(ReportStatisticsService);

  gridConfig = input.required<ReportStatisticsShelfGridConfig>();
  filter = input<Record<string, string | number | string[] | null | undefined>>({});
  active = input(false);
  filterVersion = input(0);

  items = signal<ReportStatisticsShelfGridItem[]>([]);
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
      .getShelfGrid(cfg.gridSegment, {
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
          console.error('Lỗi tải lưới hồ sơ theo kệ lưu trữ:', err);
          this.items.set([]);
          this.totalCount.set(0);
        }
      });
  }
}
