import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { finalize } from 'rxjs';
import { environment } from '@env/environment';
import { EcoPaginatorComponent, WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';

@Component({
  selector: 'app-external-api-key-history',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, WfBreadcrumbComponent, EcoPaginatorComponent],
  providers: [MessageService],
  templateUrl: './external-api-key-history.component.html',
  styleUrl: './external-api-key-history.component.scss',
})
export class ExternalApiKeyHistoryComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly messageService = inject(MessageService);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/external-api-keys/call-logs`;

  logs = signal<any[]>([]);
  loading = signal(false);
  currentPage = signal(1);
  pageSize = signal(10);
  totalCount = signal(0);

  keyName = signal('');
  fromDate = signal<string | null>(null);
  toDate = signal<string | null>(null);

  appliedKeyName = signal('');
  appliedFromDate = signal<string | null>(null);
  appliedToDate = signal<string | null>(null);

  ngOnInit(): void {
    this.loadLogs();
  }

  onSearch(): void {
    this.appliedKeyName.set(this.keyName().trim());
    this.appliedFromDate.set(this.fromDate() || null);
    this.appliedToDate.set(this.toDate() || null);
    this.currentPage.set(1);
    this.loadLogs();
  }

  onReset(): void {
    this.keyName.set('');
    this.fromDate.set(null);
    this.toDate.set(null);
    this.onSearch();
  }

  loadLogs(): void {
    this.loading.set(true);

    const params: Record<string, string> = {
      page: String(this.currentPage()),
      pageSize: String(this.pageSize()),
    };
    if (this.appliedKeyName()) params['keyName'] = this.appliedKeyName();
    if (this.appliedFromDate()) params['fromDate'] = this.toIsoStartOfDay(this.appliedFromDate()!);
    if (this.appliedToDate()) params['toDate'] = this.toIsoEndOfDay(this.appliedToDate()!);

    this.http.get<any>(this.apiUrl, { params }).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: response => {
        const items = response?.items ?? response?.Items ?? (Array.isArray(response) ? response : []);
        const total = response?.totalCount ?? response?.TotalCount ?? response?.total ?? items.length;
        this.logs.set(items);
        this.totalCount.set(total);
      },
      error: error => this.showError(error, 'Không thể tải lịch sử đồng bộ API.'),
    });
  }

  goToPage(page: string | number): void {
    const requestedPage = Number(page);
    if (!Number.isInteger(requestedPage) || requestedPage < 1) return;
    this.currentPage.set(requestedPage);
    this.loadLogs();
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize.set(pageSize);
    this.currentPage.set(1);
    this.loadLogs();
  }

  getLogKeyName(log: any): string {
    return log?.keyName || log?.apiKeyName || log?.KeyName || '---';
  }

  getLogHttpMethod(log: any): string {
    return log?.httpMethod || log?.method || log?.HttpMethod || '---';
  }

  getLogEndpoint(log: any): string {
    return log?.requestPath || log?.endpoint || log?.path || log?.RequestPath || '---';
  }

  getLogStatusCode(log: any): number | string {
    return log?.statusCode ?? log?.responseStatusCode ?? log?.StatusCode ?? '---';
  }

  getLogCalledAt(log: any): string {
    return this.formatDate(log?.calledAt || log?.createdAt || log?.CalledAt || log?.CreatedAt);
  }

  getLogIpAddress(log: any): string {
    return log?.ipAddress || log?.clientIp || log?.IpAddress || '---';
  }

  getLogDuration(log: any): string {
    const duration = log?.durationMs ?? log?.duration ?? log?.DurationMs;
    return duration === undefined || duration === null ? '---' : `${duration} ms`;
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '---';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString('vi-VN');
  }

  private toIsoStartOfDay(dateStr: string): string {
    const [y, m, d] = dateStr.split('-').map(Number);
    return new Date(y, m - 1, d, 0, 0, 0, 0).toISOString();
  }

  private toIsoEndOfDay(dateStr: string): string {
    const [y, m, d] = dateStr.split('-').map(Number);
    return new Date(y, m - 1, d, 23, 59, 59, 999).toISOString();
  }

  private showError(error: any, fallback: string): void {
    this.messageService.add({
      severity: 'error',
      summary: 'Lỗi',
      detail: error?.error?.message || fallback,
    });
  }
}
