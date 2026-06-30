import { Component, OnInit, signal, computed, inject, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { BhsCatalogColumn, DossierManagementService } from '../../data-access/dossier-management.service';
import { DossierPublishService } from '../../data-access/dossier-publish.service';
import { DossierListTab } from '../../utils/dossier-status.util';
import { AuthService } from '@sohoa.frontend/shared/core';
import { finalize } from 'rxjs';

type PublishTab = 'pending-publish' | 'published' | 'unpublished';

function tabLabel(tab: PublishTab): string {
  const labels: Record<PublishTab, string> = {
    'pending-publish': 'Chờ xuất bản',
    published: 'Đã xuất bản',
    unpublished: 'Hủy xuất bản',
  };
  return labels[tab];
}

@Component({
  selector: 'app-dossier-publish',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DialogModule],
  template: `
    <div class="wf-card">
      <div class="tab-bar">
        <button
          type="button"
          class="tab-item"
          *ngFor="let tab of tabs"
          [class.tab-active]="activeTab() === tab"
          (click)="selectTab(tab)">
          {{ tabLabel(tab) }}
          <span class="tab-badge" *ngIf="getTabBadgeCount(tab)">{{ getTabBadgeCount(tab) }}</span>
        </button>
      </div>

      <div class="list-toolbar">
        <div class="toolbar-left">
          <input
            type="text"
            class="wf-search-input"
            placeholder="Tìm kiếm theo thông tin hồ sơ..."
            [(ngModel)]="searchKeyword"
            (keyup.enter)="onSearch()"
          />
          <select class="wf-select" [(ngModel)]="filterGridTypeId" (change)="onSearch()">
            <option [ngValue]="null">-- Tất cả loại lưới điện --</option>
            <option *ngFor="let item of gridTypes()" [value]="item.id">{{ item.name }}</option>
          </select>
          <select class="wf-select" [(ngModel)]="filterInfrastructureId" (change)="onSearch()">
            <option [ngValue]="null">-- Tất cả trạm/đường dây --</option>
            <option *ngFor="let item of infrastructures()" [value]="item.id">{{ item.name }}</option>
          </select>
          <button (click)="onSearch()" class="btn-tim">
            <i class="pi pi-search"></i> Tìm
          </button>
        </div>
      </div>

      <div class="wf-table-wrap">
        <table class="wf-table">
          <thead>
            <tr>
              <th class="col-stt">STT</th>
              <th *ngFor="let col of bhsColumns()">{{ col.label }}</th>
              <th>Trạm / Đường dây</th>
              <th style="width: 100px; text-align: center;">Số tài liệu</th>
              <th class="col-hd">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            <ng-container *ngIf="loading()">
              <tr *ngFor="let r of [1,2,3,4,5]" class="skeleton-row">
                <td class="col-stt"><div class="skeleton-bar short" style="margin: 0 auto; width: 24px;"></div></td>
                <td *ngFor="let col of bhsColumns()"><div class="skeleton-bar"></div></td>
                <td><div class="skeleton-bar"></div></td>
                <td style="width: 100px;"><div class="skeleton-bar short" style="margin: 0 auto; width: 40px;"></div></td>
                <td class="col-hd"><div class="skeleton-bar short" style="margin-left: auto; width: 70px;"></div></td>
              </tr>
            </ng-container>

            <ng-container *ngIf="!loading()">
              <tr *ngIf="items().length === 0">
                <td [attr.colspan]="tableColSpan()" class="empty-row">
                  <i class="pi pi-inbox"></i>
                  <div>Không tìm thấy hồ sơ nào phù hợp.</div>
                </td>
              </tr>

              <tr *ngFor="let item of items(); let i = index">
                <td class="col-stt text-muted">{{ (currentPage() - 1) * pageSize() + i + 1 }}</td>
                <td *ngFor="let col of bhsColumns(); let first = first">
                  <b *ngIf="first" class="wf-name-link" (click)="viewDetail.emit(item.id)">{{ getCatalogValue(item, col) }}</b>
                  <span *ngIf="!first">{{ getCatalogValue(item, col) }}</span>
                </td>
                <td>
                  <div>{{ item.infrastructureName || '-' }}</div>
                  <div class="text-muted" style="font-size: 0.75rem;">{{ item.infrastructureCode }}</div>
                </td>
                <td class="text-center" style="width: 100px;">{{ item.documentCount ?? 0 }}</td>
                <td class="col-hd">
                  <div class="action-buttons-group">
                    <button (click)="viewDetail.emit(item.id)" class="act-btn act-assign" title="Xem chi tiết">
                      <i class="pi pi-eye"></i>
                    </button>

                    <button *ngIf="activeTab() === 'pending-publish' && authService.hasPermission('DOSSIER_PUBLISH_RELEASE')" 
                            (click)="requestAction('publish', item)" class="act-btn act-assign" title="Xuất bản" style="margin-left: 6px;">
                      <i class="pi pi-cloud-upload"></i>
                    </button>

                    <button *ngIf="activeTab() === 'published' && authService.hasPermission('DOSSIER_PUBLISH_RELEASE')" 
                            (click)="requestAction('unpublish', item)" class="act-btn act-delete" title="Hủy xuất bản" style="margin-left: 6px;">
                      <i class="pi pi-ban"></i>
                    </button>

                    <button *ngIf="activeTab() === 'unpublished' && authService.hasPermission('DOSSIER_PUBLISH_RELEASE')" 
                            (click)="requestAction('republish', item)" class="act-btn act-assign" title="Tái xuất bản" style="margin-left: 6px;">
                      <i class="pi pi-refresh"></i>
                    </button>
                  </div>
                </td>
              </tr>
            </ng-container>
          </tbody>
        </table>
      </div>

      <div class="table-footer" *ngIf="!loading()">
        <span class="record-count">Tổng số: <b>{{ totalCount() }}</b> hồ sơ.</span>
        <div class="pagination">
          <button class="page-btn" [disabled]="currentPage() === 1" (click)="changePage(currentPage() - 1)">
            <i class="pi pi-chevron-left"></i>
          </button>
          <span class="page-current">Trang {{ currentPage() }} / {{ totalPages() || 1 }}</span>
          <button class="page-btn" [disabled]="currentPage() >= totalPages() || totalPages() === 0" (click)="changePage(currentPage() + 1)">
            <i class="pi pi-chevron-right"></i>
          </button>
        </div>
      </div>
    </div>

    <!-- Confirm Action Dialog -->
    <p-dialog
      [visible]="showConfirmDialog()"
      (visibleChange)="$event ? null : onCancelAction()"
      [header]="confirmHeader()"
      [modal]="true"
      [style]="{ width: '420px' }"
      styleClass="evn-dialog-custom"
      [closable]="!actionSubmitting()">
      <div style="display: flex; align-items: flex-start; gap: 12px; padding: 8px 0 16px;">
        <i class="pi pi-exclamation-triangle" style="font-size: 1.8rem; color: #3b82f6;"></i>
        <div>
          <p style="margin: 0 0 6px 0; font-weight: 600; color: #1e293b;">{{ confirmTitle() }}</p>
          <p style="margin: 0; color: #64748b; font-size: 0.875rem;">
            Hồ sơ <b style="color: #1e293b;">{{ actionTargetLabel() }}</b> sẽ được thay đổi trạng thái xuất bản.
          </p>
        </div>
      </div>
      <ng-template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9; padding-top: 12px;">
          <button class="btn-cancel btn-small" (click)="onCancelAction()" [disabled]="actionSubmitting()">
            <i class="pi pi-times"></i> Hủy
          </button>
          <button class="btn-save btn-small" (click)="onConfirmAction()" [disabled]="actionSubmitting()"
                  [style.background-color]="confirmButtonColor()" [style.border-color]="confirmButtonColor()">
            <i class="pi pi-spin pi-spinner" *ngIf="actionSubmitting()"></i>
            <i class="pi pi-check" *ngIf="!actionSubmitting()"></i>
            Xác nhận
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .tab-badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 18px;
      height: 18px;
      padding: 0 5px;
      margin-left: 6px;
      border-radius: 999px;
      background: #e2e8f0;
      color: #475569;
      font-size: 0.72rem;
      font-weight: 600;
      line-height: 1;
    }
    .tab-item.tab-active .tab-badge {
      background: #dbeafe;
      color: #1d4ed8;
    }
  `]
})
export class DossierPublishComponent implements OnInit {
  @Output() viewDetail = new EventEmitter<string>();

