import {
  Component,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { EcoPaginatorComponent } from '@sohoa.frontend/shared/layout';
import { DossierDocumentEditDialogComponent } from '@sohoa.frontend/features/dossier-management';
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
  imports: [CommonModule, TableModule, EcoPaginatorComponent, DossierDocumentEditDialogComponent],
  templateUrl: './report-statistics-document-list.component.html',
  styleUrl: './report-statistics-document-list.component.scss'
})
export class ReportStatisticsDocumentListComponent {
  private statisticsService = inject(ReportStatisticsService);

  listConfig = input.required<ReportStatisticsDocumentListConfig>();
  filter = input<Record<string, string | number | string[] | null | undefined>>({});
  active = input(false);
  filterVersion = input(0);

  items = signal<ReportStatisticsDocumentListItem[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(10);

  // Popup xem chi tiết tài liệu — dùng chung component với màn /dossier-management/publish/:id.
  selectedDocument = signal<ReportStatisticsDocumentListItem | null>(null);
  showDocumentDetail = signal(false);

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

  onPaginatorPageChange(event: { first: number; rows: number; page: number; pageCount: number }): void {
    this.page.set(event.page + 1);
    this.pageSize.set(event.rows);
  }

  resetPagination(): void {
    this.page.set(1);
  }

  reload(): void {
    this.loadData();
  }

  openDossierDetail(item: ReportStatisticsDocumentListItem): void {
    if (!item.dossierId || !item.versionId) return;
    this.selectedDocument.set(item);
    this.showDocumentDetail.set(true);
  }

  onDocumentDetailVisibleChange(visible: boolean): void {
    this.showDocumentDetail.set(visible);
    if (!visible) {
      this.selectedDocument.set(null);
    }
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
