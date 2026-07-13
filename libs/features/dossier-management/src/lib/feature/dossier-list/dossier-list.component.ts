import { Component, OnInit, signal, computed, inject, Output, EventEmitter, Input } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { ToastModule } from 'primeng/toast';

import { DialogModule } from 'primeng/dialog';

import { MenuModule } from 'primeng/menu';

import { MessageService, MenuItem } from 'primeng/api';

import { BhsCatalogColumn, DossierManagementService, DossierWorkflowAction, normalizeDossierWorkflowAction } from '../../data-access/dossier-management.service';
import { AuthService } from '@sohoa.frontend/shared/core';
import {
  isRejectWorkflowLabel,
  isApproveWorkflowLabel,
  isRejectWorkflowAction,
  sortWorkflowActionsRejectLast,
  filterUsersByRequiredRole,
} from '../../utils/dossier-workflow-bpmn.util';
import {
  DossierListTab,
  DossierMenuScope,
  DossierTabCounts,
  getDefaultTabForMenuScope,
  getDossierStatusLabel,
  getDossierStatusPillClass,
  getTabsForMenuScope,
} from '../../utils/dossier-status.util';
import {
  canMutateDossierOnCreatorMenu as hasCreatorMenuMutatePermission,
  hasDossierCreatePermission,
} from '../../utils/dossier-permission.util';
import { isUserAuthorizedForWorkflowAction, buildListItemPatchFromSources, shouldKeepItemOnTab, DossierListItemPatch } from '../../utils/dossier-workflow-auth.util';
import { catchError, finalize, forkJoin, of } from 'rxjs';