  tabs: PublishTab[] = ['pending-publish', 'published', 'unpublished'];
  tabLabel = tabLabel;

  private service = inject(DossierManagementService);
  private publishService = inject(DossierPublishService);
  private messageService = inject(MessageService);
  authService = inject(AuthService);

  items = signal<any[]>([]);
  loading = signal<boolean>(false);
  totalCount = signal<number>(0);

  currentPage = signal<number>(1);
  pageSize = signal<number>(10);

  searchKeyword = signal<string>('');
  filterGridTypeId = signal<number | null>(null);
  filterInfrastructureId = signal<string | null>(null);

  gridTypes = signal<any[]>([]);
  infrastructures = signal<any[]>([]);
  bhsColumns = signal<BhsCatalogColumn[]>([]);

  activeTab = signal<PublishTab>('pending-publish');
  tabCounts = signal<any>(null);

  // Action Confirm Dialog State
  showConfirmDialog = signal<boolean>(false);
  confirmActionType = signal<'publish' | 'unpublish' | 'republish' | null>(null);
  actionTarget = signal<any>(null);
  actionSubmitting = signal<boolean>(false);

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));
  tableColSpan = computed(() => this.bhsColumns().length + 4);

  actionTargetLabel = computed(() => {
    const item = this.actionTarget();
    if (!item) return '';
    const firstCol = this.bhsColumns()[0];
    if (firstCol) {
      const val = this.getCatalogValue(item, firstCol);
      if (val !== '-') return val;
    }
    return item.infrastructureName || item.dossierTypeName || 'này';
  });

  confirmHeader() {
    switch (this.confirmActionType()) {
      case 'publish': return 'Xác nhận xuất bản';
      case 'unpublish': return 'Xác nhận hủy xuất bản';
      case 'republish': return 'Xác nhận tái xuất bản';
      default: return 'Xác nhận hành động';
    }
  }

  confirmTitle() {
    switch (this.confirmActionType()) {
      case 'publish': return 'Bạn có chắc chắn muốn xuất bản hồ sơ này?';
      case 'unpublish': return 'Bạn có chắc chắn muốn hủy xuất bản hồ sơ này?';
      case 'republish': return 'Bạn có chắc chắn muốn tái xuất bản hồ sơ này?';
      default: return 'Xác nhận thực hiện hành động?';
    }
  }

  confirmButtonColor() {
    return this.confirmActionType() === 'unpublish' ? '#dc2626' : '#22c55e';
  }

  getTabBadgeCount(tab: PublishTab): number {
    const counts = this.tabCounts();
    if (!counts) return 0;
    switch (tab) {
      case 'pending-publish': return counts.pendingPublish || counts.PendingPublish || 0;
      case 'published': return counts.published || counts.Published || 0;
      case 'unpublished': return counts.unpublished || counts.Unpublished || 0;
      default: return 0;
    }
  }

  ngOnInit() {
    this.loadLookups();
    this.refreshList();
  }

  selectTab(tab: PublishTab) {
    if (this.activeTab() === tab) return;
    this.activeTab.set(tab);
    this.currentPage.set(1);
    this.loadData();
  }

  onSearch() {
    this.currentPage.set(1);
    this.refreshList();
  }

  refreshList() {
    this.loadTabCounts();
    this.loadData();
  }

  loadTabCounts() {
    this.publishService.getTabCounts({
      keyword: this.searchKeyword(),
      gridTypeId: this.filterGridTypeId() !== null ? this.filterGridTypeId()! : undefined,
      infrastructureId: this.filterInfrastructureId() || undefined,
    }).subscribe({
      next: (counts) => this.tabCounts.set(counts),
      error: () => console.error('Failed to load publish dossier tab counts')
    });
  }

  loadLookups() {
    this.service.getGridTypeLookup().subscribe({
      next: (res) => this.gridTypes.set(res),
      error: () => console.error('Failed to load grid types')
    });
    this.service.getInfrastructureLookup().subscribe({
      next: (res) => this.infrastructures.set(res),
      error: () => console.error('Failed to load infrastructures')
    });
    this.service.getBhsCatalogColumns().subscribe({
      next: (cols) => this.bhsColumns.set(cols),
      error: () => console.error('Failed to load BHS catalog columns')
    });
  }

  loadData() {
    this.loading.set(true);
    this.items.set([]);

    this.publishService.getPaged({
      tab: this.activeTab() as DossierListTab,
      keyword: this.searchKeyword(),
      gridTypeId: this.filterGridTypeId() !== null ? this.filterGridTypeId()! : undefined,
      infrastructureId: this.filterInfrastructureId() || undefined,
      page: this.currentPage(),
      pageSize: this.pageSize()
    }).subscribe({
      next: (res) => {
        this.items.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải danh sách hồ sơ' });
        this.loading.set(false);
      }
    });
  }

  getCatalogValue(item: any, col: BhsCatalogColumn): string {
    const data = item?.catalogData ?? item?.CatalogData ?? {};
    const value = data[col.key] ?? data[col.code];
    return value != null && String(value).trim() !== '' ? String(value) : '-';
  }

  changePage(page: number) {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
      this.loadData();
    }
  }

  requestAction(type: 'publish' | 'unpublish' | 'republish', item: any) {
    this.confirmActionType.set(type);
    this.actionTarget.set(item);
    this.showConfirmDialog.set(true);
  }

  onCancelAction() {
    this.showConfirmDialog.set(false);
    this.confirmActionType.set(null);
    this.actionTarget.set(null);
  }

  onConfirmAction() {
    const type = this.confirmActionType();
    const item = this.actionTarget();
    if (!type || !item) return;

    this.actionSubmitting.set(true);
    let obs$;

    if (type === 'publish') {
      obs$ = this.publishService.publish(item.id);
    } else if (type === 'unpublish') {
      obs$ = this.publishService.unpublish(item.id);
    } else {
      obs$ = this.publishService.republish(item.id);
    }

    obs$.pipe(
      finalize(() => {
        this.actionSubmitting.set(false);
        this.onCancelAction();
      })
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: type === 'publish' ? 'Xuất bản hồ sơ thành công' : type === 'unpublish' ? 'Hủy xuất bản hồ sơ thành công' : 'Tái xuất bản hồ sơ thành công'
        });
        this.refreshList();
      },
      error: (err) => {
        const msg = err?.error?.message || 'Có lỗi xảy ra khi thực hiện thao tác';
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: msg });
      }
    });
  }
}
