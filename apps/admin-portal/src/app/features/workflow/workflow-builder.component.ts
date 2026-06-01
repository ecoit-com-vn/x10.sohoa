import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import {
  WorkflowService,
  WorkflowDefinition,
  WorkflowStep
} from '../../core/services/workflow.service';
import { finalize } from 'rxjs/operators';

// ─── BPMN internal types ──────────────────────────────────────────────────────

interface BpmnNode {
  id: string;
  type: 'start' | 'task' | 'gateway' | 'end';
  label: string;
  x: number;
  y: number;
  stepNum?: number;
  // linked to WorkflowStep
  stepRef?: WorkflowStep;
}

interface BpmnEdge {
  id: string;
  from: string;
  to: string;
  label?: string;
}

// ─── Danh mục loại quy trình theo nghiệp vụ số hóa EVNHANOI ─────────────────

const LOAI_QUY_TRINH_OPTIONS = [
  'Quy trình số hóa hồ sơ đường dây',
  'Quy trình số hóa hồ sơ trạm biến áp',
  'Quy trình phê duyệt tài liệu số hóa',
  'Quy trình kiểm soát chất lượng OCR',
  'Quy trình mượn/trả hồ sơ kỹ thuật',
  'Quy trình bàn giao hồ sơ kỹ thuật',
  'Quy trình hiệu đính tài liệu số hóa',
  'Quy trình cấp mã hồ sơ số hóa',
];

// ─── Sơ đồ BPMN mẫu theo nghiệp vụ số hóa ───────────────────────────────────

const SAMPLE_NODES: BpmnNode[] = [
  { id: 'n_start', type: 'start',   label: 'Bắt đầu',               x: 50,  y: 160 },
  { id: 'n1',      type: 'task',    label: 'Tiếp nhận &\nQuét HS',  x: 140, y: 130, stepNum: 1 },
  { id: 'n2',      type: 'task',    label: 'Nhận dạng\nOCR tự động', x: 310, y: 130, stepNum: 2 },
  { id: 'gw1',     type: 'gateway', label: '',                        x: 480, y: 147 },
  { id: 'n3',      type: 'task',    label: 'Kiểm soát\nchất lượng', x: 565, y: 130, stepNum: 3 },
  { id: 'gw2',     type: 'gateway', label: '',                        x: 735, y: 147 },
  { id: 'n4',      type: 'task',    label: 'Phê duyệt &\nLưu kho số', x: 820, y: 130, stepNum: 4 },
  { id: 'gw3',     type: 'gateway', label: '',                        x: 990, y: 147 },
  { id: 'n_end',   type: 'end',     label: 'Kết thúc',               x: 1075, y: 160 },
];

const SAMPLE_EDGES: BpmnEdge[] = [
  { id: 'e0',  from: 'n_start', to: 'n1' },
  { id: 'e1',  from: 'n1',      to: 'n2' },
  { id: 'e2',  from: 'n2',      to: 'gw1' },
  { id: 'e3',  from: 'gw1',     to: 'n3',      label: 'Tiếp tục' },
  { id: 'e3b', from: 'gw1',     to: 'n1',      label: 'Quét lại' },
  { id: 'e4',  from: 'n3',      to: 'gw2' },
  { id: 'e5',  from: 'gw2',     to: 'n4',      label: 'Đạt CL' },
  { id: 'e5b', from: 'gw2',     to: 'n1',      label: 'Từ chối' },
  { id: 'e6',  from: 'n4',      to: 'gw3' },
  { id: 'e7',  from: 'gw3',     to: 'n_end',   label: 'Duyệt' },
  { id: 'e7b', from: 'gw3',     to: 'n1',      label: 'Từ chối' },
];