function tabLabel(tab: DossierListTab, kindId?: number): string {
  const labels: Partial<Record<DossierListTab, string>> = {
    draft: kindId === 1 ? 'Nháp' : 'Tạo mới',
    'pending-action': 'Chờ xử lý',
    'in-progress': 'Đang xử lý',
    completed: kindId === 1 ? 'Hoàn thành' : 'Đã duyệt',
    returned: 'Trả lại',
  };
  return labels[tab] ?? '';
}

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

  imports: [CommonModule, FormsModule, ToastModule, DialogModule, MenuModule],

  template: `

    <div class="wf-card">

      <div class="tab-bar">
        <button
          type="button"
          class="tab-item"
          *ngFor="let tab of visibleTabs()"
          [class.tab-active]="activeTab() === tab"
          (click)="selectTab(tab)">
          {{ tabLabel(tab, kindIdSignal()) }}
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

        <div class="toolbar-right" *ngIf="isCreatorMenu() && activeTab() === 'draft' && canCreateDossier()">

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

              <th style="min-width: 200px; white-space: nowrap !important;">Trạm / Đường dây</th>

              <th style="width: 130px; text-align: center; white-space: normal !important;">Số lượng tài liệu</th>

              <th style="width: 140px; white-space: nowrap !important;">Người xử lý hiện tại</th>

              <th style="width: 160px; text-align: center; white-space: normal !important;">Trạng thái duyệt</th>

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

                <td class="station-cell" [title]="getInfrastructureName(item)">{{ getInfrastructureName(item) }}</td>

                <td class="text-center">{{ item.documentCount ?? 0 }}</td>

                <td class="handler-cell" [title]="getCurrentHandlerName(item)">{{ getCurrentHandlerName(item) }}</td>

                <td class="text-center">
                  <span [class]="getDossierStatusPillClass(item.statusId)">
                    {{ getDossierStatusLabel(item.statusId, item.statusName) }}
                  </span>
                </td>

                <td class="col-hd">

                  <div class="action-buttons-group">

                    <button (click)="onViewDetail(item.id)" class="act-btn act-assign" title="Chi tiết">

                      <i class="pi pi-eye"></i>

                    </button>

                    <button *ngIf="canEditItem(item)" (click)="onEdit(item.id)" class="act-btn act-edit" title="Sửa thông tin">

                      <i class="pi pi-pencil"></i>

                    </button>

                    <!-- Creator tab draft buttons -->
                    <ng-container *ngIf="isCreatorMenu() && activeTab() === 'draft'">
                      <!-- Hoàn thành nhập liệu cho Tạo mới (statusId === 1) -->
                      <button *ngIf="item.statusId === 1" (click)="onQuickCompleteInput(item)" class="act-btn act-edit" title="Hoàn thành nhập liệu" style="background-color: #f0fdf4; color: #16a34a; border-color: #bbf7d0;">
                        <i class="pi pi-check-circle"></i>
                      </button>
                      <!-- Gửi duyệt cho Hoàn thành (statusId === 2) -->
                      <button *ngIf="item.statusId === 2" (click)="onQuickSubmitForApproval(item)" class="act-btn act-assign" title="Gửi duyệt" style="background-color: #f0f9ff; color: #0284c7; border-color: #bae6fd;">
                        <i class="pi pi-send"></i>
                      </button>
                    </ng-container>

                    <!-- Thao tác nhanh (Quick Actions) dạng dropdown -->
                    <div *ngIf="checkQuickActionPermission(item)" style="display: inline-flex; justify-content: center; align-items: center;">
                      <button (click)="openQuickActionMenu($event, item, actionMenu)" class="act-btn act-assign" title="Thao tác nhanh">
                        <i class="pi pi-chevron-down"></i>
                      </button>
                    </div>

                    <button *ngIf="isCreatorMenu() && (item.statusId === 1 || item.statusId === 2 || !item.workflowInstanceId)" (click)="onDelete(item)" class="act-btn act-delete" title="Xóa">
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

    <p-menu #actionMenu [model]="quickActionMenuItems" [popup]="true" appendTo="body" styleClass="quick-action-menu"></p-menu>

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

    <!-- Dialog xác nhận hoàn thành nhập liệu nhanh -->
    <p-dialog [visible]="showQuickCompleteConfirm()" 
              (visibleChange)="$event ? null : showQuickCompleteConfirm.set(false)"
              header="Xác nhận hoàn thành" 
              [modal]="true" 
              [style]="{ width: '420px' }"
              styleClass="evn-dialog-custom"
              [closable]="!quickActionSubmitting()">
      <div style="display: flex; align-items: flex-start; gap: 12px; padding: 8px 0 16px;">
        <i class="pi pi-exclamation-triangle" style="font-size: 1.8rem; color: #3b82f6;"></i>
        <div>
          <p style="margin: 0 0 6px 0; font-weight: 600; color: #1e293b;">Bạn có chắc chắn muốn hoàn thành nhập liệu?</p>
          <p style="margin: 0; color: #64748b; font-size: 0.875rem;">
            Hồ sơ <b style="color: #1e293b;">{{ completeTargetLabel() }}</b> sẽ được chuyển sang trạng thái "Hoàn thành".
          </p>
        </div>
      </div>
      <ng-template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9; padding-top: 12px;">
          <button class="btn-cancel btn-small" (click)="showQuickCompleteConfirm.set(false)" [disabled]="quickActionSubmitting()">
            <i class="pi pi-times"></i> Hủy
          </button>
          <button class="btn-save btn-small" (click)="confirmQuickComplete()" [disabled]="quickActionSubmitting()">
            <i class="pi pi-spin pi-spinner" *ngIf="quickActionSubmitting()"></i>
            <i class="pi pi-check" *ngIf="!quickActionSubmitting()"></i>
            Xác nhận
          </button>
        </div>
      </ng-template>
    </p-dialog>

    <!-- Dialog gửi duyệt nhanh (cùng UI màn chi tiết) -->
    <p-dialog
      [visible]="showQuickSubmitConfirm()"
      (visibleChange)="$event ? null : showQuickSubmitConfirm.set(false)"
      header="Gửi duyệt hồ sơ"
      [modal]="true"
      [style]="{ width: '450px' }"
      styleClass="evn-dialog-custom"
      [closable]="!quickSubmitSubmitting() && !quickActionSubmitting()">
      <div style="display: flex; flex-direction: column; gap: 16px; padding: 8px 0 16px;">
        <div style="display: flex; align-items: flex-start; gap: 12px;">
          <i class="pi pi-send" style="font-size: 1.8rem; color: #1d4ed8;"></i>
          <div>
            <p style="margin: 0 0 6px 0; font-weight: 600; color: #1e293b;">Xác nhận gửi duyệt hồ sơ lên cấp trên</p>
            <p style="margin: 0; color: #64748b; font-size: 0.875rem;">
              Hồ sơ sẽ đi vào quy trình phê duyệt bước: <b style="color: #1e293b;">{{ quickSubmitNextStepInfo()?.stepName || 'Phê duyệt' }}</b>.
            </p>
          </div>
        </div>

        <div *ngIf="quickSubmitNextStepInfo()?.requiresNextAssignee" class="form-group">
          <label class="form-label required">Người duyệt tiếp theo ({{ quickSubmitNextStepInfo()?.stepName }})</label>
          <select class="wf-select" [value]="quickSubmitSelectedNextUser()" (change)="onQuickSubmitNextUserChange($event)">
            <option value="">-- Chọn người phê duyệt --</option>
            <option *ngFor="let u of filteredQuickSubmitNextUsers()" [value]="u.id || u.Id || u.userId || u.username">
              {{ u.fullName || u.FullName || u.name || u.username }}
            </option>
          </select>
        </div>
      </div>
      <ng-template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #f1f5f9; padding-top: 12px;">
          <button class="btn-cancel btn-small" (click)="showQuickSubmitConfirm.set(false)" [disabled]="quickActionSubmitting()">
            <i class="pi pi-times"></i> Hủy
          </button>
          <button class="btn-save btn-small" (click)="confirmQuickSubmit()" [disabled]="quickActionSubmitting() || quickSubmitSubmitting()">
            <i class="pi pi-spin pi-spinner" *ngIf="quickActionSubmitting() || quickSubmitSubmitting()"></i>
            <i class="pi pi-check" *ngIf="!quickActionSubmitting() && !quickSubmitSubmitting()"></i>
            Xác nhận gửi
          </button>
        </div>
      </ng-template>
    </p-dialog>

    <p-dialog
      [visible]="showQuickActionDialog()"
      (visibleChange)="$event ? null : closeQuickActionDialog()"
      [header]="pendingQuickActionMeta()?.label || pendingQuickAction()?.name || 'Xác nhận'"
      [modal]="true"
      [style]="{ width: '460px' }"
      styleClass="evn-dialog-custom"
      [closable]="!quickActionSubmitting()">
      <div *ngIf="quickActionLoading()" style="display: flex; align-items: center; gap: 8px; color: #64748b; padding: 8px 0;">
        <i class="pi pi-spin pi-spinner"></i> Đang tải thông tin quy trình...
      </div>
      <div *ngIf="!quickActionLoading()" style="display: flex; flex-direction: column; gap: 16px; padding: 4px 0 8px;">
        <div class="form-group" *ngIf="pendingQuickActionMeta()?.requiresUser && !isRejectLabel(pendingQuickActionMeta()?.label)">
          <label class="form-label">
            <span class="required">*</span> Người xử lý bước tiếp theo
          </label>
          <div *ngIf="quickActionUsersLoading()" style="display: flex; align-items: center; gap: 8px; color: #64748b; font-size: 0.875rem;">
            <i class="pi pi-spin pi-spinner"></i> Đang tải danh sách người xử lý...
          </div>
          <select *ngIf="!quickActionUsersLoading()" class="wf-select w-full"
                  [ngModel]="selectedNextUserId()"
                  (ngModelChange)="selectedNextUserId.set($event)">
            <option value="" disabled selected>-- Chọn người xử lý --</option>
            <option *ngFor="let u of filteredNextUsers()" [value]="u.id ?? u.Id">
              {{ u.fullName ?? u.FullName ?? u.name }} ({{ u.username ?? u.Username }})
            </option>
          </select>
        </div>
        <div class="form-group">
          <label class="form-label">Ý kiến xử lý <span style="color: #94a3b8; font-weight: 400;">(tuỳ chọn)</span></label>
          <textarea class="wf-textarea w-full" rows="3"
                    [ngModel]="quickActionComment()"
                    (ngModelChange)="quickActionComment.set($event)"
                    [placeholder]="isRejectLabel(pendingQuickActionMeta()?.label) ? 'Nhập lý do từ chối / trả lại...' : 'Nhập ý kiến xử lý (nếu có)...'">
          </textarea>
        </div>
      </div>
      <ng-template #footer>
        <button class="btn-cancel btn-small" (click)="closeQuickActionDialog()" [disabled]="quickActionSubmitting()">Hủy</button>
        <button class="btn-small"
                [class.btn-cancel]="isRejectLabel(pendingQuickActionMeta()?.label)"
                [class.btn-save]="isApproveLabel(pendingQuickActionMeta()?.label)"
                [class.btn-green]="!isRejectLabel(pendingQuickActionMeta()?.label) && !isApproveLabel(pendingQuickActionMeta()?.label)"
                (click)="confirmQuickAction()"
                [disabled]="quickActionSubmitting() || quickActionLoading() || quickActionUsersLoading()">
          <i class="pi pi-spin pi-spinner" *ngIf="quickActionSubmitting()"></i>
          <i class="pi pi-check" *ngIf="!quickActionSubmitting() && !isRejectLabel(pendingQuickActionMeta()?.label)"></i>
          <i class="pi pi-times" *ngIf="!quickActionSubmitting() && isRejectLabel(pendingQuickActionMeta()?.label)"></i>
          {{ pendingQuickActionMeta()?.label || pendingQuickAction()?.name }}
        </button>
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
    ::ng-deep .quick-action-reject .p-menuitem-text,
    ::ng-deep .quick-action-reject .p-menuitem-icon,
    ::ng-deep .quick-action-reject span,
    ::ng-deep .quick-action-reject i {
      color: #ef4444 !important;
    }
    .station-cell,
    .handler-cell {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 220px;
    }
    .handler-cell {
      max-width: 160px;
    }
    ::ng-deep .quick-action-approve .p-menuitem-text,
    ::ng-deep .quick-action-approve .p-menuitem-icon,
    ::ng-deep .quick-action-approve span,
    ::ng-deep .quick-action-approve i {
      color: #3BA962 !important;
    }
    ::ng-deep .quick-action-menu .p-menuitem-link {
      display: flex !important;
      justify-content: center !important;
      align-items: center !important;
      text-align: center !important;
      padding: 8px 16px !important;
    }
    ::ng-deep .quick-action-menu .p-menuitem-text {
      flex: none !important;
      margin-left: 8px !important;
    }
  `]

})

