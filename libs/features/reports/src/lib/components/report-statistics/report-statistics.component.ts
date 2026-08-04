// sohoa.frontend/libs/features/reports/src/lib/components/report-statistics/report-statistics.component.ts
import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { InputTextModule } from 'primeng/inputtext';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';
import { WfBreadcrumbComponent, EcoPaginatorComponent, } from '@sohoa.frontend/shared/layout';

export interface UserReportItem {
  id: number;
  code: string;
  name: string;
  is_published: boolean;
  is_configured: boolean;
  status: 'published' | 'draft' | 'not_configured';
}

@Component({
  selector: 'app-report-statistics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    TooltipModule,
    TableModule,
    InputTextModule,
    WfBreadcrumbComponent,
    EcoPaginatorComponent,
  ],
  providers: [MessageService],
  templateUrl: './report-statistics.component.html',
  styleUrls: ['./report-statistics.component.scss'],
})
export class ReportStatisticsComponent implements OnInit {
  reports = signal<UserReportItem[]>([]);
  loading = signal<boolean>(false);
  searchKeyword = signal<string>('');
  appliedKeyword = signal<string>('');
  searchStatus = signal<string>('');
  appliedStatus = signal<string>('');

  currentPage = signal(1);
  pageSize = signal(10);

  filteredReports = computed(() => {
    const kw = this.appliedKeyword().toLowerCase().trim();
    const status = this.appliedStatus();
    return this.reports().filter(r => {
      const isKwMatch = !kw || r.name.toLowerCase().includes(kw) || r.code.toLowerCase().includes(kw);
      const isStatusMatch = !status || r.status === status;
      return isKwMatch && isStatusMatch;
    });
  });

  pagedReports = computed(() => {
    const first = (this.currentPage() - 1) * this.pageSize();
    return this.filteredReports().slice(first, first + this.pageSize());
  });

  private http = inject(HttpClient);
  private router = inject(Router);
  private messageService = inject(MessageService);
  public authService = inject(AuthService);
  private apiUrl = `${environment.apiGatewayUrl}/api/v1/reports/statistics`;

  // Ánh xạ mã báo cáo tĩnh tới đường dẫn route FE tương ứng
  private staticReportRouteMap: Record<string, string> = {
    REPORT_DOSSIER_BY_VOLTAGE_GRID: '/reports/dossier-by-voltage-grid',
    REPORT_DOSSIER_BY_EQUIPMENT: '/reports/dossier-by-equipment-type',
    REPORT_DOSSIER_BY_STATION: '/reports/dossier-by-station',
    REPORT_DOSSIER_BY_LINE: '/reports/dossier-by-line',
    REPORT_DOSSIER_BY_OPERATION_YEAR: '/reports/dossier-by-operation-year',
    REPORT_DOSSIER_BY_OPERATION_TIME: '/reports/dossier-by-operation-time',
    REPORT_DOSSIER_BY_MANUFACTURE_YEAR: '/reports/dossier-by-manufacture-year',
    REPORT_DOSSIER_BY_EQUIPMENT_STATUS: '/reports/dossier-by-equipment-status',
    REPORT_DOSSIER_GENERAL_INPUT: '/reports/dossier-general-input',
    REPORT_DOSSIER_MOST_VIEWED: '/reports/dossier-most-viewed',
    REPORT_DOSSIER_BY_YEAR: '/reports/dossier-by-year',
    REPORT_DOSSIER_BY_MONTH: '/reports/dossier-by-month',
    REPORT_DOSSIER_BY_ALLOCATION: '/reports/dossier-by-allocation',
    REPORT_DOSSIER_BY_DOSSIER_TYPE: '/reports/dossier-by-dossier-type',
    REPORT_DOSSIER_BY_SHELF: '/reports/dossier-by-shelf',
    REPORT_DOSSIER_BY_BOX: '/reports/dossier-by-box',
    REPORT_DOSSIER_BY_FLOOR: '/reports/dossier-by-floor',
    REPORT_DOSSIER_BY_DOCUMENT_TYPE: '/reports/dossier-by-document-type',
    REPORT_DOSSIER_BY_INPUT_OFFICER: '/reports/dossier-by-input-officer'
  };

  ngOnInit(): void {
    this.loadReports();
  }

  onSearch(): void {
    this.appliedKeyword.set(this.searchKeyword().trim());
    this.appliedStatus.set(this.searchStatus());
    this.currentPage.set(1);
  }

  onStatusFilterChange(status: string): void {
    this.searchStatus.set(status);
    this.onSearch();
  }

  onResetSearch(): void {
    this.searchKeyword.set('');
    this.appliedKeyword.set('');
    this.searchStatus.set('');
    this.appliedStatus.set('');
    this.currentPage.set(1);
  }

  onUnitPageChange(event: { first?: number; rows?: number }) {
    const rows = Number(event.rows) || this.pageSize();
    const first = Number(event.first) || 0;
    this.pageSize.set(rows);
    this.currentPage.set(Math.floor(first / rows) + 1);
  }

  loadReports(): void {
    this.loading.set(true);
    this.http
      .get<Omit<UserReportItem, 'status'>[]>(`${this.apiUrl}/my-reports`)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          const reportsWithStatus = (data || []).map(r => ({
            ...r,
            status: this.getReportStatus(r)
          }));
          this.reports.set(reportsWithStatus);
        },
        error: (err) => {
          console.error('Lỗi tải danh sách báo cáo:', err);
          const msg = err?.error?.message || 'Không thể tải danh sách báo cáo được phép xem.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
        }
      });
  }

  getReportStatus(report: Omit<UserReportItem, 'status'>): 'published' | 'draft' | 'not_configured' {
    if (report.is_published) {
      return 'published';
    }
    if (report.is_configured) {
      return 'draft';
    }
    return 'not_configured';
  }

  viewReport(report: UserReportItem): void {
    const route = this.staticReportRouteMap[report.code];
    if (route) {
      this.router.navigate([route]);
    } else {
      this.messageService.add({
        severity: 'info',
        summary: 'Thông báo',
        detail: `Tính năng xem chi tiết cho "${report.name}" đang được phát triển.`
      });
    }
  }
}

