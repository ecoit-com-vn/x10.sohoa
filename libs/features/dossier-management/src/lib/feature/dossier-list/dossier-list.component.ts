import { Component, OnInit, signal, computed, inject, Output, EventEmitter } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { ToastModule } from 'primeng/toast';

import { DialogModule } from 'primeng/dialog';

import { MessageService } from 'primeng/api';

import { BhsCatalogColumn, DossierManagementService } from '../../data-access/dossier-management.service';
import {
  DossierListTab,
  DossierTabCounts,
  getDossierStatusLabel,
  getDossierStatusPillClass,
  getDossierWorkflowStepSubtitle,
} from '../../utils/dossier-status.util';

function normalizeTabCounts(raw: unknown): DossierTabCounts {
  const source = (raw ?? {}) as Record<string, unknown>;
  return {
    draft: Number(source['draft'] ?? source['Draft'] ?? 0),
    pendingAction: Number(source['pendingAction'] ?? source['PendingAction'] ?? 0),
    inProgress: Number(source['inProgress'] ?? source['InProgress'] ?? 0),
    completed: Number(source['completed'] ?? source['Completed'] ?? 0),
    returned: Number(source['returned'] ?? source['Returned'] ?? 0),
  };
}

@Component({

  selector: 'app-dossier-list',

  standalone: true,

  imports: [CommonModule, FormsModule, ToastModule, DialogModule],

  template: `

    <div class="wf-card">

      <div class="tab-bar">
        <button type="button" class="tab-item" [class.tab-active]="activeTab() === 'draft'" (click)="selectTab('draft')">
          Nháp
          <span class="tab-badge" *ngIf="tabCounts()?.draft">{{ tabCounts()!.draft }}</span>
        </button>
        <button type="button" class="tab-item" [class.tab-active]="activeTab() === 'pending-action'" (click)="selectTab('pending-action')">
          Chờ xử lý
          <span class="tab-badge" *ngIf="tabCounts()?.pendingAction">{{ tabCounts()!.pendingAction }}</span>
        </button>
        <button type="button" class="tab-item" [class.tab-active]="activeTab() === 'in-progress'" (click)="selectTab('in-progress')">
          Đang xử lý
          <span class="tab-badge" *ngIf="tabCounts()?.inProgress">{{ tabCounts()!.inProgress }}</span>
        </button>
        <button type="button" class="tab-item" [class.tab-active]="activeTab() === 'completed'" (click)="selectTab('completed')">
          Hoàn thành
          <span class="tab-badge" *ngIf="tabCounts()?.completed">{{ tabCounts()!.completed }}</span>
        </button>
        <button type="button" class="tab-item" [class.tab-active]="activeTab() === 'returned'" (click)="selectTab('returned')">
          Trả lại
          <span class="tab-badge" *ngIf="tabCounts()?.returned">{{ tabCounts()!.returned }}</span>
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

        <div class="toolbar-right" *ngIf="activeTab() === 'draft'">

          <button (click)="onCreateNew()" class="btn-green">

            <i class="pi pi-plus"></i> Tạo hồ sơ mới

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

              <th>Số lượng tài liệu</th>

              <th>Trạng thái duyệt</th>

              <th class="col-hd">Thao tác</th>

            </tr>

          </thead>

          <tbody>

            <ng-container *ngIf="loading()">

              <tr *ngFor="let r of [1,2,3,4,5]" class="skeleton-row">

                <td class="col-stt"><div class="skeleton-bar short" style="margin: 0 auto; width: 24px;"></div></td>

                <td *ngFor="let col of bhsColumns()"><div class="skeleton-bar"></div></td>

                <td><div class="skeleton-bar"></div></td>

                <td><div class="skeleton-bar short"></div></td>

                <td><div class="skeleton-bar"></div></td>

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

                  <b *ngIf="first" class="wf-name-link" (click)="onRowPrimaryAction(item)">{{ getCatalogValue(item, col) }}</b>

                  <span *ngIf="!first">{{ getCatalogValue(item, col) }}</span>

                </td>

                <td>

                  <div>{{ item.infrastructureName || '-' }}</div>

                  <div class="text-muted" style="font-size: 0.75rem;">{{ item.infrastructureCode }}</div>

                </td>

                <td class="text-center">{{ item.documentCount ?? 0 }}</td>

                <td>
                  <span [class]="getDossierStatusPillClass(item.status)">
                    {{ getDossierStatusLabel(item.status) }}
                  </span>
                  <div class="text-muted" style="font-size: 0.75rem; margin-top: 2px;"
                       *ngIf="getDossierWorkflowStepSubtitle(item.status, item.workflowStepName ?? item.workflowStatusName) as step">
                    {{ step }}
                  </div>
                </td>

                <td class="col-hd">

                  <div class="action-buttons-group">

                    <button (click)="onViewDetail(item.id)" class="act-btn act-assign" title="Chi tiết">

                      <i class="pi pi-eye"></i>

                    </button>

                    <button *ngIf="canEditItem(item)" (click)="onEdit(item.id)" class="act-btn act-edit" title="Sửa thông tin">

                      <i class="pi pi-pencil"></i>

                    </button>

                    <button *ngIf="item.status === 'Draft' || !item.workflowInstanceId" (click)="onDelete(item)" class="act-btn act-delete" title="Xóa">

                      <i class="pi pi-trash"></i>

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



    <p-dialog

      [visible]="showDeleteConfirm()"

      (visibleChange)="$event ? null : onCancelDelete()"

      header="Xác nhận xóa hồ sơ"

      [modal]="true"

      [style]="{ width: '420px' }"

      styleClass="evn-dialog-custom"

      [closable]="!deleting()">

      <div style="display: flex; align-items: flex-start; gap: 12px; padding: 8px 0 16px;">

        <i class="pi pi-exclamation-triangle" style="font-size: 1.8rem; color: #f59e0b;"></i>

        <div>

          <p style="margin: 0 0 6px 0; font-weight: 600; color: #1e293b;">Bạn có chắc chắn muốn xóa?</p>

          <p style="margin: 0; color: #64748b; font-size: 0.875rem;">

            Hồ sơ <b style="color: #1e293b;">{{ deleteTargetLabel() }}</b> sẽ bị xóa và không thể khôi phục.

          </p>

        </div>

      </div>

      <ng-template #footer>

        <div style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9; padding-top: 12px;">

          <button class="btn-cancel btn-small" (click)="onCancelDelete()" [disabled]="deleting()">

            <i class="pi pi-times"></i> Hủy

          </button>

          <button class="btn-save btn-small" (click)="onConfirmDelete()" [disabled]="deleting()"

                  style="background-color: #dc2626; border-color: #dc2626;">

            <i class="pi pi-spin pi-spinner" *ngIf="deleting()"></i>

            <i class="pi pi-trash" *ngIf="!deleting()"></i>

            Xóa

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

export class DossierListComponent implements OnInit {

  private service = inject(DossierManagementService);

  private messageService = inject(MessageService);



  @Output() viewDetail = new EventEmitter<string>();

  @Output() edit = new EventEmitter<string>();

  @Output() create = new EventEmitter<void>();



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

  activeTab = signal<DossierListTab>('draft');

  tabCounts = signal<DossierTabCounts | null>(null);



  showDeleteConfirm = signal<boolean>(false);

  deleteTarget = signal<any>(null);

  deleting = signal<boolean>(false);



  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  tableColSpan = computed(() => this.bhsColumns().length + 5);

  getDossierStatusPillClass = getDossierStatusPillClass;

  getDossierStatusLabel = getDossierStatusLabel;

  getDossierWorkflowStepSubtitle = getDossierWorkflowStepSubtitle;



  deleteTargetLabel = computed(() => {

    const item = this.deleteTarget();

    if (!item) return '';

    const firstCol = this.bhsColumns()[0];

    if (firstCol) {

      const val = this.getCatalogValue(item, firstCol);

      if (val !== '-') return val;

    }

    return item.infrastructureName || item.dossierTypeName || 'này';

  });



  ngOnInit() {

    this.loadLookups();

    this.refreshList();

  }



  selectTab(tab: DossierListTab) {

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

    this.service.getDossierTabCounts({

      keyword: this.searchKeyword(),

      gridTypeId: this.filterGridTypeId() !== null ? this.filterGridTypeId()! : undefined,

      infrastructureId: this.filterInfrastructureId() || undefined,

    }).subscribe({

      next: (counts) => this.tabCounts.set(normalizeTabCounts(counts)),

      error: () => console.error('Failed to load dossier tab counts')

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

    const filter = {

      tab: this.activeTab(),

      keyword: this.searchKeyword(),

      gridTypeId: this.filterGridTypeId() !== null ? this.filterGridTypeId()! : undefined,

      infrastructureId: this.filterInfrastructureId() || undefined,

      page: this.currentPage(),

      pageSize: this.pageSize()

    };



    this.service.getDossiers(filter).subscribe({

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



  onCreateNew() {

    this.create.emit();

  }



  onViewDetail(id: string) {

    this.viewDetail.emit(id);

  }



  onRowPrimaryAction(item: { id: string; status?: string; Status?: string }) {

    if (this.canEditItem(item)) {

      this.onEdit(item.id);

      return;

    }

    this.onViewDetail(item.id);

  }



  canEditItem(item: { status?: string; Status?: string; currentStepAllowEdit?: boolean; CurrentStepAllowEdit?: boolean }): boolean {

    if (this.activeTab() === 'draft') return true;

    const status = item.status ?? item.Status;

    if (status === 'Draft' || status === 'Returned') return true;

    const stepAllowEdit = item.currentStepAllowEdit ?? item.CurrentStepAllowEdit;

    if (this.activeTab() === 'pending-action' && stepAllowEdit) return true;

    return false;

  }



  onEdit(id: string) {

    this.edit.emit(id);

  }



  onDelete(item: any) {

    this.deleteTarget.set(item);

    this.showDeleteConfirm.set(true);

  }



  onConfirmDelete() {

    const item = this.deleteTarget();

    if (!item) return;

    this.deleting.set(true);

    this.service.deleteDossier(item.id).subscribe({

      next: () => {

        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã xóa hồ sơ' });

        this.showDeleteConfirm.set(false);

        this.deleteTarget.set(null);

        this.deleting.set(false);

        this.refreshList();

      },

      error: (err) => {

        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể xóa hồ sơ' });

        this.deleting.set(false);

        this.showDeleteConfirm.set(false);

      }

    });

  }



  onCancelDelete() {

    this.showDeleteConfirm.set(false);

    this.deleteTarget.set(null);

  }

}


