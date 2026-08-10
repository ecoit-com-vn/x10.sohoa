import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { forkJoin, Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '@env/environment';
import { AuditLogService } from '@sohoa.frontend/shared/core';

interface ActivityLog {
  id: string;
  action: string;
  user: string;
  time: string;
  status: 'success' | 'warning' | 'info';
  detail: string;
}

interface RecentDossier {
  id: string;
  code: string;
  title: string;
  station: string;
  documentCount: number;
  handler: string;
  statusName: string;
}

interface DossierListItemDto {
  id: string;
  infrastructureName?: string;
  dossierTypeName?: string;
  documentCount?: number;
  creator?: { name?: string; username?: string };
  currentHandlerName?: string;
  statusName?: string;
  catalogData?: Record<string, string>;
}

interface DossierTypeChartStatDto {
  dossierTypeCode?: string;
  dossierTypeName?: string;
  dossierCount?: number;
  documentCount?: number;
}

interface DossierGeneralInputChartStatDto {
  groupCode?: string;
  groupName?: string;
  dossierCount?: number;
  documentCount?: number;
}

interface TrendInfo {
  /** Trị tuyệt đối để hiển thị, ví dụ 12.3 nghĩa là "12.3%" */
  displayPercent: number;
  isUp: boolean;
}

const NEUTRAL_TREND: TrendInfo = { displayPercent: 0, isUp: true };

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private http = inject(HttpClient);
  private auditLogService = inject(AuditLogService);
  private cdr = inject(ChangeDetectorRef);

  loading = false;

  // Bốn thẻ chỉ số tổng quan — lấy từ dữ liệu hồ sơ/tài liệu/nhật ký thao tác thực tế
  totalDossiers = 0;
  totalDocuments = 0;
  searchCount = 0;
  downloadCount = 0;

  // % tăng/giảm thật so với tháng trước cho từng chỉ số
  dossierTrend: TrendInfo = NEUTRAL_TREND;
  documentTrend: TrendInfo = NEUTRAL_TREND;
  searchTrend: TrendInfo = NEUTRAL_TREND;
  downloadTrend: TrendInfo = NEUTRAL_TREND;

  weeklyData: Array<{
    day: string;
    dossierValue: number;
    dossierPercent: number;
    documentValue: number;
    documentPercent: number;
  }> = [];

  categories: Array<{ name: string; percent: number; value: string; color: string }> = [];

  recentActivities: ActivityLog[] = [];
  recentDossiers: RecentDossier[] = [];
  username = 'Người dùng';

  ngOnInit() {
    if (typeof window !== 'undefined') {
      const token = localStorage.getItem('token');
      if (token) {
        try {
          const payloadBase64 = token.split('.')[1];
          const payloadJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
          const payload = JSON.parse(payloadJson);
          this.username = payload.name || payload.unique_name || payload.username || payload.sub || 'Người dùng';
        } catch (e) {
          this.username = 'Người dùng';
        }
      }

      // Chỉ tải dữ liệu Dashboard trên môi trường client (nơi có localStorage chứa token JWT) để tránh lỗi 401 Unauthorized trong SSR
      this.loadDashboardData();
    }
  }

  loadDashboardData() {
    this.loading = true;

    this.loadRecentDossiers();
    this.loadDossierTypeStatistics();
    this.loadWeeklyTrend();
    this.loadUsageCounters();
    this.loadRecentActivities();
    this.loadMonthlyTrends();
  }

  /** So sánh giá trị hiện tại với kỳ trước, trả về % chênh lệch (trị tuyệt đối) + chiều tăng/giảm. */
  private computeTrend(current: number, previous: number): TrendInfo {
    if (previous <= 0) {
      return { displayPercent: current > 0 ? 100 : 0, isUp: true };
    }
    const percent = ((current - previous) / previous) * 100;
    return { displayPercent: Math.abs(Math.round(percent * 10) / 10), isUp: percent >= 0 };
  }

  /** Tổng hồ sơ + danh sách hồ sơ mới nhất (đã sắp xếp theo ngày tạo giảm dần ở backend, không lọc theo trạng thái/tab) */
  private loadRecentDossiers() {
    this.http
      .get<any>(`${environment.apiGatewayUrl}/api/v1/search/dossiers`, {
        params: { page: '1', pageSize: '5' }
      })
      .subscribe({
        next: (res) => {
          const rawItems: DossierListItemDto[] = res?.items ?? res?.Items ?? [];
          const seenIds = new Set<string>();
          const items = rawItems.filter((item) => {
            if (!item.id || seenIds.has(item.id)) return false;
            seenIds.add(item.id);
            return true;
          });
          this.totalDossiers = res?.totalCount ?? res?.TotalCount ?? 0;
          this.recentDossiers = items.map((item) => this.mapDossier(item));
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.warn('Không thể tải danh sách hồ sơ mới nhất:', err);
          this.totalDossiers = 0;
          this.recentDossiers = [];
          this.cdr.markForCheck();
        }
      });
  }

  /** Tìm giá trị trong catalogData theo tên cột hiển thị (vd. "Mã hồ sơ", "Tiêu đề hồ sơ") — không phân biệt hoa/thường. */
  private findCatalogValue(item: DossierListItemDto, label: string): string | undefined {
    const data = item.catalogData ?? {};
    const key = Object.keys(data).find((k) => k.trim().toLowerCase() === label.toLowerCase());
    return key ? data[key] || undefined : undefined;
  }

  private mapDossier(item: DossierListItemDto): RecentDossier {
    // Mã hồ sơ / tiêu đề hồ sơ thật do hệ thống sinh khi tạo hồ sơ, lưu trong catalogData (EAV) theo tên cột.
    const realCode = this.findCatalogValue(item, 'Mã hồ sơ');
    const realTitle = this.findCatalogValue(item, 'Tiêu đề hồ sơ');
    return {
      id: item.id,
      code: realCode || '—',
      title: realTitle || [item.dossierTypeName, item.infrastructureName].filter(Boolean).join(' — ') || 'Hồ sơ',
      station: item.infrastructureName || '—',
      documentCount: item.documentCount ?? 0,
      // Cùng logic với trang "Quản lý hồ sơ": ưu tiên người xử lý hiện tại, fallback người tạo
      handler: item.currentHandlerName || item.creator?.name || item.creator?.username || '—',
      statusName: item.statusName || '—'
    };
  }

  /** Tổng số tài liệu + thống kê theo loại hồ sơ */
  private loadDossierTypeStatistics() {
    this.http
      .get<DossierTypeChartStatDto[]>(
        `${environment.apiGatewayUrl}/api/v1/reports/statistics/dossier-by-dossier-type/chart-stats`
      )
      .subscribe({
        next: (stats) => {
          const list = Array.isArray(stats) ? stats : [];

          this.totalDocuments = list.reduce((sum, s) => sum + (s.documentCount ?? 0), 0);

          const totalDossierCount = list.reduce((sum, s) => sum + (s.dossierCount ?? 0), 0);
          const colors = ['#243b8f', '#ff6b1a', '#20bd68', '#6366f1', '#a855f7'];
          this.categories = [...list]
            .sort((a, b) => (b.dossierCount ?? 0) - (a.dossierCount ?? 0))
            .slice(0, 5)
            .map((s, idx) => {
              const count = s.dossierCount ?? 0;
              return {
                name: s.dossierTypeName || 'Loại hồ sơ',
                percent: totalDossierCount > 0 ? Math.round((count / totalDossierCount) * 100) : 0,
                value: `${count} hồ sơ`,
                color: colors[idx % colors.length]
              };
            });
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.warn('Không thể tải thống kê theo loại hồ sơ:', err);
          this.totalDocuments = 0;
          this.categories = [];
          this.cdr.markForCheck();
        }
      });
  }

  /** Số lượng hồ sơ/tài liệu tạo mới theo từng ngày trong 7 ngày qua */
  private loadWeeklyTrend() {
    const dayLabels = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
    const today = new Date();
    const days = Array.from({ length: 7 }, (_, i) => {
      const date = new Date(today);
      date.setDate(today.getDate() - (6 - i));
      const label = i === 6 ? `${dayLabels[date.getDay()]} (h.nay)` : dayLabels[date.getDay()];
      return { date, label };
    });

    const requests = days.map(({ date }) => {
      const from = new Date(date);
      from.setHours(0, 0, 0, 0);
      const to = new Date(date);
      to.setHours(23, 59, 59, 999);
      return this.http
        .get<DossierGeneralInputChartStatDto[]>(
          `${environment.apiGatewayUrl}/api/v1/reports/statistics/dossier-general-input/chart-stats`,
          { params: { fromDate: from.toISOString(), toDate: to.toISOString() } }
        )
        .pipe(catchError(() => of([] as DossierGeneralInputChartStatDto[])));
    });

    forkJoin(requests).subscribe((results) => {
      const totals = results.map((groups) => ({
        dossier: (groups ?? []).reduce((sum, g) => sum + (g.dossierCount ?? 0), 0),
        document: (groups ?? []).reduce((sum, g) => sum + (g.documentCount ?? 0), 0)
      }));

      const maxVal = Math.max(600, ...totals.map((t) => Math.max(t.dossier, t.document)));
      this.weeklyData = days.map((d, idx) => ({
        day: d.label,
        dossierValue: totals[idx].dossier,
        dossierPercent: Math.min(100, Number(((totals[idx].dossier / maxVal) * 100).toFixed(2))),
        documentValue: totals[idx].document,
        documentPercent: Math.min(100, Number(((totals[idx].document / maxVal) * 100).toFixed(2)))
      }));
      this.cdr.markForCheck();
    });
  }

  /** Lượt tra cứu — cộng dồn từ LOOKUP_VIEW_DAILY_COUNTS (ghi nhận mỗi khi mở hồ sơ/tài liệu qua tra cứu/tìm kiếm) */
  private loadUsageCounters() {
    this.http
      .get<any>(`${environment.apiGatewayUrl}/api/v1/reports/statistics/dossier-most-viewed/summary-stats`)
      .subscribe({
        next: (res) => {
          const station = res?.stationViewCount ?? res?.StationViewCount ?? 0;
          const line = res?.lineViewCount ?? res?.LineViewCount ?? 0;
          const doc = res?.documentViewCount ?? res?.DocumentViewCount ?? 0;
          this.searchCount = station + line + doc;

          // Suy ra tổng lượt tra cứu tháng trước từ % tăng trưởng thật của từng box (đã tính sẵn ở backend),
          // rồi tính % tăng/giảm tổng hợp — thay vì lấy trung bình cộng 3 số % (sai lệch vì khác quy mô).
          const stationGrowth = res?.stationGrowthPercent ?? res?.StationGrowthPercent;
          const lineGrowth = res?.lineGrowthPercent ?? res?.LineGrowthPercent;
          const docGrowth = res?.documentGrowthPercent ?? res?.DocumentGrowthPercent;
          const previousOf = (current: number, growthPercent: number | null | undefined) =>
            growthPercent == null || growthPercent <= -100 ? current : current / (1 + growthPercent / 100);
          const previousTotal = previousOf(station, stationGrowth) + previousOf(line, lineGrowth) + previousOf(doc, docGrowth);
          this.searchTrend = this.computeTrend(this.searchCount, previousTotal);
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.warn('Không thể tải lượt tra cứu:', err);
          this.searchCount = 0;
          this.searchTrend = NEUTRAL_TREND;
          this.cdr.markForCheck();
        }
      });

    // Lượt tải tài liệu — suy ra từ nhật ký thao tác hệ thống (audit log). Lưu ý: một số API tải file
    // cho phép truy cập ẩn danh (one-time token) nên không được audit, số này có thể thấp hơn thực tế.
    this.getDashboardDownloadCount()
      .subscribe({
        next: (totalCount) => {
          this.downloadCount = totalCount;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.warn('Không thể tải lượt tải tài liệu:', err);
          this.downloadCount = 0;
          this.cdr.markForCheck();
        }
      });
  }

  /** % tăng/giảm thật so với tháng trước cho tổng hồ sơ, tổng tài liệu và lượt tải tài liệu. */
  private loadMonthlyTrends() {
    const now = new Date();
    const startOfThisMonth = new Date(now.getFullYear(), now.getMonth(), 1, 0, 0, 0, 0);
    const endOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999);
    const startOfLastMonth = new Date(now.getFullYear(), now.getMonth() - 1, 1, 0, 0, 0, 0);
    const endOfLastMonth = new Date(now.getFullYear(), now.getMonth(), 0, 23, 59, 59, 999);

    const fetchDossierDocTotals = (from: Date, to: Date) =>
      this.http
        .get<DossierGeneralInputChartStatDto[]>(
          `${environment.apiGatewayUrl}/api/v1/reports/statistics/dossier-general-input/chart-stats`,
          { params: { fromDate: from.toISOString(), toDate: to.toISOString() } }
        )
        .pipe(
          map((groups) => ({
            dossier: (groups ?? []).reduce((sum, g) => sum + (g.dossierCount ?? 0), 0),
            document: (groups ?? []).reduce((sum, g) => sum + (g.documentCount ?? 0), 0)
          })),
          catchError(() => of({ dossier: 0, document: 0 }))
        );

    forkJoin([
      fetchDossierDocTotals(startOfThisMonth, endOfToday),
      fetchDossierDocTotals(startOfLastMonth, endOfLastMonth)
    ]).subscribe(([current, previous]) => {
      this.dossierTrend = this.computeTrend(current.dossier, previous.dossier);
      this.documentTrend = this.computeTrend(current.document, previous.document);
      this.cdr.markForCheck();
    });

    const fetchDownloadCount = (from: Date, to: Date) =>
      this.getDashboardDownloadCount(from, to).pipe(catchError(() => of(0)));

    forkJoin([
      fetchDownloadCount(startOfThisMonth, endOfToday),
      fetchDownloadCount(startOfLastMonth, endOfLastMonth)
    ]).subscribe(([current, previous]) => {
      this.downloadTrend = this.computeTrend(current, previous);
      this.cdr.markForCheck();
    });
  }

  private getDashboardDownloadCount(fromDate?: Date, toDate?: Date): Observable<number> {
    const params: Record<string, string> = {};
    if (fromDate) params['fromDate'] = fromDate.toISOString();
    if (toDate) params['toDate'] = toDate.toISOString();

    return this.http
      .get<any>(`${environment.apiGatewayUrl}/api/v1/audit-logs/dashboard/download-count`, { params })
      .pipe(map((res) => res?.totalCount ?? res?.TotalCount ?? 0));
  }

  private loadRecentActivities() {
    this.auditLogService.getRecent().subscribe({
      next: (res) => {
        const logs = res.logs || res.Logs || [];
        this.recentActivities = logs.slice(0, 5).map((item: any, idx: number) => ({
          id: item.id ? item.id.substring(0, 8) : `AL-${100 + idx}`,
          action: item.action || 'USER_ACTION',
          user: item.userName || item.user || 'system',
          time: new Date(item.timestamp || item.occurredAt || item['@timestamp'] || Date.now()).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
          status: item.statusCode && item.statusCode >= 400 ? 'warning' : 'success',
          detail: [item.serviceName, item.resourceName, item.details].filter(Boolean).join(' — ') || 'Thao tác hệ thống'
        }));
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.warn('Không thể tải audit logs:', err);
        this.recentActivities = [];
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }
}