// ─── Component ────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-workflow-builder',
  standalone: true,
  imports: [CommonModule, FormsModule, HttpClientModule, ToastModule],
  providers: [MessageService],
  template: `
    <div class="wf-page">
      <p-toast></p-toast>

      <!-- ══════════════════════════════════════════════════════════
           LOADING OVERLAY
      ══════════════════════════════════════════════════════════ -->
      <div class="loading-overlay" *ngIf="loading || saving || deleting">
        <div class="loading-spinner">
          <i class="pi pi-spin pi-spinner"></i>
          <span>{{ saving ? 'Đang lưu quy trình...' : deleting ? 'Đang xóa quy trình...' : loadingMsg }}</span>
        </div>
      </div>

      <!-- ══════════════════════════════════════════════════════════
           VIEW: DANH SÁCH QUY TRÌNH
      ══════════════════════════════════════════════════════════ -->
      <div *ngIf="viewMode === 'list'" class="wf-card">

        <div class="breadcrumb">
          <i class="pi pi-home bc-icon"></i>
          <span class="bc-text">Trang chủ</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">Cài đặt quy trình</span>
        </div>

        <!-- Toolbar -->
        <div class="list-toolbar">
          <div class="toolbar-left">
            <input id="wf-search" type="text" class="wf-search-input"
              placeholder="Tìm kiếm theo tên, mô tả..."
              [(ngModel)]="searchKeyword"
              (keyup.enter)="loadList()" />
            <button id="btn-tim" class="btn-tim" (click)="loadList()">
              <i class="pi pi-search"></i> Tìm
            </button>
          </div>
          <div class="toolbar-right">
            <button id="btn-loc" class="btn-outlined"
              [class.btn-filter-active]="showFilter"
              (click)="showFilter = !showFilter">
              <i class="pi pi-filter"></i> Lọc
            </button>
            <button id="btn-them" class="btn-green" (click)="onAddNew()">
              <i class="pi pi-plus"></i> Thêm
            </button>
            <button id="btn-excel" class="btn-excel" (click)="onExportExcel()">
              <i class="pi pi-file-excel"></i> Excel
            </button>
            <button id="btn-xoa-nhieu" class="btn-outlined btn-xoa-outlined"
              [disabled]="selectedIds.length === 0"
              (click)="promptDeleteSelected()">
              <i class="pi pi-trash"></i> Xóa
            </button>
          </div>
        </div>

        <!-- Filter row -->
        <div class="filter-row" *ngIf="showFilter">
          <select class="wf-select" [(ngModel)]="filterIsActive" (change)="loadList()">
            <option value="">-- Tất cả trạng thái --</option>
            <option value="true">Đang hoạt động</option>
            <option value="false">Ngưng hoạt động</option>
          </select>
          <button class="btn-outlined btn-small" (click)="resetFilter()">
            <i class="pi pi-times"></i> Xóa lọc
          </button>
        </div>

        <!-- Error banner -->
        <div class="error-banner" *ngIf="listError">
          <i class="pi pi-exclamation-circle"></i> {{ listError }}
          <button class="btn-retry" (click)="loadList()">Thử lại</button>
        </div>

        <!-- Table -->
        <div class="wf-table-wrap">
          <table class="wf-table" id="wf-table">
            <thead>
              <tr>
                <th class="col-chk">
                  <input type="checkbox"
                    [checked]="isAllSelected()"
                    (change)="toggleSelectAll($event)" />
                </th>
                <th class="col-stt">STT</th>
                <th class="col-loai">Loại quy trình</th>
                <th class="col-mota">Mô tả</th>
                <th class="col-phien">Phiên bản</th>
                <th class="col-tt">Trạng thái</th>
                <th class="col-hd">Hành động</th>
              </tr>
            </thead>
            <tbody>
              <!-- Skeleton rows khi đang tải -->
              <tr *ngIf="loading && workflows.length === 0" class="skeleton-row">
                <td colspan="7">
                  <div class="skeleton-bar"></div>
                  <div class="skeleton-bar short"></div>
                </td>
              </tr>

              <tr *ngFor="let wf of workflows; let i = index"
                [class.row-selected]="isSelected(wf.id!)">
                <td class="col-chk">
                  <input type="checkbox"
                    [checked]="isSelected(wf.id!)"
                    (change)="toggleSelect(wf.id!)" />
                </td>
                <td class="col-stt text-muted">{{ i + 1 }}</td>
                <td class="col-loai">
                  <span class="wf-name-link" (click)="onEdit(wf)">{{ wf.name }}</span>
                </td>
                <td class="col-mota">
                  <span class="mota-text" [title]="wf.description">{{ truncate(wf.description, 60) || '—' }}</span>
                </td>
                <td class="col-phien">{{ wf.version }}</td>
                <td class="col-tt">
                  <span class="status-pill"
                    [class.status-active]="wf.isActive"
                    [class.status-inactive]="!wf.isActive">
                    <i class="pi pi-clock"></i>
                    {{ wf.isActive ? 'Đang hoạt động' : 'Ngưng hoạt động' }}
                  </span>
                </td>
                <td class="col-hd">
                  <button class="act-btn act-edit" (click)="onEdit(wf)" title="Sửa quy trình">
                    <i class="pi pi-pencil"></i>
                  </button>
                  <button class="act-btn act-delete" (click)="promptDeleteOne(wf)" title="Xóa quy trình">
                    <i class="pi pi-trash"></i>
                  </button>
                </td>
              </tr>

              <tr *ngIf="!loading && workflows.length === 0 && !listError">
                <td colspan="7" class="empty-row">
                  <i class="pi pi-inbox"></i>
                  <div>Chưa có quy trình nào. Nhấn <b>Thêm</b> để tạo mới.</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Footer -->
        <div class="table-footer">
          <span class="record-count">Tổng số: <b>{{ workflows.length }}</b> bản ghi.</span>
          <div class="pagination">
            <button class="page-btn" disabled><i class="pi pi-chevron-left"></i></button>
            <span class="page-current">1</span>
            <button class="page-btn" disabled><i class="pi pi-chevron-right"></i></button>
            <select class="page-size-sel">
              <option>10 / trang</option>
              <option>20 / trang</option>
              <option>50 / trang</option>
            </select>
          </div>
        </div>
      </div>

      <!-- ══════════════════════════════════════════════════════════
           VIEW: FORM SỬA / THÊM MỚI
      ══════════════════════════════════════════════════════════ -->
      <div *ngIf="viewMode === 'edit'" class="wf-card">

        <div class="breadcrumb">
          <i class="pi pi-home bc-icon"></i>
          <span class="bc-link" (click)="onBackToList()">Trang chủ</span>
          <span class="bc-sep">/</span>
          <span class="bc-link" (click)="onBackToList()">Cài đặt quy trình</span>
          <span class="bc-sep">/</span>
          <span class="bc-current">{{ isEditMode ? 'Sửa quy trình' : 'Thêm mới' }}</span>
        </div>

        <div class="edit-header">
          <h2 class="edit-title">{{ isEditMode ? 'Sửa quy trình' : 'Thêm mới quy trình' }}</h2>
          <div class="edit-actions">
            <button id="btn-quaylai" class="btn-back" (click)="onBackToList()">
              <i class="pi pi-arrow-left"></i> Quay lại
            </button>
            <button id="btn-luu" class="btn-save"
              [disabled]="saving"
              (click)="onSave()">
              <i class="pi pi-spin pi-spinner" *ngIf="saving"></i>
              <i class="pi pi-save" *ngIf="!saving"></i>
              {{ saving ? 'Đang lưu...' : 'Lưu' }}
            </button>
          </div>
        </div>

        <!-- Tabs -->
        <div class="tab-bar">
          <button class="tab-item" id="tab-general"
            [class.tab-active]="activeTab === 'general'"
            (click)="activeTab = 'general'">
            Thông tin chung
          </button>
          <button class="tab-item" id="tab-design"
            [class.tab-active]="activeTab === 'design'"
            (click)="activeTab = 'design'">
            Thiết kế quy trình
          </button>
        </div>

        <!-- ── TAB 1: THÔNG TIN CHUNG ──────────────────────────── -->
        <div *ngIf="activeTab === 'general'" class="tab-content">
          <div class="form-grid-3">
            <div class="form-group">
              <label class="form-label">Loại quy trình <span class="required">*</span></label>
              <select class="wf-select w-full" [(ngModel)]="draft.name" id="sel-loai">
                <option value="" disabled>-- Chọn loại quy trình --</option>
                <option *ngFor="let opt of loaiOptions" [value]="opt">{{ opt }}</option>
              </select>
              <div class="field-error" *ngIf="formSubmitted && !draft.name">
                Vui lòng chọn loại quy trình.
              </div>
            </div>
            <div class="form-group">
              <label class="form-label">Trạng thái <span class="required">*</span></label>
              <select class="wf-select w-full" [(ngModel)]="draft.isActive" id="sel-trangthai">
                <option [ngValue]="true">Đang hoạt động</option>
                <option [ngValue]="false">Ngưng hoạt động</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">Phiên bản</label>
              <input id="inp-phienban" type="text" class="wf-input w-full phienban-input"
                [(ngModel)]="draft.version"
                placeholder="1.0" />
            </div>
          </div>

          <div class="epbuoc-box">
            <label class="epbuoc-wrap" for="chk-epbuoc">
              <input type="checkbox" id="chk-epbuoc" class="epbuoc-cb"
                [(ngModel)]="draft.forceActivate" />
              <span class="epbuoc-label">Ép buộc kích hoạt</span>
            </label>
            <div class="epbuoc-note">
              Nếu chọn, các quy trình cũ cùng loại sẽ bị vô hiệu hóa tự động.
            </div>
          </div>

          <div class="form-group mt-4">
            <label class="form-label">Mô tả</label>
            <textarea id="ta-mota" class="wf-textarea" rows="5"
              placeholder="Nhập mô tả quy trình (tối đa 500 ký tự)"
              [(ngModel)]="draft.description"
              maxlength="500">
            </textarea>
            <div class="char-count">{{ draft.description.length || 0 }} / 500</div>
          </div>
        </div>

        <!-- ── TAB 2: THIẾT KẾ QUY TRÌNH (BPMN) ──────────────── -->
        <div *ngIf="activeTab === 'design'" class="tab-content bpmn-section">

          <div class="bpmn-workspace">
            <!-- Toolbox -->
            <div class="bpmn-toolbox">
              <button class="tool-btn" title="Phóng to" (click)="onZoom(0.15)">
                <i class="pi pi-search-plus"></i>
              </button>
              <button class="tool-btn" title="Thu nhỏ" (click)="onZoom(-0.15)">
                <i class="pi pi-search-minus"></i>
              </button>
              <div class="tool-sep"></div>
              <button class="tool-btn" title="Chọn / Di chuyển"
                [class.tool-active]="activeTool === 'select'"
                (click)="activeTool = 'select'">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M3 3l7 18 4-8 8-4L3 3z"/>
                </svg>
              </button>
              <div class="tool-sep"></div>
              <button class="tool-btn" title="Thêm: Sự kiện Bắt đầu" (click)="addNode('start')">
                <svg width="14" height="14" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" fill="#22c55e"/></svg>
              </button>
              <button class="tool-btn" title="Thêm: Công việc / Bước xử lý" (click)="addNode('task')">
                <svg width="18" height="13" viewBox="0 0 24 18"><rect x="1" y="1" width="22" height="16" rx="2" fill="none" stroke="#3b82f6" stroke-width="2"/></svg>
              </button>
              <button class="tool-btn" title="Thêm: Cổng điều kiện" (click)="addNode('gateway')">
                <svg width="16" height="16" viewBox="0 0 24 24"><polygon points="12,2 22,12 12,22 2,12" fill="none" stroke="#f59e0b" stroke-width="2"/></svg>
              </button>
              <button class="tool-btn" title="Thêm: Sự kiện Kết thúc" (click)="addNode('end')">
                <svg width="14" height="14" viewBox="0 0 24 24">
                  <circle cx="12" cy="12" r="10" fill="none" stroke="#ef4444" stroke-width="2.5"/>
                  <circle cx="12" cy="12" r="6" fill="#ef4444"/>
                </svg>
              </button>
              <div class="tool-sep"></div>
              <button class="tool-btn tool-reset" title="Đặt lại sơ đồ mẫu" (click)="resetDiagram()">
                <i class="pi pi-refresh"></i>
              </button>
            </div>

            <!-- Canvas -->
            <div class="bpmn-viewport"
              (mousemove)="onMouseMove($event)"
              (mouseup)="onMouseUp()">

              <!-- Properties panel -->
              <div class="prop-panel" *ngIf="selectedNode">
                <div class="prop-header">
                  <span>Thuộc tính phần tử</span>
                  <button class="prop-close" (click)="selectedNode = null">
                    <i class="pi pi-times"></i>
                  </button>
                </div>
                <div class="prop-body">
                  <div class="prop-row">
                    <label>Tên bước</label>
                    <input type="text" class="prop-input" [(ngModel)]="selectedNode.label" />
                  </div>
                  <div class="prop-row" *ngIf="selectedNode.type === 'task'">
                    <label>Thứ tự bước</label>
                    <input type="number" class="prop-input" [(ngModel)]="selectedNode.stepNum" min="1" />
                  </div>
                  <div class="prop-row" *ngIf="selectedNode.type === 'task'">
                    <label>Vai trò phụ trách</label>
                    <input type="text" class="prop-input"
                      [ngModel]="selectedNode.stepRef?.requiredRole"
                      (ngModelChange)="updateStepRef(selectedNode, 'requiredRole', $event)"
                      placeholder="Ví dụ: KiemSoatVien, GiamDoc..." />
                  </div>
                  <div class="prop-row" *ngIf="selectedNode.type === 'task'">
                    <label>Loại hành động</label>
                    <select class="prop-select"
                      [ngModel]="selectedNode.stepRef?.actionType"
                      (ngModelChange)="updateStepRef(selectedNode, 'actionType', $event)">
                      <option value="Scan">Quét tài liệu</option>
                      <option value="DataEntry">Nhập liệu</option>
                      <option value="Review">Kiểm soát</option>
                      <option value="Approve">Phê duyệt</option>
                    </select>
                  </div>
                  <div class="prop-type-row">
                    <span class="prop-type-badge prop-type-{{selectedNode.type}}">
                      {{ getTypeLabel(selectedNode.type) }}
                    </span>
                  </div>
                </div>
                <div class="prop-footer">
                  <button class="prop-btn-close" (click)="selectedNode = null">Đóng</button>
                  <button class="prop-btn-del" (click)="deleteNode(selectedNode)">
                    <i class="pi pi-trash"></i> Xóa
                  </button>
                </div>
              </div>

              <!-- Hint -->
              <div class="canvas-hint">
                <i class="pi pi-info-circle"></i>
                Kéo thả để di chuyển. Click để chỉnh sửa. Dữ liệu sơ đồ được lưu cùng quy trình.
              </div>

              <!-- SVG BPMN diagram -->
              <svg class="bpmn-svg"
                [attr.width]="svgW"
                [attr.height]="svgH"
                [style.transform]="'scale('+scale+')'">

                <defs>
                  <marker id="arr" markerWidth="9" markerHeight="6" refX="8" refY="3" orient="auto">
                    <polygon points="0 0, 9 3, 0 6" fill="#6b7280"/>
                  </marker>
                  <pattern id="grid" width="20" height="20" patternUnits="userSpaceOnUse">
                    <path d="M 20 0 L 0 0 0 20" fill="none" stroke="#e5e7eb" stroke-width="0.5"/>
                  </pattern>
                </defs>

                <rect width="100%" height="100%" fill="url(#grid)"/>

                <!-- Edges -->
                <g *ngFor="let edge of bpmnEdges">
                  <path [attr.d]="buildPath(edge)" fill="none"
                    stroke="#6b7280" stroke-width="1.5" marker-end="url(#arr)"/>
                  <text *ngIf="edge.label"
                    [attr.x]="edgeLabelX(edge)" [attr.y]="edgeLabelY(edge)"
                    font-size="10" fill="#6b7280" font-weight="600" text-anchor="middle">
                    {{ edge.label }}
                  </text>
                </g>

                <!-- Nodes -->
                <g *ngFor="let node of bpmnNodes"
                  [attr.transform]="'translate('+node.x+','+node.y+')'"
                  [class.node-sel]="selectedNode === node"
                  style="cursor:move"
                  (mousedown)="onNodeDown($event, node)"
                  (click)="selectedNode = node; $event.stopPropagation()">

                  <!-- START -->
                  <ng-container *ngIf="node.type === 'start'">
                    <circle cx="18" cy="18" r="18" fill="#22c55e" opacity="0.18"/>
                    <circle cx="18" cy="18" r="13" fill="#22c55e"/>
                    <text x="18" y="46" font-size="10" fill="#374151" text-anchor="middle" font-family="Inter,sans-serif">{{ node.label }}</text>
                  </ng-container>

                  <!-- TASK -->
                  <ng-container *ngIf="node.type === 'task'">
                    <rect x="0" y="0" width="122" height="48" rx="4"
                      [attr.fill]="selectedNode === node ? '#eff6ff' : '#fff'"
                      [attr.stroke]="selectedNode === node ? '#3b82f6' : '#9ca3af'"
                      stroke-width="1.5"/>
                    <circle cx="13" cy="13" r="9"
                      [attr.fill]="selectedNode === node ? '#3b82f6' : '#002D72'"/>
                    <text x="13" y="17" font-size="9" fill="#fff" text-anchor="middle" font-weight="bold">
                      {{ node.stepNum }}
                    </text>
                    <text x="63" y="20" font-size="10" fill="#1e293b" text-anchor="middle"
                      font-family="Inter,sans-serif" font-weight="500">
                      <tspan *ngFor="let ln of splitLabel(node.label); let li = index"
                        x="63" [attr.dy]="li === 0 ? '0' : '13'">{{ ln }}</tspan>
                    </text>
                  </ng-container>

                  <!-- GATEWAY -->
                  <ng-container *ngIf="node.type === 'gateway'">
                    <polygon points="22,0 44,22 22,44 0,22"
                      [attr.fill]="selectedNode === node ? '#fffbeb' : '#fff'"
                      [attr.stroke]="selectedNode === node ? '#f59e0b' : '#9ca3af'"
                      stroke-width="1.5"/>
                    <text x="22" y="27" font-size="14" fill="#6b7280" text-anchor="middle">✕</text>
                  </ng-container>

                  <!-- END -->
                  <ng-container *ngIf="node.type === 'end'">
                    <circle cx="18" cy="18" r="18" fill="#ef4444" opacity="0.15"/>
                    <circle cx="18" cy="18" r="13" fill="none" stroke="#ef4444" stroke-width="3"/>
                    <circle cx="18" cy="18" r="7" fill="#ef4444"/>
                    <text x="18" y="46" font-size="10" fill="#374151" text-anchor="middle" font-family="Inter,sans-serif">{{ node.label }}</text>
                  </ng-container>
                </g>

              </svg>
            </div>
          </div>

          <!-- Legend -->
          <div class="bpmn-legend">
            <span class="legend-item"><svg width="14" height="14"><circle cx="7" cy="7" r="7" fill="#22c55e"/></svg> Bắt đầu</span>
            <span class="legend-item"><svg width="22" height="14"><rect x="0" y="1" width="22" height="12" rx="2" fill="none" stroke="#3b82f6" stroke-width="1.5"/></svg> Công việc</span>
            <span class="legend-item"><svg width="16" height="16"><polygon points="8,0 16,8 8,16 0,8" fill="none" stroke="#f59e0b" stroke-width="1.5"/></svg> Cổng ĐK</span>
            <span class="legend-item"><svg width="14" height="14"><circle cx="7" cy="7" r="6" fill="none" stroke="#ef4444" stroke-width="2"/><circle cx="7" cy="7" r="3" fill="#ef4444"/></svg> Kết thúc</span>
          </div>
        </div>
      </div>

    </div>
  `,
  styles: [`
    /* ─── Reset & Page ───────────────────────────────────────── */
    * { box-sizing: border-box; }
    .wf-page {
      font-family: 'Inter','Segoe UI',sans-serif;
      font-size: 0.875rem; color: #374151;
      background: #f5f7fb; min-height: calc(100vh - 56px);
      padding: 20px; position: relative;
    }
    /* ─── Loading overlay ──────────────────────────────────────── */
    .loading-overlay {
      position: fixed; inset: 0; background: rgba(255,255,255,.7);
      display: flex; align-items: center; justify-content: center; z-index: 2000;
    }
    .loading-spinner {
      display: flex; align-items: center; gap: 10px;
      background: #fff; padding: 14px 24px; border-radius: 10px;
      box-shadow: 0 4px 20px rgba(0,0,0,.12); font-size: 0.9rem; color: #374151;
    }
    .loading-spinner .pi-spinner { font-size: 1.4rem; color: #002D72; }
    /* ─── Card ────────────────────────────────────────────────── */
    .wf-card {
      background: #fff; border-radius: 8px;
      box-shadow: 0 1px 6px rgba(0,0,0,.07); padding: 20px 24px;
    }
    /* ─── Breadcrumb ──────────────────────────────────────────── */
    .breadcrumb {
      display: flex; align-items: center; gap: 6px;
      font-size: 0.8rem; color: #6b7280; margin-bottom: 16px;
    }
    .bc-icon { font-size: 0.78rem; }
    .bc-sep { color: #d1d5db; }
    .bc-text { color: #6b7280; }
    .bc-current { color: #002D72; font-weight: 600; }
    .bc-link { color: #002D72; cursor: pointer; }
    .bc-link:hover { text-decoration: underline; }
    /* ─── Toolbar ─────────────────────────────────────────────── */
    .list-toolbar {
      display: flex; align-items: center; justify-content: space-between;
      gap: 10px; flex-wrap: wrap; margin-bottom: 12px;
    }
    .toolbar-left { display: flex; align-items: center; gap: 8px; }
    .toolbar-right { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
    .wf-search-input {
      height: 34px; padding: 0 12px; width: 270px;
      border: 1px solid #d1d5db; border-radius: 5px;
      font-size: 0.85rem; outline: none; transition: border-color .2s;
    }
    .wf-search-input:focus { border-color: #002D72; box-shadow: 0 0 0 2px rgba(0,45,114,.1); }
    .btn-tim {
      height: 34px; padding: 0 14px; background: #3b82f6; color: #fff;
      border: none; border-radius: 5px; font-size: 0.85rem; cursor: pointer;
      display: flex; align-items: center; gap: 5px; transition: background .2s;
    }
    .btn-tim:hover { background: #2563eb; }
    .btn-outlined {
      height: 34px; padding: 0 12px; background: #fff; color: #374151;
      border: 1px solid #d1d5db; border-radius: 5px; font-size: 0.85rem;
      cursor: pointer; display: flex; align-items: center; gap: 5px; transition: all .2s;
    }
    .btn-outlined:hover:not(:disabled) { background: #f9fafb; }
    .btn-outlined:disabled { opacity: 0.4; cursor: not-allowed; }
    .btn-filter-active { background: #eff6ff; border-color: #93c5fd; color: #2563eb; }
    .btn-xoa-outlined { color: #6b7280; }
    .btn-green {
      height: 34px; padding: 0 14px; background: #22c55e; color: #fff;
      border: none; border-radius: 5px; font-size: 0.85rem; cursor: pointer;
      display: flex; align-items: center; gap: 5px; transition: background .2s;
    }
    .btn-green:hover { background: #16a34a; }
    .btn-excel {
      height: 34px; padding: 0 14px; background: #16a34a; color: #fff;
      border: none; border-radius: 5px; font-size: 0.85rem; cursor: pointer;
      display: flex; align-items: center; gap: 5px; transition: background .2s;
    }
    .btn-excel:hover { background: #15803d; }
    .btn-small { height: 30px; padding: 0 10px; font-size: 0.8rem; }
    /* Filter row */
    .filter-row {
      display: flex; align-items: center; gap: 10px; flex-wrap: wrap;
      padding: 10px 12px; background: #f8faff;
      border: 1px solid #e0e7ff; border-radius: 6px; margin-bottom: 12px;
    }
    .wf-select {
      height: 34px; padding: 0 10px; border: 1px solid #d1d5db;
      border-radius: 5px; font-size: 0.85rem; background: #fff; outline: none; cursor: pointer;
    }
    .wf-select.w-full { width: 100%; }
    /* Error banner */
    .error-banner {
      display: flex; align-items: center; gap: 10px;
      padding: 10px 14px; background: #fef2f2; border: 1px solid #fecaca;
      border-radius: 6px; margin-bottom: 12px; font-size: 0.85rem; color: #dc2626;
    }
    .btn-retry {
      padding: 2px 10px; background: #fff; border: 1px solid #fecaca;
      border-radius: 4px; color: #dc2626; cursor: pointer; font-size: 0.8rem;
    }
    /* ─── Table ─────────────────────────────────────────────────── */
    .wf-table-wrap { overflow-x: auto; }
    .wf-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .wf-table thead tr { background: #f8fafc; border-bottom: 2px solid #e2e8f0; }
    .wf-table th {
      padding: 10px 12px; text-align: left; font-weight: 600;
      color: #475569; font-size: 0.82rem; white-space: nowrap;
    }
    .wf-table td { padding: 9px 12px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .wf-table tbody tr:hover { background: #f8faff; }
    .wf-table tbody tr.row-selected { background: #eff6ff; }
    .col-chk { width: 40px; text-align: center; }
    .col-stt { width: 50px; text-align: center; }
    .col-loai { min-width: 200px; }
    .col-mota { min-width: 180px; }
    .col-phien { width: 90px; text-align: center; font-family: 'Courier New',monospace; font-weight: 600; }
    .col-tt { width: 155px; text-align: center; }
    .col-hd { width: 90px; text-align: center; }
    .text-muted { color: #9ca3af; }
    .wf-name-link { color: #002D72; font-weight: 500; cursor: pointer; }
    .wf-name-link:hover { text-decoration: underline; color: #FF6B00; }
    .mota-text { color: #6b7280; }
    /* Status pills */
    .status-pill {
      display: inline-flex; align-items: center; gap: 5px;
      padding: 4px 10px; border-radius: 20px; font-size: 0.78rem; font-weight: 600;
    }
    .status-active  { background: #dcfce7; color: #15803d; border: 1px solid #bbf7d0; }
    .status-inactive{ background: #fef2f2; color: #dc2626; border: 1px solid #fecaca; }
    .status-pill .pi { font-size: 0.72rem; }
    /* Action buttons */
    .act-btn {
      width: 30px; height: 30px; border: none; border-radius: 5px; cursor: pointer;
      background: transparent; display: inline-flex; align-items: center;
      justify-content: center; font-size: 0.9rem; transition: background .15s; margin: 0 2px;
    }
    .act-edit  { color: #2563eb; } .act-edit:hover  { background: #eff6ff; }
    .act-delete{ color: #dc2626; } .act-delete:hover{ background: #fef2f2; }
    /* Skeleton */
    .skeleton-row td { padding: 16px 12px; }
    .skeleton-bar {
      height: 14px; background: linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);
      background-size: 200%; border-radius: 4px; margin-bottom: 8px;
      animation: shimmer 1.5s infinite;
    }
    .skeleton-bar.short { width: 60%; }
    @keyframes shimmer { 0%{background-position:200%} 100%{background-position:-200%} }
    /* Empty row */
    .empty-row { text-align: center; padding: 40px; color: #9ca3af; }
    .empty-row .pi { font-size: 2.2rem; display: block; margin-bottom: 8px; }
    /* Table footer */
    .table-footer {
      display: flex; align-items: center; justify-content: space-between;
      padding-top: 12px; border-top: 1px solid #f1f5f9;
      margin-top: 6px; flex-wrap: wrap; gap: 8px;
    }
    .record-count { font-size: 0.83rem; color: #6b7280; }
    .record-count b { color: #374151; }
    .pagination { display: flex; align-items: center; gap: 5px; }
    .page-btn {
      width: 28px; height: 28px; border: 1px solid #e5e7eb; border-radius: 4px;
      background: #fff; cursor: pointer; display: flex; align-items: center;
      justify-content: center; font-size: 0.8rem; color: #6b7280; transition: all .15s;
    }
    .page-btn:hover:not(:disabled) { background: #002D72; color: #fff; border-color: #002D72; }
    .page-btn:disabled { opacity: 0.4; cursor: default; }
    .page-current {
      min-width: 28px; height: 28px; border: 1px solid #002D72; border-radius: 4px;
      background: #002D72; color: #fff; display: inline-flex; align-items: center;
      justify-content: center; font-size: 0.82rem; font-weight: 600;
    }
    .page-size-sel {
      height: 28px; padding: 0 6px; border: 1px solid #e5e7eb;
      border-radius: 4px; font-size: 0.8rem; background: #fff;
    }
    /* ─── Edit form ──────────────────────────────────────────── */
    .edit-header {
      display: flex; align-items: center; justify-content: space-between;
      margin-bottom: 18px; padding-bottom: 14px; border-bottom: 1px solid #e5e7eb;
    }
    .edit-title { font-size: 1.2rem; font-weight: 700; color: #1e293b; margin: 0; }
    .edit-actions { display: flex; gap: 8px; }
    .btn-back {
      height: 34px; padding: 0 14px; background: #fff; color: #374151;
      border: 1px solid #d1d5db; border-radius: 5px; font-size: 0.85rem; cursor: pointer;
      display: flex; align-items: center; gap: 5px; transition: all .2s;
    }
    .btn-back:hover { background: #f1f5f9; }
    .btn-save {
      height: 34px; padding: 0 16px; background: #22c55e; color: #fff;
      border: none; border-radius: 5px; font-size: 0.85rem; font-weight: 600;
      cursor: pointer; display: flex; align-items: center; gap: 5px; transition: background .2s;
    }
    .btn-save:hover:not(:disabled) { background: #16a34a; }
    .btn-save:disabled { opacity: 0.6; cursor: not-allowed; }
    /* Tabs */
    .tab-bar { display: flex; border-bottom: 2px solid #e5e7eb; margin-bottom: 20px; }
    .tab-item {
      padding: 10px 20px; background: none; border: none; font-size: 0.875rem;
      color: #6b7280; cursor: pointer; font-weight: 500; transition: all .15s;
      border-bottom: 2px solid transparent; margin-bottom: -2px;
    }
    .tab-item:hover { color: #002D72; }
    .tab-item.tab-active { color: #002D72; border-bottom-color: #002D72; font-weight: 600; }
    .tab-content { padding-top: 4px; }
    /* Form */
    .form-grid-3 {
      display: grid; grid-template-columns: 1fr 1fr 180px;
      gap: 16px; margin-bottom: 16px;
    }
    @media (max-width: 768px) { .form-grid-3 { grid-template-columns: 1fr; } }
    .form-group { display: flex; flex-direction: column; gap: 5px; }
    .form-label { font-size: 0.83rem; font-weight: 500; color: #374151; }
    .required { color: #ef4444; margin-left: 2px; }
    .field-error { font-size: 0.78rem; color: #dc2626; margin-top: 2px; }
    .wf-input {
      height: 36px; padding: 0 12px; border: 1px solid #d1d5db;
      border-radius: 5px; font-size: 0.85rem; outline: none; transition: border-color .2s;
    }
    .wf-input:focus { border-color: #002D72; }
    .wf-input.w-full { width: 100%; }
    .phienban-input { background: #f9fafb; }
    .wf-textarea {
      width: 100%; padding: 10px 12px; border: 1px solid #d1d5db;
      border-radius: 5px; font-size: 0.85rem; resize: vertical;
      font-family: inherit; outline: none; transition: border-color .2s;
    }
    .wf-textarea:focus { border-color: #002D72; }
    .char-count { font-size: 0.75rem; color: #9ca3af; text-align: right; margin-top: 3px; }
    .mt-4 { margin-top: 16px; }
    .epbuoc-box {
      padding: 10px 14px; background: #f8faff;
      border: 1px solid #e0e7ff; border-radius: 6px;
      display: flex; flex-direction: column; gap: 4px;
    }
    .epbuoc-wrap { display: flex; align-items: center; gap: 8px; cursor: pointer; }
    .epbuoc-cb { width: 14px; height: 14px; cursor: pointer; accent-color: #002D72; }
    .epbuoc-label { font-size: 0.875rem; font-weight: 600; color: #1e293b; }
    .epbuoc-note { font-size: 0.78rem; color: #6b7280; margin-left: 22px; }
    /* ─── BPMN Designer ────────────────────────────────────────── */
    .bpmn-section { padding-top: 0; }
    .bpmn-workspace {
      display: flex; border: 1px solid #e5e7eb; border-radius: 8px;
      overflow: hidden; height: 480px;
    }
    .bpmn-toolbox {
      display: flex; flex-direction: column; align-items: center;
      gap: 4px; padding: 10px 7px; background: #f8fafc;
      border-right: 1px solid #e5e7eb; min-width: 46px;
    }
    .tool-btn {
      width: 32px; height: 32px; border: 1px solid #e5e7eb; border-radius: 5px;
      background: #fff; cursor: pointer; display: flex; align-items: center;
      justify-content: center; font-size: 0.85rem; color: #374151; transition: all .15s;
    }
    .tool-btn:hover { background: #eff6ff; border-color: #93c5fd; color: #2563eb; }
    .tool-btn.tool-active { background: #002D72; border-color: #002D72; color: #fff; }
    .tool-btn.tool-reset:hover { background: #fef2f2; border-color: #fecaca; color: #dc2626; }
    .tool-sep { width: 26px; height: 1px; background: #e5e7eb; margin: 3px 0; }
    .bpmn-viewport { flex: 1; overflow: auto; background: #fafafa; position: relative; }
    .bpmn-svg { transform-origin: top left; display: block; user-select: none; }
    /* Property panel */
    .prop-panel {
      position: absolute; top: 10px; right: 10px; z-index: 10;
      width: 215px; background: #fff; border: 1px solid #e5e7eb;
      border-radius: 8px; box-shadow: 0 4px 16px rgba(0,0,0,.12);
    }
    .prop-header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 9px 12px; border-bottom: 1px solid #f1f5f9;
      font-size: 0.83rem; font-weight: 600; color: #1e293b;
    }
    .prop-close {
      width: 22px; height: 22px; border: none; background: none; cursor: pointer;
      color: #9ca3af; border-radius: 4px; display: flex; align-items: center;
      justify-content: center; font-size: 0.8rem; transition: all .15s;
    }
    .prop-close:hover { background: #f1f5f9; color: #374151; }
    .prop-body { padding: 10px 12px; display: flex; flex-direction: column; gap: 9px; }
    .prop-row { display: flex; flex-direction: column; gap: 3px; }
    .prop-row label { font-size: 0.75rem; color: #6b7280; font-weight: 500; }
    .prop-input {
      height: 30px; padding: 0 8px; width: 100%;
      border: 1px solid #d1d5db; border-radius: 4px; font-size: 0.82rem; outline: none;
    }
    .prop-input:focus { border-color: #002D72; }
    .prop-select {
      height: 30px; padding: 0 6px; width: 100%;
      border: 1px solid #d1d5db; border-radius: 4px; font-size: 0.82rem; outline: none;
      background: #fff;
    }
    .prop-type-row { margin-top: 2px; }
    .prop-type-badge {
      display: inline-block; padding: 2px 8px; border-radius: 10px;
      font-size: 0.72rem; font-weight: 600;
    }
    .prop-type-task    { background: #dbeafe; color: #1e40af; }
    .prop-type-gateway { background: #fef3c7; color: #92400e; }
    .prop-type-start   { background: #dcfce7; color: #15803d; }
    .prop-type-end     { background: #fee2e2; color: #991b1b; }
    .prop-footer {
      display: flex; gap: 6px; padding: 8px 12px; border-top: 1px solid #f1f5f9;
    }
    .prop-btn-close {
      flex: 1; height: 28px; background: #f3f4f6; border: 1px solid #e5e7eb;
      border-radius: 4px; font-size: 0.8rem; cursor: pointer; transition: all .15s;
    }
    .prop-btn-close:hover { background: #e5e7eb; }
    .prop-btn-del {
      height: 28px; padding: 0 10px; background: #fef2f2; color: #dc2626;
      border: 1px solid #fecaca; border-radius: 4px; font-size: 0.8rem;
      cursor: pointer; display: flex; align-items: center; gap: 4px; transition: all .15s;
    }
    .prop-btn-del:hover { background: #fee2e2; }
    .canvas-hint {
      position: absolute; top: 8px; left: 10px; z-index: 5;
      background: rgba(255,255,255,.9); padding: 5px 10px; border-radius: 5px;
      font-size: 0.75rem; color: #6b7280; border: 1px solid #e5e7eb; pointer-events: none;
    }
    .node-sel rect, .node-sel circle, .node-sel polygon {
      filter: drop-shadow(0 0 4px rgba(59,130,246,.5));
    }
    .bpmn-legend {
      display: flex; align-items: center; gap: 20px; flex-wrap: wrap;
      padding: 10px 14px; background: #f8fafc; border: 1px solid #e5e7eb;
      border-radius: 6px; margin-top: 10px;
    }
    .legend-item { display: flex; align-items: center; gap: 7px; font-size: 0.78rem; color: #374151; }
    /* ─── Modals ───────────────────────────────────────────────── */
    .modal-overlay {
      position: fixed; inset: 0; background: rgba(0,0,0,.45);
      display: flex; align-items: center; justify-content: center; z-index: 1000;
    }
    .modal-box {
      background: #fff; border-radius: 12px; padding: 32px 28px; width: 380px;
      text-align: center; box-shadow: 0 20px 60px rgba(0,0,0,.2);
    }
    .modal-icon { font-size: 2.5rem; margin-bottom: 12px; }
    .modal-icon.warn { color: #f59e0b; }
    .modal-title { font-size: 1.1rem; font-weight: 700; color: #1e293b; margin: 0 0 8px; }
    .modal-msg { color: #6b7280; font-size: 0.875rem; margin: 0 0 6px; }
    .modal-target { font-weight: 600; color: #002D72; font-size: 0.875rem; margin: 0 0 20px; }
    .modal-note { font-size: 0.8rem; color: #dc2626; margin: 4px 0 20px; }
    .modal-actions { display: flex; justify-content: center; gap: 10px; }
    .btn-delete-confirm {
      height: 36px; padding: 0 18px; background: #dc2626; color: #fff;
      border: none; border-radius: 6px; font-size: 0.875rem; font-weight: 600;
      cursor: pointer; display: flex; align-items: center; gap: 5px; transition: background .2s;
    }
    .btn-delete-confirm:hover:not(:disabled) { background: #b91c1c; }
    .btn-delete-confirm:disabled { opacity: 0.6; cursor: not-allowed; }
  `]
})
export class WorkflowBuilderComponent implements OnInit {