export class DossierListComponent implements OnInit {

  @Input({ required: true }) set menuScope(value: DossierMenuScope) {
    const changed = this.menuScopeSignal() !== value;
    this.menuScopeSignal.set(value);
    this.activeTab.set(getDefaultTabForMenuScope(value));
    if (changed && this.listBootstrapped) {
      this.refreshList();
    }
  }

  @Input() set kindId(value: number | undefined) {
    const id = value ?? 2;
    const changed = this.kindIdSignal() !== id;
    this.kindIdSignal.set(id);
    this.service.setKindContext(id);
    if (changed && this.listBootstrapped) {
      this.refreshList();
    }
  }

  kindIdSignal = signal<number>(2);
  private listBootstrapped = false;
  private menuScopeSignal = signal<DossierMenuScope>('creator');
  visibleTabs = computed(() => getTabsForMenuScope(this.menuScopeSignal(), this.kindIdSignal()));
  isCreatorMenu = computed(() => this.menuScopeSignal() === 'creator');
  isApproverMenu = computed(() => this.menuScopeSignal() === 'approver');
  isDigitization = computed(() => this.kindIdSignal() === 1);

  tabLabel = tabLabel;

  canCreateDossier(): boolean {
    return hasDossierCreatePermission(this.authService, this.isDigitization());
  }

