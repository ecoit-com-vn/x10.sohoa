import {
  Component,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ReportStatisticsDocumentListConfig } from '../../data-access/report-statistics.config';
import {
  ReportStatisticsDocumentListItem,
  ReportStatisticsService
} from '../../data-access/report-statistics.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-report-statistics-document-list',
  standalone: true,
  imports: [CommonModule, TableModule],
  templateUrl: './report-statistics-document-list.component.html',
  styleUrl: './report-statistics-document-list.component.scss'
})
export class ReportStatisticsDocumentListComponent {
  private statisticsService = inject(ReportStatisticsService);
  private router = inject(Router);

  listConfig = input.required<ReportStatisticsDocumentListConfig>();
  filter = input<Record<string, string | number | string[] | null | undefined>>({});
  active = input(false);
  filterVersion = input(0);

  items = signal<ReportStatisticsDocumentListItem[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(10);

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

  openDossierDetail(item: ReportStatisticsDocumentListItem): void {
    if (!item.dossierId) return;
    const url = this.router.serializeUrl(
      this.router.createUrlTree(['/dossier-management/publish', item.dossierId])
    );
    window.open(url, '_blank');
  }

  private loadData(): void {
    const cfg = this.listConfig();
    if (!cfg) return;

    this.loading.set(true);
    this.statisticsService
      .getDocumentList(cfg.listSegment, {
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
          console.error('Lỗi tải danh sách tài liệu báo cáo:', err);
          this.items.set([]);
          this.totalCount.set(0);
        }
      });
  }
}
