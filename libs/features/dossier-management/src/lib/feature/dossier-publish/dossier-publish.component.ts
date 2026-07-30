import { Component, OnInit, signal, computed, inject, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { MenuItem, MessageService } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
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
  imports: [CommonModule, FormsModule, ToastModule, DialogModule, MenuModule],
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
            placeholder="Tìm theo mã hồ sơ, tiêu đề hồ sơ..."
            [(ngModel)]="searchKeyword"
            (keyup.enter)="onSearch()"
          />
          <select class="wf-select" [ngModel]="filterDossierTypeId()" (ngModelChange)="onDossierTypeFilterChange($event)">
            <option [ngValue]="null">-- Tất cả loại hồ sơ --</option>
            <option *ngFor="let item of dossierTypes()" [value]="item.id">{{ item.name }}</option>
          </select>
          <div class="searchable-select">
            <button type="button" class="wf-select searchable-select-trigger" (click)="toggleInfrastructureDropdown()">
              <span>{{ selectedInfrastructureLabel() }}</span>
              <i class="pi pi-chevron-down"></i>
            </button>
            <div class="searchable-select-panel" *ngIf="isInfrastructureDropdownOpen()">
              <input
                type="text"
                class="searchable-select-input"
                placeholder="Tìm trên trạm/đường dây..."
                [ngModel]="infrastructureSearchKeyword()"
                (ngModelChange)="infrastructureSearchKeyword.set($event)"
              />
              <button type="button" class="searchable-select-option" (click)="selectInfrastructure(null)">
                -- Tất cả trạm/đường dây --
              </button>
              <button
                type="button"
                class="searchable-select-option"
                *ngFor="let item of filteredInfrastructures()"
                (click)="selectInfrastructure(item.id)"
              >
                {{ item.name }}
              </button>
              <div class="searchable-select-empty" *ngIf="filteredInfrastructures().length === 0">Không có dữ liệu</div>
            </div>
          </div>
          <select class="wf-select" [ngModel]="filterEquipmentId()" (ngModelChange)="onEquipmentFilterChange($event)" [disabled]="!filterInfrastructureId()">
            <option [ngValue]="null">-- Tất cả thiết bị --</option>
            <option *ngFor="let item of equipments()" [value]="item.id">{{ item.name }}</option>
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
              <th>Loại hồ sơ</th>
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
                <td>{{ getDossierTypeName(item) }}</td>
                <td>
                  <div>{{ item.infrastructureName || '-' }}</div>
                  <div class="text-muted" style="font-size: 0.75rem;">{{ item.infrastructureCode }}</div>
                </td>
                <td class="text-center" style="width: 100px;">{{ item.documentCount ?? 0 }}</td>
                <td class="col-hd">
                  <div class="action-buttons-group">
                    <button
                      type="button"
                      class="act-btn act-more"
                      title="Thao tác"
                      (click)="openPublishActionMenu(item, $event, publishActionMenu)">
                      <i class="pi pi-ellipsis-h"></i>
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

    <p-menu
      #publishActionMenu
      [model]="publishActionMenuItems()"
      [popup]="true"
      appendTo="body"
      styleClass="row-action-menu">
    </p-menu>

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
          <p *ngIf="confirmActionType() === 'delete'" style="margin: 0; color: #64748b; font-size: 0.875rem;">
            Hồ sơ <b style="color: #1e293b;">{{ actionTargetLabel() }}</b> sẽ bị xóa và không thể khôi phục.
          </p>
          <p *ngIf="confirmActionType() !== 'delete'" style="margin: 0; color: #64748b; font-size: 0.875rem;">
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
    .toolbar-right {
      margin-left: auto;
      display: flex;
      align-items: center;
    }
    .searchable-select {
      position: relative;
      min-width: 220px;
    }
    .searchable-select-trigger {
      display: flex;
      align-items: center;
      justify-content: space-between;
      width: 100%;
      text-align: left;
    }
    .searchable-select-trigger span {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .searchable-select-panel {
      position: absolute;
      z-index: 20;
      top: calc(100% + 4px);
      left: 0;
      width: 100%;
      max-height: 280px;
      overflow-y: auto;
      padding: 8px;
      background: #ffffff;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      box-shadow: 0 8px 16px rgba(15, 23, 42, 0.15);
    }
    .searchable-select-input {
      width: 100%;
      box-sizing: border-box;
      margin-bottom: 6px;
      padding: 8px 10px;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      outline: none;
    }
    .searchable-select-option {
      display: block;
      width: 100%;
      padding: 8px 10px;
      border: 0;
      border-radius: 4px;
      background: transparent;
      color: #334155;
      text-align: left;
      cursor: pointer;
    }
    .searchable-select-option:hover {
      background: #eff6ff;
      color: #1d4ed8;
    }
    .searchable-select-empty {
      padding: 8px 10px;
      color: #64748b;
      font-size: 0.9rem;
    }
  `]
})
export class DossierPublishComponent implements OnInit {
  @Output() viewDetail = new EventEmitter<string>();
  @Output() edit = new EventEmitter<string>();

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
  filterInfrastructureId = signal<string | null>(null);
  filterDossierTypeId = signal<string | null>(null);
  filterEquipmentId = signal<string | null>(null);
  infrastructureSearchKeyword = signal<string>('');
  isInfrastructureDropdownOpen = signal<boolean>(false);

  infrastructures = signal<any[]>([]);
  dossierTypes = signal<any[]>([]);
  equipments = signal<any[]>([]);
  filteredInfrastructures = computed(() => {
    const keyword = this.infrastructureSearchKeyword().trim().toLocaleLowerCase();
    if (!keyword) return this.infrastructures();
    return this.infrastructures().filter((item) =>
      String(item.name ?? '').toLocaleLowerCase().includes(keyword)
    );
  });
  bhsColumns = signal<BhsCatalogColumn[]>([]);

  activeTab = signal<PublishTab>('pending-publish');
  tabCounts = signal<any>(null);

  // Action Confirm Dialog State
  showConfirmDialog = signal<boolean>(false);
  confirmActionType = signal<'publish' | 'unpublish' | 'republish' | 'delete' | null>(null);
  actionTarget = signal<any>(null);
  actionSubmitting = signal<boolean>(false);
  publishActionMenuItems = signal<MenuItem[]>([]);

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));
  tableColSpan = computed(() => this.bhsColumns().length + 5);

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
    if (this.confirmActionType() === 'delete') return 'Xác nhận xóa hồ sơ';

    switch (this.confirmActionType()) {
      case 'publish': return 'Xác nhận xuất bản';
      case 'unpublish': return 'Xác nhận hủy xuất bản';
      case 'republish': return 'Xác nhận tái xuất bản';
      default: return 'Xác nhận hành động';
    }
  }

  confirmTitle() {
    if (this.confirmActionType() === 'delete') return 'Bạn có chắc chắn muốn xóa hồ sơ này?';

    switch (this.confirmActionType()) {
      case 'publish': return 'Bạn có chắc chắn muốn xuất bản hồ sơ này?';
      case 'unpublish': return 'Bạn có chắc chắn muốn hủy xuất bản hồ sơ này?';
      case 'republish': return 'Bạn có chắc chắn muốn tái xuất bản hồ sơ này?';
      default: return 'Xác nhận thực hiện hành động?';
    }
  }

  confirmButtonColor() {
    return this.confirmActionType() === 'unpublish' || this.confirmActionType() === 'delete' ? '#dc2626' : '#22c55e';
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

  toggleInfrastructureDropdown(): void {
    this.isInfrastructureDropdownOpen.update((open) => !open);
  }

  onDossierTypeFilterChange(dossierTypeId: string | null): void {
    this.filterDossierTypeId.set(dossierTypeId || null);
    this.onSearch();
  }

  onEquipmentFilterChange(equipmentId: string | null): void {
    this.filterEquipmentId.set(equipmentId || null);
    this.onSearch();
  }

  selectInfrastructure(infrastructureId: string | null): void {
    this.filterInfrastructureId.set(infrastructureId);
    this.filterEquipmentId.set(null);
    this.equipments.set([]);
    this.infrastructureSearchKeyword.set('');
    this.isInfrastructureDropdownOpen.set(false);

    if (infrastructureId) {
      this.service.getEquipmentLookup({ infrastructureId, pageSize: 1000 }).subscribe({
        next: (res) => this.equipments.set(Array.isArray(res) ? res : (res?.items ?? [])),
        error: () => this.equipments.set([]),
      });
    }

    this.onSearch();
  }

  selectedInfrastructureLabel(): string {
    const infrastructureId = this.filterInfrastructureId();
    if (!infrastructureId) return '-- Tất cả trạm/đường dây --';
    return this.infrastructures().find((item) => String(item.id) === String(infrastructureId))?.name
      ?? '-- Tất cả trạm/đường dây --';
  }

  refreshList() {
    this.loadTabCounts();
    this.loadData();
  }

  loadTabCounts() {
    this.publishService.getTabCounts({
      keyword: this.searchKeyword(),
      dossierTypeId: this.filterDossierTypeId() || undefined,
      infrastructureId: this.filterInfrastructureId() || undefined,
      equipmentId: this.filterEquipmentId() || undefined,
    }).subscribe({
      next: (counts) => this.tabCounts.set(counts),
      error: () => console.error('Failed to load publish dossier tab counts')
    });
  }

  loadLookups() {
    this.service.getDossierTypeLookup().subscribe({
      next: (res) => this.dossierTypes.set(res),
      error: () => console.error('Failed to load dossier types')
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
    const dossierTypeId = this.filterDossierTypeId();

    this.publishService.getPaged({
      tab: this.activeTab() as DossierListTab,
      keyword: this.searchKeyword(),
      dossierTypeId: dossierTypeId || undefined,
      infrastructureId: this.filterInfrastructureId() || undefined,
      equipmentId: this.filterEquipmentId() || undefined,
      page: this.currentPage(),
      pageSize: dossierTypeId ? 1000 : this.pageSize()
    }).subscribe({
      next: (res) => {
        const sourceItems = res.items || [];
        const matchingItems = dossierTypeId
          ? sourceItems.filter((item: any) =>
              String(item.dossierTypeId ?? item.DossierTypeId ?? '').toLowerCase()
              === String(dossierTypeId).toLowerCase()
            )
          : sourceItems;

        if (dossierTypeId) {
          const start = (this.currentPage() - 1) * this.pageSize();
          this.items.set(matchingItems.slice(start, start + this.pageSize()));
          this.totalCount.set(matchingItems.length);
        } else {
          this.items.set(matchingItems);
          this.totalCount.set(res.totalCount || 0);
        }
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

  getDossierTypeName(item: any): string {
    const name = item?.dossierTypeName ?? item?.DossierTypeName;
    return name != null && String(name).trim() !== '' ? String(name) : '-';
  }

  changePage(page: number) {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
      this.loadData();
    }
  }

  requestAction(type: 'publish' | 'unpublish' | 'republish' | 'delete', item: any) {
    this.confirmActionType.set(type);
    this.actionTarget.set(item);
    this.showConfirmDialog.set(true);
  }

  openPublishActionMenu(item: any, event: MouseEvent, menu: Menu): void {
    const items: MenuItem[] = [
      {
        label: 'Xem chi tiết',
        title: 'Xem chi tiết',
        icon: 'pi pi-eye color-teal',
        command: () => this.viewDetail.emit(item.id),
      },
      {
        label: 'Sửa thông tin',
        title: 'Sửa thông tin',
        icon: 'pi pi-pencil color-blue',
        command: () => this.edit.emit(item.id),
      },
    ];

    if (this.authService.hasPermission('DOSSIER_PUBLISH_RELEASE')) {
      if (this.activeTab() === 'pending-publish') {
        items.push({
          label: 'Xuất bản',
          title: 'Xuất bản',
          icon: 'pi pi-cloud-upload color-teal',
          command: () => this.requestAction('publish', item),
        });
      } else if (this.activeTab() === 'published') {
        items.push({
          label: 'Hủy xuất bản',
          title: 'Hủy xuất bản',
          icon: 'pi pi-ban color-red',
          command: () => this.requestAction('unpublish', item),
        });
      } else if (this.activeTab() === 'unpublished') {
        items.push({
          label: 'Tái xuất bản',
          title: 'Tái xuất bản',
          icon: 'pi pi-refresh color-teal',
          command: () => this.requestAction('republish', item),
        });
      }
    }

    if (this.activeTab() === 'unpublished') {
      items.push({
        label: 'Xóa',
        title: 'Xóa',
        icon: 'pi pi-trash color-red',
        command: () => this.requestAction('delete', item),
      });
    }

    this.publishActionMenuItems.set(items);
    menu.toggle(event);
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

    if (type === 'delete') {
      obs$ = this.service.deleteDossier(item.id);
    } else if (type === 'publish') {
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
        if (type === 'delete') {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa hồ sơ' });
          this.refreshList();
          return;
        }

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