  private service = inject(DossierManagementService);

  private messageService = inject(MessageService);

  authService = inject(AuthService);

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

  getTabBadgeCount(tab: DossierListTab): number {
    const counts = this.tabCounts();
    if (!counts) return 0;
    switch (tab) {
      case 'draft': return counts.draft;
      case 'pending-action': return counts.pendingAction;
      case 'in-progress': return counts.inProgress;
      case 'completed': return counts.completed;
      case 'returned': return counts.returned;
      default: return 0;
    }
  }



  showDeleteConfirm = signal<boolean>(false);

  deleteTarget = signal<any>(null);

  deleting = signal<boolean>(false);

  showQuickActionDialog = signal<boolean>(false);
  quickActionLoading = signal<boolean>(false);
  quickActionUsersLoading = signal<boolean>(false);
  quickActionSubmitting = signal<boolean>(false);
  quickActionComment = signal<string>('');
  selectedNextUserId = signal<string>('');
  quickActionDossierId = signal<string | null>(null);
  pendingQuickAction = signal<DossierWorkflowAction | null>(null);
  pendingQuickActionMeta = signal<{
    label: string;
    targetNodeId: string;
    requiresUser: boolean;
    requiredRole: string;
  } | null>(null);
  users = signal<any[]>([]);
  showQuickCompleteConfirm = signal<boolean>(false);
  showQuickSubmitConfirm = signal<boolean>(false);
  selectedQuickItem = signal<any>(null);
  quickSubmitNextStepInfo = signal<any>(null);
  quickSubmitSelectedNextUser = signal<string>('');
  quickSubmitSubmitting = signal<boolean>(false);
  filteredQuickSubmitNextUsers = computed(() => {
    const info = this.quickSubmitNextStepInfo();
    if (!info || !info.requiredRole) return [];
    const roles = info.requiredRole.split(',').map((r: string) => r.trim().toUpperCase());
    return this.users().filter((u: any) => {
      const uRoles: string[] = (u.roles || u.Roles || []).map((r: string) => r.toUpperCase());
      return uRoles.some(r => roles.includes(r));
    });
  });