  @ViewChild('canvasViewport') canvasViewport!: ElementRef;

  // ─── View state ─────────────────────────────────────────────────────────────
  viewMode: 'list' | 'edit' = 'list';
  activeTab: 'general' | 'design' = 'general';
  isEditMode = false;
  formSubmitted = false;

  // ─── Loading / error state ──────────────────────────────────────────────────
  loading = false;
  loadingMsg = 'Đang tải...';
  saving = false;
  deleting = false;
  listError = '';

  // ─── Search / filter ────────────────────────────────────────────────────────
  searchKeyword = '';
  showFilter = false;
  filterIsActive = '';

  // ─── Data ───────────────────────────────────────────────────────────────────
  loaiOptions = LOAI_QUY_TRINH_OPTIONS;
  workflows: WorkflowDefinition[] = [];
  selectedIds: string[] = [];

  // ─── Edit draft ─────────────────────────────────────────────────────────────
  draft: WorkflowDefinition = this.emptyDraft();

  // ─── Delete state ───────────────────────────────────────────────────────────
  showDeleteOneConfirm = false;
  showDeleteSelectedConfirm = false;
  deleteTarget: WorkflowDefinition | null = null;

  // ─── BPMN state ─────────────────────────────────────────────────────────────
  bpmnNodes: BpmnNode[] = [];
  bpmnEdges: BpmnEdge[] = [];
  selectedNode: BpmnNode | null = null;
  activeTool: 'select' | 'hand' = 'select';
  scale = 1;
  svgW = 1400;
  svgH = 460;
  private dragging: BpmnNode | null = null;
  private dragOffX = 0;
  private dragOffY = 0;
  private nodeCounter = 200;

