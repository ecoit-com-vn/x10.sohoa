import { Component, OnInit, signal, computed, inject, Output, EventEmitter } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { ToastModule } from 'primeng/toast';

import { DialogModule } from 'primeng/dialog';

import { MessageService } from 'primeng/api';

import { BhsCatalogColumn, DossierManagementService } from '../../data-access/dossier-management.service';



@Component({

  selector: 'app-dossier-list',

  standalone: true,

  imports: [CommonModule, FormsModule, ToastModule, DialogModule],

  template: `

    <div class="wf-card">

      <div class="edit-header">

        <div>

          <h2 class="edit-title">Quản lý Hồ sơ Thiết bị</h2>

          <p class="text-muted" style="font-size: 0.83rem; margin: 4px 0 0 0;">Quản lý danh sách hồ sơ, cập nhật thông tin và theo dõi quy trình duyệt</p>

        </div>

        <div class="edit-actions">

          <button (click)="onCreateNew()" class="btn-green">

            <i class="pi pi-plus"></i> Tạo hồ sơ mới

          </button>

        </div>

      </div>



      <div class="list-toolbar">

        <div class="toolbar-left">

          <input

            type="text"

            class="wf-search-input"

            placeholder="Tìm kiếm theo thông tin hồ sơ..."

            [(ngModel)]="searchKeyword"

            (keyup.enter)="loadData()"

          />

          <select class="wf-select" [(ngModel)]="filterGridTypeId" (change)="loadData()">

            <option [ngValue]="null">-- Tất cả loại lưới điện --</option>

            <option *ngFor="let item of gridTypes()" [value]="item.id">{{ item.name }}</option>

          </select>

          <select class="wf-select" [(ngModel)]="filterInfrastructureId" (change)="loadData()">

            <option [ngValue]="null">-- Tất cả trạm/đường dây --</option>

            <option *ngFor="let item of infrastructures()" [value]="item.id">{{ item.name }}</option>

          </select>

          <button (click)="loadData()" class="btn-tim">

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

              <th>Số lượng tài liệu</th>

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

                  <b *ngIf="first" class="wf-name-link" (click)="onViewDetail(item.id)">{{ getCatalogValue(item, col) }}</b>

                  <span *ngIf="!first">{{ getCatalogValue(item, col) }}</span>

                </td>

                <td>

                  <div>{{ item.infrastructureName || '-' }}</div>

                  <div class="text-muted" style="font-size: 0.75rem;">{{ item.infrastructureCode }}</div>

                </td>

                <td class="text-center">{{ item.documentCount ?? 0 }}</td>

                <td class="col-hd">

                  <div class="action-buttons-group">

                    <button (click)="onViewDetail(item.id)" class="act-btn act-assign" title="Chi tiết">

                      <i class="pi pi-eye"></i>

                    </button>

                    <button *ngIf="item.status === 'Draft' || item.status === 'Returned'" (click)="onEdit(item.id)" class="act-btn act-edit" title="Sửa thông tin">

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

  styles: []

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



  showDeleteConfirm = signal<boolean>(false);

  deleteTarget = signal<any>(null);

  deleting = signal<boolean>(false);



  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  tableColSpan = computed(() => this.bhsColumns().length + 4);



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

    this.loadData();

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

    const filter = {

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

        this.loadData();

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