  filteredNextUsers = computed(() =>
    filterUsersByRequiredRole(this.users(), this.pendingQuickActionMeta()?.requiredRole)
  );



  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  tableColSpan = computed(() => this.bhsColumns().length + 6);

  getDossierStatusPillClass = getDossierStatusPillClass;

  getDossierStatusLabel = getDossierStatusLabel;

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

  completeTargetLabel = computed(() => {
    const item = this.selectedQuickItem();
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
    this.listBootstrapped = true;
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

  /** Cập nhật 1 dòng từ API Oracle + workflow (tránh chờ ES đồng bộ). */
  private refreshListItemAfterMutation(id: string, onDone?: () => void) {
    forkJoin({
      detail: this.service.getDossierById(id),
      workflow: this.service.getWorkflowDetail(id, this.kindIdSignal()).pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ detail, workflow }) => {
        const patch = buildListItemPatchFromSources(detail, workflow);
        this.applyListItemPatch(id, patch);
        onDone?.();
      },
      error: () => {
        this.refreshList();
        onDone?.();
      },
    });
  }

  private applyListItemPatch(id: string, patch: DossierListItemPatch) {
    const tab = this.activeTab();

    if (tab === 'pending-action') {
      const stillInInbox = isUserAuthorizedForWorkflowAction({
        authService: this.authService,
        menuScope: this.menuScopeSignal(),
        currentAssignees: patch.currentAssignees,
        assigneeUserId: patch.currentAssignees[0],
      });
      if (!stillInInbox) {
        this.items.update((list) => list.filter((item) => item.id !== id));
        this.totalCount.update((count) => Math.max(0, count - 1));
        return;
      }
    } else if (!shouldKeepItemOnTab(tab, {
      statusId: patch.statusId,
      workflowInstanceId: patch.workflowInstanceId,
    })) {
      this.items.update((list) => list.filter((item) => item.id !== id));
      this.totalCount.update((count) => Math.max(0, count - 1));
      return;
    }

    this.items.update((list) =>
      list.map((item) => (item.id === id ? { ...item, ...patch } : item))
    );
  }



