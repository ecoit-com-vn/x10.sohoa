// sohoa.frontend/libs/features/reports/src/lib/components/report-unit-publish/report-unit-publish.component.ts
import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MessageService, MenuItem } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { Menu, MenuModule } from 'primeng/menu';
import { environment } from '@env/environment';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';

export interface RoleLookupItem {
  id: number;
  code: string;
  name: string;
}

export interface ReportUnitPublishRow {
  id: number | null;
  reportId: number;
  reportCode: string;
  reportName: string;
  isPublish: boolean;
  roleIds: number[];
}

export type ReportUnitStatusFilter = 'ALL' | 'PUBLISHED' | 'DRAFT' | 'UNCONFIGURED';
type ReportUnitStatusKey = Exclude<ReportUnitStatusFilter, 'ALL'>;

@Component({
  selector: 'app-report-unit-publish',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DialogModule, MenuModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './report-unit-publish.component.html',
  styleUrls: ['./report-unit-publish.component.scss']
})
export class ReportUnitPublishComponent implements OnInit {
  reports = signal<ReportUnitPublishRow[]>([]);
  roles = signal<RoleLookupItem[]>([]);
  loading = signal<boolean>(false);
  saving = signal<boolean>(false);

  searchKeyword = signal<string>('');
  filterStatus = signal<ReportUnitStatusFilter>('ALL');

  filteredReports = computed(() => {
    const kw = this.searchKeyword().toLowerCase().trim();
    const status = this.filterStatus();

    return this.reports().filter((r) => {
      const matchKeyword =
        !kw || r.reportName.toLowerCase().includes(kw) || r.reportCode.toLowerCase().includes(kw);
      const matchStatus = status === 'ALL' || this.statusKey(r) === status;
      return matchKeyword && matchStatus;
    });
  });

  roleMapById = computed(() => {
    const map = new Map<number, RoleLookupItem>();
    this.roles().forEach((r) => map.set(r.id, r));
    return map;
  });

  // Menu thao tác (dấu ba chấm)
  actionMenuItems: MenuItem[] = [];

  // Dialog cấu hình vai trò
  displayConfigDialog = false;
  currentReport: ReportUnitPublishRow | null = null;
  selectedRoleIds = signal<Set<number>>(new Set());

  private http = inject(HttpClient);
  private messageService = inject(MessageService);
  public authService = inject(AuthService);
  private apiUrl = `${environment.apiGatewayUrl}/api/v1/reports/unit-publish`;

  ngOnInit(): void {
    this.loadReports();
    this.loadRoles();
  }

