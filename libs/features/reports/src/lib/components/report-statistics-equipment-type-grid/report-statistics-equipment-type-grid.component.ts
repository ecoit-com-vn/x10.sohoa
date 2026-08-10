import {
  Component,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { EcoPaginatorComponent } from '@sohoa.frontend/shared/layout';
import { ReportStatisticsEquipmentTypeGridConfig } from '../../data-access/report-statistics.config';
import {
  ReportStatisticsEquipmentTypeGridItem,
  ReportStatisticsService
} from '../../data-access/report-statistics.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-report-statistics-equipment-type-grid',
  standalone: true,
  imports: [CommonModule, EcoPaginatorComponent],
  templateUrl: './report-statistics-equipment-type-grid.component.html',
  styleUrl: './report-statistics-equipment-type-grid.component.scss'
})
export class ReportStatisticsEquipmentTypeGridComponent {
  private statisticsService = inject(ReportStatisticsService);

  gridConfig = input.required<ReportStatisticsEquipmentTypeGridConfig>();
  filter = input<Record<string, string | number | string[] | null | undefined>>({});
  active = input(false);
  filterVersion = input(0);

  items = signal<ReportStatisticsEquipmentTypeGridItem[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  currentPage = signal(1);
  pageSize = signal(10);

  constructor() {
    effect(() => {
      const cfg = this.gridConfig();
      const isActive = this.active();
      if (!cfg || !isActive) return;

      void this.filterVersion();
      void this.currentPage();
      void this.pageSize();

      queueMicrotask(() => this.loadData());
    });
  }

  onPageChange(event: { first?: number; rows?: number }): void {
    const rows = Number(event.rows) || this.pageSize();
    const first = Number(event.first) || 0;
    this.pageSize.set(rows);
    this.currentPage.set(Math.floor(first / rows) + 1);
  }

  resetPagination(): void {
    this.currentPage.set(1);
  }

  reload(): void {
    this.loadData();
  }

  private loadData(): void {
    const cfg = this.gridConfig();
    if (!cfg) return;

    this.loading.set(true);
    this.statisticsService
      .getEquipmentTypeGrid(cfg.gridSegment, {
        ...this.filter(),
        page: this.currentPage(),
        pageSize: this.pageSize()
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          this.items.set(res?.items || []);
          this.totalCount.set(res?.totalCount || 0);
        },
        error: (err) => {
          console.error('Lỗi tải lưới hồ sơ theo loại thiết bị:', err);
          this.items.set([]);
          this.totalCount.set(0);
        }
      });
  }
}