  loadTabCounts() {

    this.service.getDossierTabCounts({
      menuScope: this.menuScopeSignal(),
      kindId: this.kindIdSignal(),
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
      menuScope: this.menuScopeSignal(),
      kindId: this.kindIdSignal(),
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

  getInfrastructureName(item: any): string {
    const name = item?.infrastructureName ?? item?.InfrastructureName;
    if (name != null && String(name).trim() !== '') return String(name).trim();
    return '-';
  }

  getCurrentHandlerName(item: any): string {
    const handlerName = item?.currentHandlerName ?? item?.CurrentHandlerName;
    const creatorUsername = item?.creator?.username ?? item?.Creator?.Username;
    const normalizedUsername = creatorUsername ? String(creatorUsername).trim().toLowerCase() : '';

    if (handlerName != null && String(handlerName).trim() !== '') {
      const normalizedHandler = String(handlerName).trim();
      if (!normalizedUsername || normalizedHandler.toLowerCase() !== normalizedUsername) {
        return normalizedHandler;
      }
    }

    const creatorName = item?.creator?.name ?? item?.Creator?.Name;
    if (creatorName != null && String(creatorName).trim() !== '') {
      const normalizedCreator = String(creatorName).trim();
      if (!normalizedUsername || normalizedCreator.toLowerCase() !== normalizedUsername) {
        return normalizedCreator;
      }
    }

    return '-';
  }

  onQuickSubmitNextUserChange(event: Event) {
    const target = event.target as HTMLSelectElement | null;
    this.quickSubmitSelectedNextUser.set(target?.value || '');
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



  isAssignedToCurrentUser(item: any): boolean {
    if (!item) return false;
    
    const userId = this.authService.getUserId();
    const roles = this.authService.getUserRoles() || [];

    if (roles.includes('ADMIN')) return true;

    const status = item.status ?? item.Status;

    if (status === 'Returned') {
      return this.isCurrentUserCreator(item);
    }

    if (this.activeTab() === 'draft' || status === 'Draft' || status === 'New' || status === 'CompletedInput') {
      return this.isCurrentUserCreator(item);
    }

    if (item.currentAssignees && item.currentAssignees.length > 0) {
      return item.currentAssignees.some((assignee: string) =>
        String(assignee).toLowerCase() === String(userId).toLowerCase()
      );
    }

    return false;
  }

  private isCurrentUserCreator(item: any): boolean {
    const userId = this.authService.getUserId();
    const creatorId = item.creator?.id ?? item.Creator?.Id ?? item.creatorId ?? item.CreatorId;
    const creatorUsername = item.creator?.username ?? item.Creator?.Username ?? item.creatorUsername ?? item.CreatorUsername ?? item.createdBy ?? item.CreatedBy;

    const normalizeGuid = (val: unknown) => val ? String(val).replace(/-/g, '').toLowerCase().trim() : '';
    const normCreatorId = normalizeGuid(creatorId);
    const normUserId = normalizeGuid(userId);

    if (normCreatorId !== '' && normCreatorId === normUserId) return true;

    const normCreatorUsername = creatorUsername ? String(creatorUsername).toLowerCase().trim() : '';
    const normUserUsername = userId ? String(userId).toLowerCase().trim() : '';
    return normCreatorUsername !== '' && normCreatorUsername === normUserUsername;
  }

  canEditItem(item: any): boolean {
    if (this.isApproverMenu()) return false;
    if (!this.canMutateDossierOnCreatorMenu()) return false;

    const status = item.status ?? item.Status;
    const isDraftState = this.activeTab() === 'draft' || status === 'Draft' || status === 'New' || status === 'CompletedInput' || status === 'Returned';
    const stepAllowEdit = item.currentStepAllowEdit ?? item.CurrentStepAllowEdit;
    
    if (isDraftState || stepAllowEdit) {
      return this.isAssignedToCurrentUser(item);
    }

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

  checkQuickActionPermission(item: any): boolean {
    const actions = this.getItemAvailableActions(item);
    if (!item || actions.length === 0) return false;
    if (!item.currentAssignees || item.currentAssignees.length === 0) return false;

    const statusId = item.statusId ?? item.StatusId;
    const status = item.status ?? item.Status;

    return isUserAuthorizedForWorkflowAction({
      authService: this.authService,
      menuScope: this.menuScopeSignal(),
      currentAssignees: item.currentAssignees,
      statusId: statusId ?? (status === 'Returned' ? 5 : undefined),
      isCreator: this.isCurrentUserCreator(item),
    });
  }

  private shouldUseResubmit(item: any): boolean {
    if (!this.isCreatorMenu()) return false;
    const status = item.status ?? item.Status;
    return status === 'Returned' || this.activeTab() === 'returned';
  }

  /** Menu quản lý: sửa form/tài liệu cần EDIT hoặc CREATE theo loại hồ sơ. */
  private canMutateDossierOnCreatorMenu(): boolean {
    if (!this.isCreatorMenu()) return false;
    return hasCreatorMenuMutatePermission(this.authService, this.isDigitization());
  }

  onQuickAction(
    dossierId: string,
    action: DossierWorkflowAction,
    meta: { label: string; targetNodeId: string; requiresUser: boolean; requiredRole: string }
  ) {
    const isReject = this.isRejectLabel(meta.label);
    if (meta.requiresUser && !isReject && !this.selectedNextUserId()) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi',
        detail: 'Vui lòng chọn người xử lý bước tiếp theo.',
      });
      return;
    }

    this.quickActionSubmitting.set(true);
    const request = {
      nextNodeId: meta.targetNodeId || action.nextNodeId,
      actionLabel: meta.label || action.name,
      comment: this.quickActionComment() || undefined,
      nextAssigneeUserId: (!isReject && meta.requiresUser) ? this.selectedNextUserId() : undefined,
    };
    const targetItem = this.items().find(i => i.id === dossierId);
    const workflowCall = this.shouldUseResubmit(targetItem ?? { status: this.activeTab() === 'returned' ? 'Returned' : '' })
      ? this.service.resubmitWorkflow(dossierId, request, this.kindIdSignal())
      : this.service.moveWorkflow(dossierId, request, this.kindIdSignal());

    workflowCall.pipe(
      finalize(() => this.quickActionSubmitting.set(false))
    ).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: `Thao tác "${meta.label || action.name}" thành công!`,
        });
        this.closeQuickActionDialog(true);
        this.refreshListItemAfterMutation(dossierId, () => this.loadTabCounts());
      },
      error: (err) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err?.error?.message || 'Không thể thực hiện thao tác nhanh.',
        });
      },
    });
  }

  openQuickActionDialog(item: any, rawAction: Record<string, unknown>) {
    const action = normalizeDossierWorkflowAction(rawAction);
    const meta = this.buildQuickActionMeta(action);

    this.quickActionDossierId.set(item.id);
    this.pendingQuickAction.set(action);
    this.pendingQuickActionMeta.set(meta);
    this.quickActionComment.set('');
    this.selectedNextUserId.set('');
    this.users.set([]);
    this.showQuickActionDialog.set(true);
    this.quickActionLoading.set(false);
    this.loadQuickActionUsers(meta);
  }

  private getItemAvailableActions(item: any): any[] {
    const actions = item?.availableActions ?? item?.AvailableActions;
    return Array.isArray(actions) ? actions : [];
  }

  private buildQuickActionMeta(action: DossierWorkflowAction) {
    return {
      label: action.name,
      targetNodeId: action.nextNodeId,
      requiresUser: !!action.requiresNextAssignee,
      requiredRole: action.nextStepRole ?? '',
    };
  }

  /** Gọi API users/lookup theo role từ availableActions ES. */
  private loadQuickActionUsers(meta: { requiresUser: boolean; requiredRole: string }) {
    if (!meta.requiresUser) {
      this.users.set([]);
      this.quickActionUsersLoading.set(false);
      return;
    }

    this.quickActionUsersLoading.set(true);
    const roles = meta.requiredRole.split(',').map((r) => r.trim()).filter(Boolean);
    const apiRole = roles.length === 1 ? roles[0] : null;

    this.service.getUsersLookup(apiRole).subscribe({
      next: (users) => {
        const list = Array.isArray(users) ? users : [];
        this.users.set(roles.length > 1 ? filterUsersByRequiredRole(list, meta.requiredRole) : list);
        this.quickActionUsersLoading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'warn',
          summary: 'Cảnh báo',
          detail: 'Không thể tải danh sách người xử lý.',
        });
        this.users.set([]);
        this.quickActionUsersLoading.set(false);
      },
    });
  }

  confirmQuickAction() {
    const dossierId = this.quickActionDossierId();
    const action = this.pendingQuickAction();
    const meta = this.pendingQuickActionMeta();
    if (!dossierId || !action || !meta) return;
    this.onQuickAction(dossierId, action, meta);
  }

  closeQuickActionDialog(force = false) {
    if (!force && this.quickActionSubmitting()) return;
    this.showQuickActionDialog.set(false);
    this.quickActionDossierId.set(null);
    this.pendingQuickAction.set(null);
    this.pendingQuickActionMeta.set(null);
    this.quickActionComment.set('');
    this.selectedNextUserId.set('');
    this.quickActionUsersLoading.set(false);
  }

  isRejectLabel(label?: string | null): boolean {
    return isRejectWorkflowLabel(label);
  }

  isApproveLabel(label?: string | null): boolean {
    return isApproveWorkflowLabel(label);
  }

  quickActionMenuItems: MenuItem[] = [];

  openQuickActionMenu(event: Event, item: any, menu: any) {
    event.stopPropagation();
    const actions = sortWorkflowActionsRejectLast(this.getItemAvailableActions(item));
    if (actions.length === 0) return;

    this.quickActionMenuItems = actions.map((act: any) => {
      const isReject = isRejectWorkflowAction(act);
      return {
        label: act.name,
        icon: isReject ? 'pi pi-times-circle' : 'pi pi-check-circle',
        styleClass: isReject ? 'quick-action-reject' : 'quick-action-approve',
        command: () => {
          this.openQuickActionDialog(item, act);
        }
      };
    });

    menu.toggle(event);
  }

  onQuickCompleteInput(item: any) {
    this.selectedQuickItem.set(item);
    this.showQuickCompleteConfirm.set(true);
  }

  confirmQuickComplete() {
    const item = this.selectedQuickItem();
    if (!item || this.quickActionSubmitting()) return;

    this.quickActionSubmitting.set(true);
    this.service.completeInput(item.id).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã hoàn thành nhập liệu thành công' });
        this.showQuickCompleteConfirm.set(false);
        this.quickActionSubmitting.set(false);
        this.items.update((list) =>
          list.map((row) =>
            row.id === item.id
              ? { ...row, statusId: 2, status: 'CompletedInput', statusName: 'Hoàn thành' }
              : row
          )
        );
        this.refreshListItemAfterMutation(item.id, () => this.loadTabCounts());
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể hoàn thành nhập liệu' });
        this.quickActionSubmitting.set(false);
      }
    });
  }

  onQuickSubmitForApproval(item: any) {
    this.selectedQuickItem.set(item);
    this.quickSubmitSubmitting.set(true);
    this.service.getNextStepInfo(this.kindIdSignal()).subscribe({
      next: (res) => {
        if (res?.autoApprove) {
          this.service.submitForApproval(item.id, {
            nextNodeId: '',
            actionLabel: 'Tự động duyệt',
            comment: 'Tự động phê duyệt — chưa cấu hình quy trình.'
          }, this.kindIdSignal()).subscribe({
            next: () => {
              this.messageService.add({ severity: 'success', summary: 'Thành công', detail: res.message || 'Đã tự động phê duyệt hồ sơ' });
              this.quickSubmitSubmitting.set(false);
              this.showQuickSubmitConfirm.set(false);
              this.refreshListItemAfterMutation(item.id, () => this.loadTabCounts());
            },
            error: (err: any) => {
              this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể tự động phê duyệt hồ sơ.' });
              this.quickSubmitSubmitting.set(false);
            }
          });
          return;
        }
        this.quickSubmitNextStepInfo.set(res);
        this.quickSubmitSelectedNextUser.set('');
        this.service.getUsersLookup().subscribe({
          next: (users) => {
            this.users.set(Array.isArray(users) ? users : []);
            this.showQuickSubmitConfirm.set(true);
            this.quickSubmitSubmitting.set(false);
          },
          error: () => {
            this.users.set([]);
            this.showQuickSubmitConfirm.set(true);
            this.quickSubmitSubmitting.set(false);
          }
        });
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể lấy thông tin bước duyệt tiếp theo.' });
        this.quickSubmitSubmitting.set(false);
      }
    });
  }

  confirmQuickSubmit() {
    const item = this.selectedQuickItem();
    const info = this.quickSubmitNextStepInfo();
    if (!item || !info || this.quickActionSubmitting()) return;

    if (info.requiresNextAssignee && !this.quickSubmitSelectedNextUser()) {
      this.messageService.add({ severity: 'warn', summary: 'Cảnh báo', detail: 'Vui lòng chọn người duyệt tiếp theo.' });
      return;
    }

    this.quickActionSubmitting.set(true);
    this.service.submitForApproval(item.id, {
      nextNodeId: info.nextNodeId,
      actionLabel: 'Trình duyệt',
      nextAssigneeUserId: this.quickSubmitSelectedNextUser() || undefined,
      comment: 'Kính trình phê duyệt hồ sơ.'
    }, this.kindIdSignal()).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã gửi duyệt hồ sơ thành công' });
        this.showQuickSubmitConfirm.set(false);
        this.quickActionSubmitting.set(false);
        this.refreshListItemAfterMutation(item.id, () => this.loadTabCounts());
      },
      error: (err: any) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể gửi duyệt hồ sơ' });
        this.quickActionSubmitting.set(false);
      }
    });
  }

}