  loadReports(): void {
    this.loading.set(true);
    this.http
      .get<ReportUnitPublishRow[]>(`${this.apiUrl}/reports`)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => this.reports.set(data || []),
        error: (err) => {
          console.error('Lỗi tải danh sách nhóm báo cáo đơn vị:', err);
          const msg = err?.error?.message || 'Không thể tải danh sách báo cáo của đơn vị.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
        }
      });
  }

  loadRoles(): void {
    this.http.get<RoleLookupItem[]>(`${environment.apiGatewayUrl}/api/v1/roles/lookup`).subscribe({
      next: (data) => this.roles.set(data || []),
      error: (err) => console.error('Lỗi tải danh sách vai trò:', err)
    });
  }

  roleNamesOf(report: ReportUnitPublishRow): string {
    if (!report.roleIds?.length) return '';
    const map = this.roleMapById();
    return report.roleIds
      .map((id) => map.get(id)?.name)
      .filter((name): name is string => !!name)
      .join(', ');
  }

  statusKey(report: ReportUnitPublishRow): ReportUnitStatusKey {
    if (report.isPublish) return 'PUBLISHED';
    if (report.id) return 'DRAFT';
    return 'UNCONFIGURED';
  }

  statusLabel(report: ReportUnitPublishRow): string {
    switch (this.statusKey(report)) {
      case 'PUBLISHED':
        return 'Công bố';
      case 'DRAFT':
        return 'Lưu nháp';
      default:
        return 'Chưa cấu hình';
    }
  }

  statusColor(report: ReportUnitPublishRow): { bg: string; color: string } {
    switch (this.statusKey(report)) {
      case 'PUBLISHED':
        return { bg: '#f0fdf4', color: '#15803d' };
      case 'DRAFT':
        return { bg: '#fff7ed', color: '#c2410c' };
      default:
        return { bg: '#f1f5f9', color: '#64748b' };
    }
  }

  canEdit(): boolean {
    return this.authService.hasPermission('REPORT_UNIT_PUBLISH_EDIT') || this.authService.hasPermission('SUPER_ADMIN');
  }

  canRelease(): boolean {
    return this.authService.hasPermission('REPORT_UNIT_PUBLISH_RELEASE') || this.authService.hasPermission('SUPER_ADMIN');
  }

  openActionMenu(report: ReportUnitPublishRow, event: Event, menu: Menu): void {
    event.stopPropagation();
    const canEdit = this.canEdit();
    const canRelease = this.canRelease();
    const isDraft = this.statusKey(report) === 'DRAFT';

    this.actionMenuItems = [
      ...(canEdit || canRelease
        ? [
            {
              label: 'Cấu hình',
              icon: 'pi pi-cog text-sky-600',
              command: () => this.openConfigDialog(report)
            }
          ]
        : []),
      ...(canRelease && isDraft
        ? [
            {
              label: 'Công bố',
              icon: 'pi pi-cloud-upload text-green-600',
              command: () => this.quickPublish(report)
            }
          ]
        : [])
    ];
    menu.toggle(event);
  }

  /// Công bố nhanh cấu hình đang ở trạng thái Lưu nháp (không cần mở lại dialog).
  quickPublish(report: ReportUnitPublishRow): void {
    this.saving.set(true);
    this.http
      .post(`${this.apiUrl}/publish`, { reportId: report.reportId, isPublish: true, roleIds: report.roleIds || [] })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã công bố cấu hình báo cáo.' });
          this.loadReports();
        },
        error: (err) => {
          console.error('Công bố cấu hình nhóm báo cáo đơn vị lỗi:', err);
          const msg = err?.error?.message || 'Công bố thất bại.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
        }
      });
  }

  openConfigDialog(report: ReportUnitPublishRow): void {
    this.currentReport = report;
    this.selectedRoleIds.set(new Set(report.roleIds || []));
    this.displayConfigDialog = true;
  }

  hideConfigDialog(): void {
    this.displayConfigDialog = false;
    this.currentReport = null;
  }

  isRoleSelected(roleId: number): boolean {
    return this.selectedRoleIds().has(roleId);
  }

  toggleRole(roleId: number): void {
    const next = new Set(this.selectedRoleIds());
    if (next.has(roleId)) {
      next.delete(roleId);
    } else {
      next.add(roleId);
    }
    this.selectedRoleIds.set(next);
  }

  saveDraft(): void {
    this.persist(false);
  }

  publishConfig(): void {
    this.persist(true);
  }

  private persist(isPublish: boolean): void {
    if (!this.currentReport) return;
    const reportId = this.currentReport.reportId;
    const roleIds = Array.from(this.selectedRoleIds());

    this.saving.set(true);
    this.http
      .post(`${this.apiUrl}/${isPublish ? 'publish' : 'save'}`, { reportId, isPublish, roleIds })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: isPublish ? 'Đã công bố cấu hình báo cáo.' : 'Đã lưu nháp cấu hình báo cáo.'
          });
          this.hideConfigDialog();
          this.loadReports();
        },
        error: (err) => {
          console.error('Lưu cấu hình nhóm báo cáo đơn vị lỗi:', err);
          const msg = err?.error?.message || 'Lưu cấu hình thất bại.';
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
        }
      });
  }
}