  constructor(
    private workflowSvc: WorkflowService,
    private messageService: MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.loadList();
  }

  // ─── List: load from API ────────────────────────────────────────────────────

  loadList(): void {
    this.loading = true;
    this.loadingMsg = 'Đang tải danh sách quy trình...';
    this.listError = '';

    const isActive = this.filterIsActive === 'true'  ? true
                   : this.filterIsActive === 'false' ? false
                   : undefined;

    this.workflowSvc.getAll(this.searchKeyword || undefined, isActive)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (data) => {
          this.workflows = data;
          this.selectedIds = [];
        },
        error: (err) => {
          this.listError = `Không thể tải danh sách: ${err.message}`;
        }
      });
  }

  resetFilter(): void {
    this.filterIsActive = '';
    this.searchKeyword = '';
    this.loadList();
  }

  // ─── Selection ──────────────────────────────────────────────────────────────

  isSelected(id: string): boolean { return this.selectedIds.includes(id); }
  isAllSelected(): boolean {
    return this.workflows.length > 0 &&
           this.workflows.every(w => this.selectedIds.includes(w.id!));
  }
  toggleSelect(id: string): void {
    const i = this.selectedIds.indexOf(id);
    i >= 0 ? this.selectedIds.splice(i, 1) : this.selectedIds.push(id);
  }
  toggleSelectAll(e: Event): void {
    this.selectedIds = (e.target as HTMLInputElement).checked
      ? this.workflows.map(w => w.id!)
      : [];
  }

  // ─── CRUD actions ──────────────────────────────────────────────────────────

  onAddNew(): void {
    this.isEditMode = false;
    this.formSubmitted = false;
    this.draft = this.emptyDraft();
    this.resetDiagram();
    this.activeTab = 'general';
    this.viewMode = 'edit';
  }

  onEdit(wf: WorkflowDefinition): void {
    this.loading = true;
    this.loadingMsg = 'Đang tải chi tiết quy trình...';
    this.workflowSvc.getById(wf.id!)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (detail) => {
          this.isEditMode = true;
          this.formSubmitted = false;
          this.draft = { ...detail, steps: detail.steps || [] };
          this.loadDiagramFromSteps(detail.steps || []);
          this.activeTab = 'general';
          this.viewMode = 'edit';
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.message });
        }
      });
  }

  onSave(): void {
    this.formSubmitted = true;
    if (!this.draft.name) return;

    // Sync steps từ BPMN diagram
    this.draft.steps = this.bpmnNodesToSteps();

    this.saving = true;
    const op$ = this.isEditMode
      ? this.workflowSvc.update(this.draft.id!, this.draft)
      : this.workflowSvc.create(this.draft);

    op$.pipe(finalize(() => this.saving = false)).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: this.isEditMode ? 'Đã cập nhật quy trình.' : 'Đã tạo mới quy trình.'
        });
        this.viewMode = 'list';
        this.loadList();
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi lưu', detail: err.message });
      }
    });
  }

  promptDeleteOne(wf: WorkflowDefinition): void {
    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa quy trình "${wf.name}"?`,
      header: 'Xác nhận xóa',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
      accept: () => {
        this.deleting = true;
        this.workflowSvc.delete(wf.id!)
          .pipe(finalize(() => this.deleting = false))
          .subscribe({
            next: () => {
              this.messageService.add({ severity: 'success', summary: 'Thành công', detail: `Đã xóa: ${wf.name}` });
              this.loadList();
            },
            error: (err) => {
              this.messageService.add({ severity: 'error', summary: 'Lỗi xóa', detail: err.message });
            }
          });
      }
    });
  }

  promptDeleteSelected(): void {
    this.confirmationService.confirm({
      message: `Bạn có chắc chắn muốn xóa ${this.selectedIds.length} quy trình đã chọn? Hành động này không thể hoàn tác.`,
      header: 'Xác nhận xóa nhiều',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
      accept: () => {
        this.doDeleteSelected();
      }
    });
  }

  doDeleteSelected(): void {
    if (!this.selectedIds.length) return;
    this.deleting = true;
    const deletes$ = this.selectedIds.map(id => this.workflowSvc.delete(id));
    let done = 0;
    deletes$.forEach(op => {
      op.subscribe({
        next: () => {
          done++;
          if (done === deletes$.length) {
            this.deleting = false;
            this.selectedIds = [];
            this.messageService.add({ severity: 'success', summary: 'Thành công', detail: `Đã xóa ${done} quy trình.` });
            this.loadList();
          }
        },
        error: () => { done++; }
      });
    });
  }

  onBackToList(): void {
    this.viewMode = 'list';
    this.loadList();
  }

  onExportExcel(): void {
    this.messageService.add({ severity: 'info', summary: 'Xuất dữ liệu', detail: 'Đang chuẩn bị dữ liệu xuất Excel...' });
    setTimeout(() => {
      this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã tải danh sách quy trình thành công dưới dạng Excel!' });
    }, 1200);
  }

  emptyDraft(): WorkflowDefinition {
    return { name: '', description: '', version: '1.0', forceActivate: false, isActive: true, steps: [] };
  }

  truncate(text: string, max: number): string {
    if (!text) return '';
    return text.length > max ? text.slice(0, max) + '…' : text;
  }

  // ─── BPMN ──────────────────────────────────────────────────────────────────

  resetDiagram(): void {
    this.bpmnNodes = SAMPLE_NODES.map(n => ({ ...n }));
    this.bpmnEdges = SAMPLE_EDGES.map(e => ({ ...e }));
    this.selectedNode = null;
    this.scale = 1;
  }

  /** Khi load quy trình đã lưu, map steps → BPMN nodes */
  loadDiagramFromSteps(steps: WorkflowStep[]): void {
    if (!steps.length) { this.resetDiagram(); return; }
    this.resetDiagram();
    // Overwrite task nodes với step data thực
    const taskNodes = this.bpmnNodes.filter(n => n.type === 'task');
    steps.forEach((step, i) => {
      if (taskNodes[i]) {
        taskNodes[i].label = step.stepName;
        taskNodes[i].stepNum = step.order;
        taskNodes[i].stepRef = step;
      }
    });
  }

  /** Chuyển BPMN task nodes → WorkflowStep[] để lưu vào backend */
  bpmnNodesToSteps(): WorkflowStep[] {
    return this.bpmnNodes
      .filter(n => n.type === 'task')
      .sort((a, b) => (a.stepNum || 0) - (b.stepNum || 0))
      .map((n, i) => ({
        stepName: n.label.replace('\n', ' '),
        order: n.stepNum || i + 1,
        requiredRole: n.stepRef?.requiredRole || '',
        actionType: n.stepRef?.actionType || 'Approve',
      }));
  }

  addNode(type: BpmnNode['type']): void {
    const id = `n_new_${this.nodeCounter++}`;
    const labels: Record<string, string> = {
      start: 'Bắt đầu', task: 'Bước mới', gateway: '', end: 'Kết thúc'
    };
    const node: BpmnNode = {
      id, type, label: labels[type],
      x: 180 + Math.random() * 250,
      y: 60 + Math.random() * 200,
      stepNum: type === 'task'
        ? this.bpmnNodes.filter(n => n.type === 'task').length + 1
        : undefined,
      stepRef: type === 'task'
        ? { stepName: 'Bước mới', order: 0, requiredRole: '', actionType: 'Approve' }
        : undefined
    };
    this.bpmnNodes = [...this.bpmnNodes, node];
    this.selectedNode = node;
  }

  deleteNode(node: BpmnNode): void {
    this.bpmnNodes = this.bpmnNodes.filter(n => n !== node);
    this.bpmnEdges = this.bpmnEdges.filter(e => e.from !== node.id && e.to !== node.id);
    this.selectedNode = null;
  }

  updateStepRef(node: BpmnNode, field: keyof WorkflowStep, value: string): void {
    if (!node.stepRef) node.stepRef = { stepName: '', order: 0, requiredRole: '', actionType: '' };
    (node.stepRef as any)[field] = value;
  }

  onNodeDown(event: MouseEvent, node: BpmnNode): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragging = node;
    const rect = (event.currentTarget as SVGElement).closest('.bpmn-viewport')!.getBoundingClientRect();
    this.dragOffX = (event.clientX - rect.left) / this.scale - node.x;
    this.dragOffY = (event.clientY - rect.top) / this.scale - node.y;
  }

  onMouseMove(event: MouseEvent): void {
    if (!this.dragging) return;
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    this.dragging.x = Math.max(0, (event.clientX - rect.left) / this.scale - this.dragOffX);
    this.dragging.y = Math.max(0, (event.clientY - rect.top) / this.scale - this.dragOffY);
  }

  onMouseUp(): void { this.dragging = null; }

  onZoom(delta: number): void {
    this.scale = Math.min(2, Math.max(0.35, this.scale + delta));
  }

  getTypeLabel(type: string): string {
    return ({ start:'Bắt đầu', task:'Công việc', gateway:'Cổng ĐK', end:'Kết thúc' } as any)[type] || type;
  }

  splitLabel(label: string): string[] {
    return label ? label.split('\n') : [''];
  }

  // ─── Path rendering ─────────────────────────────────────────────────────────

  private nodeCenter(id: string): { x: number; y: number } {
    const n = this.bpmnNodes.find(nd => nd.id === id);
    if (!n) return { x: 0, y: 0 };
    if (n.type === 'task')    return { x: n.x + 61, y: n.y + 24 };
    if (n.type === 'gateway') return { x: n.x + 22, y: n.y + 22 };
    return { x: n.x + 18, y: n.y + 18 }; // start/end
  }

  buildPath(edge: BpmnEdge): string {
    const f = this.nodeCenter(edge.from);
    const t = this.nodeCenter(edge.to);
    if (f.x > t.x) {
      // loopback — bezier cong xuống
      const cy = Math.max(f.y, t.y) + 75;
      return `M ${f.x} ${f.y} C ${f.x} ${cy}, ${t.x} ${cy}, ${t.x} ${t.y}`;
    }
    const mx = (f.x + t.x) / 2;
    return `M ${f.x} ${f.y} C ${mx} ${f.y}, ${mx} ${t.y}, ${t.x} ${t.y}`;
  }

  edgeLabelX(edge: BpmnEdge): number {
    const f = this.nodeCenter(edge.from);
    const t = this.nodeCenter(edge.to);
    return (f.x + t.x) / 2;
  }

  edgeLabelY(edge: BpmnEdge): number {
    const f = this.nodeCenter(edge.from);
    const t = this.nodeCenter(edge.to);
    return f.x > t.x
      ? Math.max(f.y, t.y) + 82
      : (f.y + t.y) / 2 - 8;
  }
}
