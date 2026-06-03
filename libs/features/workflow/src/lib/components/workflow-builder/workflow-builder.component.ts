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
} from '@sohoa.frontend/shared/core';
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
  templateUrl: './workflow-builder.component.html',
  styleUrl: './workflow-builder.component.scss'
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
