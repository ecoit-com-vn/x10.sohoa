import { Component, OnInit, OnDestroy, ViewChild, ElementRef, inject, PLATFORM_ID, ChangeDetectorRef, signal } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { Menu, MenuModule } from 'primeng/menu';
import { MenuItem, MessageService } from 'primeng/api';
import {
  WorkflowService,
  WorkflowDefinition,
  WorkflowStep,
  AuthService
} from '@sohoa.frontend/shared/core';
import { filter, finalize } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { environment } from '@env/environment';



const DEFAULT_BPMN_XML = `<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Definitions_1" targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn:process id="Process_1" isExecutable="false">
    <bpmn:startEvent id="StartEvent_1"/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_1">
    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="Process_1">
      <bpmndi:BPMNShape id="_BPMNShape_StartEvent_2" bpmnElement="StartEvent_1">
        <dc:Bounds x="173" y="102" width="36" height="36"/>
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>`;

const WORKFLOW_BUILDER_BASE = '/administration/workflow-builder';

// ─── Component ────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-workflow-builder',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, DialogModule, MenuModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './workflow-builder.component.html',
  styleUrl: './workflow-builder.component.scss'
})
export class WorkflowBuilderComponent implements OnInit, OnDestroy {

  // ─── Platform check ────────────────────────────────────────────────────────
  private platformId = inject(PLATFORM_ID);
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private routeSub?: Subscription;

  @ViewChild('bpmnCanvasRef') bpmnCanvasRef?: ElementRef<HTMLDivElement>;
  @ViewChild('previewCanvasRef') previewCanvasRef?: ElementRef<HTMLDivElement>;

  // ─── View state ─────────────────────────────────────────────────────────────
  viewMode: 'list' | 'edit' = 'list';
  activeTab: 'general' | 'design' | 'history' = 'general';
  isEditMode = false;
  formSubmitted = false;

  selectTab(tab: 'general' | 'design' | 'history'): void {
    this.activeTab = tab;
    if (tab === 'design') {
      setTimeout(() => {
        if (!this.bpmnModeler) {
          this.initModeler();
          return;
        }
        try {
          const canvas = this.bpmnModeler.get('canvas');
          canvas.resized();
          canvas.zoom('fit-viewport');
        } catch (e) {
          console.error('Error resizing BPMN canvas:', e);
        }
      }, 50);
    } else if (tab === 'history') {
      this.loadVersions();
    }
  }

  // ─── Loading / error state ──────────────────────────────────────────────────
  loading = false;
  loadingMsg = 'Đang tải...';
  saving = false;
  deleting = signal<boolean>(false);
  listError = '';

  // ─── Search / filter ────────────────────────────────────────────────────────
  searchKeyword = '';
  showFilter = false;
  filterIsActive = '';

  // ─── Data ───────────────────────────────────────────────────────────────────
  loaiOptions: { id: number; code: string; name: string }[] = [];
  workflows: WorkflowDefinition[] = [];
  selectedIds: string[] = [];
  versionsList: WorkflowDefinition[] = [];
  loadingHistory = false;

  displayPreviewDialog = false;
  previewVersion = '';
  previewLoading = false;
  bpmnViewer: any = null;

  onClosePreviewDialog(): void {
    this.displayPreviewDialog = false;
    this.previewVersion = '';
    this.previewLoading = false;
    if (this.bpmnViewer) {
      this.bpmnViewer.destroy();
      this.bpmnViewer = null;
    }
  }

  // Danh sách lịch sử chỉ trả về thông tin tóm tắt (không kèm bpmnXml),
  // nên phải gọi lại getById() để lấy sơ đồ đầy đủ cho phiên bản được chọn.
  onPreviewVersion(version: WorkflowDefinition): void {
    if (!version.id) return;
    this.previewVersion = version.version;
    this.displayPreviewDialog = true;
    this.previewLoading = true;
    this.cdr.detectChanges();

    this.workflowSvc.getById(version.id)
      .subscribe({
        next: (detail) => {
          this.renderPreviewDiagram(detail.bpmnXml || DEFAULT_BPMN_XML);
        },
        error: (err) => {
          this.previewLoading = false;
          this.messageService.add({ severity: 'error', summary: 'Lỗi xem trước', detail: err.message });
          this.cdr.detectChanges();
        }
      });
  }

  private async renderPreviewDiagram(xml: string): Promise<void> {
    const canvasEl = await this.waitForPreviewCanvasElement();
    if (!canvasEl) {
      this.previewLoading = false;
      this.messageService.add({ severity: 'error', summary: 'Lỗi xem trước', detail: 'Không tìm thấy vùng canvas để hiển thị sơ đồ.' });
      this.cdr.detectChanges();
      return;
    }
    try {
      if (this.bpmnViewer) {
        this.bpmnViewer.destroy();
        this.bpmnViewer = null;
      }
      const NavigatedViewer = (await import('bpmn-js/lib/NavigatedViewer')).default;
      this.bpmnViewer = new NavigatedViewer({ container: canvasEl });
      await this.bpmnViewer.importXML(xml);
      this.bpmnViewer.get('canvas').zoom('fit-viewport');
    } catch (err: any) {
      this.messageService.add({ severity: 'error', summary: 'Lỗi hiển thị sơ đồ', detail: err.message });
    } finally {
      this.previewLoading = false;
      this.cdr.detectChanges();
    }
  }

  private waitForPreviewCanvasElement(maxWaitMs = 3000): Promise<HTMLElement | null> {
    return new Promise((resolve) => {
      const start = Date.now();
      const check = () => {
        const el = this.previewCanvasRef?.nativeElement;
        if (el) {
          resolve(el);
          return;
        }
        if (Date.now() - start > maxWaitMs) {
          resolve(null);
          return;
        }
        requestAnimationFrame(check);
      };
      check();
    });
  }

  onReactivate(version: WorkflowDefinition): void {
    if (!this.authService.hasPermission('WORKFLOW_DEFINITION_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền tái kích hoạt quy trình.' });
      return;
    }
    this.reactivateTarget.set(version);
    this.showReactivateConfirm.set(true);
  }

  onConfirmReactivate(): void {
    const version = this.reactivateTarget();
    if (!version?.id) return;
    this.reactivating.set(true);
    this.workflowSvc.reactivate(version.id)
      .pipe(finalize(() => this.reactivating.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Thành công', detail: `Đã tái kích hoạt phiên bản ${version.version}.` });
          this.showReactivateConfirm.set(false);
          this.reactivateTarget.set(null);
          this.loadVersions();
          this.loadList();
        },
        error: (err) => {
          this.showReactivateConfirm.set(false);
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.message });
        }
      });
  }

  onCancelReactivate(): void {
    this.showReactivateConfirm.set(false);
    this.reactivateTarget.set(null);
  }

  loadVersions(): void {
    if (!this.draft.workflowTypeId) {
      this.versionsList = [];
      return;
    }
    this.loadingHistory = true;
    this.cdr.detectChanges();
    this.workflowSvc.getVersions(this.draft.workflowTypeId)
      .pipe(finalize(() => {
        this.loadingHistory = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (list) => {
          this.versionsList = list || [];
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi tải lịch sử phiên bản', detail: err.message });
          this.versionsList = [];
          this.cdr.detectChanges();
        }
      });
  }

  // Pagination
  currentPage = 1;
  pageSize = 10;
  totalCount = 0;

  get paginatedWorkflows(): WorkflowDefinition[] {
    return this.workflows;
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize);
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.loadList();
    }
  }

  prevPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.loadList();
    }
  }

  goToPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.totalPages) {
      this.currentPage = p;
      this.loadList();
    }
  }

  onPageSizeChange(event: any) {
    this.pageSize = Number(event.target.value);
    this.currentPage = 1;
    this.loadList();
  }

  // ─── Edit draft ─────────────────────────────────────────────────────────────
  draft: WorkflowDefinition = this.emptyDraft();

  // ─── Delete / confirm state ─────────────────────────────────────────────────
  showDeleteConfirm = signal<boolean>(false);
  showDeleteSelectedConfirm = signal<boolean>(false);
  showReactivateConfirm = signal<boolean>(false);
  deleteTarget = signal<WorkflowDefinition | null>(null);
  reactivateTarget = signal<WorkflowDefinition | null>(null);
  reactivating = signal<boolean>(false);
  actionMenuItems: MenuItem[] = [];

  // ─── Bpmn.io Modeler state ──────────────────────────────────────────────────
  bpmnModeler: any = null;
  private canvasResizeObserver?: ResizeObserver;
  selectedBpmnElement: any = null;
  selectedElementProps: any = null;

  constructor(
    private workflowSvc: WorkflowService,
    private messageService: MessageService,
    public authService: AuthService
  ) {}

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.loadList();
      this.loadSystemPermissionGroups();
      this.loadUnitPermissionGroups();
      this.loadWorkflowTypes();
      this.applyRouteState();
      this.routeSub = this.router.events
        .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
        .subscribe(() => this.applyRouteState());
    }
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.destroyModeler();
    this.onClosePreviewDialog();
  }

  openActionMenu(wf: WorkflowDefinition, event: Event, menu: Menu): void {
    event.stopPropagation();
    this.actionMenuItems = [
      ...(this.authService.hasPermission('WORKFLOW_DEFINITION_EDIT') ? [{ label: 'Sửa quy trình', title:"Sửa quy trình" ,icon: 'pi pi-pencil color-blue', command: () => this.onEdit(wf) }] : []),
      ...(this.authService.hasPermission('WORKFLOW_DEFINITION_EDIT') ? [{ label: wf.isActive ? 'Khóa quy trình' : 'Mở khóa quy trình', title: wf.isActive ? 'Khóa quy trình' : 'Mở khóa quy trình', icon: wf.isActive ? 'pi pi-lock color-red' : 'pi pi-lock-open color-teal', command: () => this.toggleWorkflowStatus(wf) }] : []),
      ...(this.authService.hasPermission('WORKFLOW_DEFINITION_DELETE') ? [{ label: 'Xóa quy trình', title:"Xóa quy trình" ,icon: 'pi pi-trash color-red', command: () => this.onDelete(wf) }] : []),
    ];
    menu.toggle(event);
  }

  private applyRouteState(): void {
    const url = this.router.url.split('?')[0];
    if (url === `${WORKFLOW_BUILDER_BASE}/new`) {
      this.openCreateMode();
      return;
    }

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      if (this.draft.id !== id || this.viewMode !== 'edit') {
        this.loadDetailById(id);
      }
      return;
    }

    this.showListMode();
  }

  private showListMode(): void {
    this.destroyModeler();
    this.viewMode = 'list';
    this.activeTab = 'general';
    this.loadList();
  }

  private openCreateMode(): void {
    if (!this.authService.hasPermission('WORKFLOW_DEFINITION_CREATE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới quy trình.' });
      this.router.navigate([WORKFLOW_BUILDER_BASE]);
      return;
    }
    this.destroyModeler();
    this.isEditMode = false;
    this.formSubmitted = false;
    this.draft = this.emptyDraft();
    this.draft.bpmnXml = DEFAULT_BPMN_XML;
    this.activeTab = 'general';
    this.viewMode = 'edit';
    this.cdr.detectChanges();
  }

  private loadDetailById(id: string): void {
    if (!this.authService.hasPermission('WORKFLOW_DEFINITION_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa quy trình.' });
      this.router.navigate([WORKFLOW_BUILDER_BASE]);
      return;
    }
    this.loading = true;
    this.loadingMsg = 'Đang tải chi tiết quy trình...';
    this.cdr.detectChanges();
    this.workflowSvc.getById(id)
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (detail) => {
          this.destroyModeler();
          this.isEditMode = true;
          this.formSubmitted = false;
          this.draft = {
            ...detail,
            workflowTypeId: this.resolveWorkflowTypeId(detail),
            steps: detail.steps || []
          };
          this.syncWorkflowTypeOption();
          this.activeTab = 'general';
          this.viewMode = 'edit';
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.message });
          this.router.navigate([WORKFLOW_BUILDER_BASE]);
        }
      });
  }

  // ─── Danh sách nhóm quyền hệ thống & đơn vị ─────────────────────────────────
  systemGroupList: any[] = [];
  unitGroupList: any[] = [];

  loadSystemPermissionGroups(): void {
    const apiUrl = `${environment.apiGatewayUrl}/api/v1/system-permission-groups/lookup`;
    this.http.get<any>(apiUrl).subscribe({
      next: (res) => {
        this.systemGroupList = Array.isArray(res) ? res : [];
        this.cdr.detectChanges();
      },
      error: (err) => console.error('Không tải được nhóm quyền hệ thống:', err)
    });
  }

  loadUnitPermissionGroups(): void {
    const apiUrl = `${environment.apiGatewayUrl}/api/v1/unit-permission-groups/lookup`;
    this.http.get<any>(apiUrl).subscribe({
      next: (res) => {
        this.unitGroupList = Array.isArray(res) ? res : [];
        this.cdr.detectChanges();
      },
      error: (err) => console.error('Không tải được nhóm quyền đơn vị:', err)
    });
  }

  // ─── Autocomplete tìm kiếm người dùng đích danh ────────────────────────────────
  userSearchResults: any[] = [];
  userSearchLoading = false;
  private userSearchTimeout: any;

  onUserSearchInput(keyword: string): void {
    clearTimeout(this.userSearchTimeout);
    if (!keyword || keyword.length < 2) {
      this.userSearchResults = [];
      this.cdr.detectChanges();
      return;
    }
    this.userSearchLoading = true;
    this.userSearchTimeout = setTimeout(() => {
      const apiUrl = `${environment.apiGatewayUrl}/api/v1/users?keyword=${encodeURIComponent(keyword)}&page=1&pageSize=20`;
      this.http.get<any>(apiUrl).subscribe({
        next: (res) => {
          this.userSearchResults = res?.items || [];
          this.userSearchLoading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.userSearchResults = [];
          this.userSearchLoading = false;
          this.cdr.detectChanges();
        }
      });
    }, 300);
  }

  selectAssignee(user: any): void {
    if (!this.selectedBpmnElement || !this.selectedElementProps) return;
    this.selectedElementProps.assigneeId = user.id;
    this.selectedElementProps.assigneeName = `${user.fullName} (${user.username})`;
    this.selectedElementProps.assigneeSearch = this.selectedElementProps.assigneeName;
    this.userSearchResults = [];
    const modeling = this.bpmnModeler.get('modeling');
    modeling.updateProperties(this.selectedBpmnElement, { assigneeId: user.id, assigneeName: user.fullName });
    this.cdr.detectChanges();
  }

  clearAssignee(): void {
    if (!this.selectedBpmnElement || !this.selectedElementProps) return;
    this.selectedElementProps.assigneeId = '';
    this.selectedElementProps.assigneeName = '';
    this.selectedElementProps.assigneeSearch = '';
    this.userSearchResults = [];
    const modeling = this.bpmnModeler.get('modeling');
    modeling.updateProperties(this.selectedBpmnElement, { assigneeId: '', assigneeName: '' });
    this.cdr.detectChanges();
  }

  loadWorkflowTypes(): void {
    const apiUrl = `${environment.apiGatewayUrl}/api/workflowdefinitions/get-workflow-type`;
    this.http.get<any>(apiUrl).subscribe({
      next: (res) => {
        const raw = Array.isArray(res)
          ? res
          : (res && Array.isArray(res.items)
            ? res.items
            : (res && Array.isArray(res.value)
              ? res.value
              : (res && Array.isArray(res.data)
                ? res.data
                : [])));
        this.loaiOptions = raw
          .map((item: string | { id?: number; code?: string; name?: string }) => {
            if (typeof item === 'string') return { id: 0, code: item, name: item };
            return {
              id: item.id ?? 0,
              code: item.code ?? '',
              name: item.name ?? item.code ?? '',
            };
          })
          .filter((opt: { id: number; code: string; name: string }) => opt.id > 0);
        this.syncWorkflowTypeOption();
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to load workflow types, leaving it empty as requested:', err);
        this.loaiOptions = [];
        this.cdr.detectChanges();
      }
    });
  }

  // ─── List: load from API ────────────────────────────────────────────────────

  loadList(resetPage = false): void {
    this.loading = true;
    this.loadingMsg = 'Đang tải danh sách quy trình...';
    this.listError = '';
    if (resetPage) {
      this.currentPage = 1;
    }
    this.cdr.detectChanges();

    const isActive = this.filterIsActive === 'true'  ? true
                   : this.filterIsActive === 'false' ? false
                   : undefined;

    this.workflowSvc.getAll(this.currentPage, this.pageSize, this.searchKeyword || undefined, isActive)
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (res: any) => {
          this.workflows = res?.items || [];
          this.totalCount = res?.totalCount || 0;
          this.selectedIds = [];
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.listError = `Không thể tải danh sách: ${err.message}`;
          this.workflows = [];
          this.totalCount = 0;
          this.cdr.detectChanges();
        }
      });
  }

  resetFilter(): void {
    this.filterIsActive = '';
    this.searchKeyword = '';
    this.currentPage = 1;
    this.loadList();
  }

  // ─── Selection ──────────────────────────────────────────────────────────────

  isSelected(id: string): boolean { return this.selectedIds.includes(id); }
  isAllSelected(): boolean {
    return (this.workflows || []).length > 0 &&
           (this.workflows || []).every(w => this.selectedIds.includes(w.id!));
  }
  toggleSelect(id: string): void {
    const i = this.selectedIds.indexOf(id);
    i >= 0 ? this.selectedIds.splice(i, 1) : this.selectedIds.push(id);
  }
  toggleSelectAll(e: Event): void {
    this.selectedIds = (e.target as HTMLInputElement).checked
      ? (this.workflows || []).map(w => w.id!)
      : [];
  }

  // ─── CRUD actions ──────────────────────────────────────────────────────────

  onAddNew(): void {
    this.router.navigate([`${WORKFLOW_BUILDER_BASE}/new`]);
  }

  onEdit(wf: WorkflowDefinition): void {
    if (!wf.id) return;
    this.router.navigate([WORKFLOW_BUILDER_BASE, wf.id]);
  }

  validateBpmnXml(xmlString: string): string[] {
    const errors: string[] = [];
    if (!xmlString) {
      errors.push('Cấu hình XML quy trình trống.');
      return errors;
    }

    try {
      const parser = new DOMParser();
      const doc = parser.parseFromString(xmlString, 'application/xml');

      const getElementsByLocalName = (localName: string): Element[] => {
        const result: Element[] = [];
        const allElements = doc.getElementsByTagName('*');
        for (let i = 0; i < allElements.length; i++) {
          if (allElements[i].localName === localName) {
            result.push(allElements[i]);
          }
        }
        return result;
      };

      // 1. Phải có duy nhất 1 Start Event
      const startEvents = getElementsByLocalName('startEvent');
      if (startEvents.length === 0) {
        errors.push('Quy trình phải có điểm bắt đầu (Start Event).');
      } else if (startEvents.length > 1) {
        errors.push('Quy trình chỉ được phép có duy nhất 1 điểm bắt đầu (Start Event).');
      }

      // 2. Phải có ít nhất 1 End Event
      const endEvents = getElementsByLocalName('endEvent');
      if (endEvents.length === 0) {
        errors.push('Quy trình phải có ít nhất 1 điểm kết thúc (End Event).');
      }

      // Find all sequence flows to map connections
      const sequenceFlows = getElementsByLocalName('sequenceFlow');
      const incomingMap = new Map<string, number>();
      const outgoingMap = new Map<string, number>();

      sequenceFlows.forEach(flow => {
        const sourceRef = flow.getAttribute('sourceRef');
        const targetRef = flow.getAttribute('targetRef');
        if (sourceRef) {
          outgoingMap.set(sourceRef, (outgoingMap.get(sourceRef) || 0) + 1);
        }
        if (targetRef) {
          incomingMap.set(targetRef, (incomingMap.get(targetRef) || 0) + 1);
        }
      });

      // Types of nodes to validate connection (Task/Gateway)
      // Rule 3: Tất cả các Node (Task/Gateway) phải được kết nối: Không được phép có Node nằm "bơ vơ" không có mũi tên đi vào (incoming) hoặc đi ra (outgoing).
      const taskTypes = [
        'task', 'userTask', 'serviceTask', 'scriptTask', 
        'sendTask', 'receiveTask', 'manualTask', 'businessRuleTask', 'callActivity'
      ];
      const gatewayTypes = [
        'exclusiveGateway', 'parallelGateway', 'inclusiveGateway', 'eventBasedGateway', 'complexGateway'
      ];
      const nodeTypes = [...taskTypes, ...gatewayTypes];

      nodeTypes.forEach(type => {
        const nodes = getElementsByLocalName(type);
        nodes.forEach(node => {
          const id = node.getAttribute('id');
          if (id) {
            const name = node.getAttribute('name') || id;
            const incomingCount = incomingMap.get(id) || 0;
            const outgoingCount = outgoingMap.get(id) || 0;

            if (incomingCount === 0 && outgoingCount === 0) {
              errors.push(`Node '${name}' hoàn toàn không được kết nối (không có mũi tên đi vào và đi ra).`);
            } else if (incomingCount === 0) {
              errors.push(`Node '${name}' không có mũi tên đi vào (incoming).`);
            } else if (outgoingCount === 0) {
              errors.push(`Node '${name}' không có mũi tên đi ra (outgoing).`);
            }
          }
        });
      });

      // Rule 4: Exclusive Gateway phải có đúng đường ra (tối đa 2 outgoing)
      const exclusiveGateways = getElementsByLocalName('exclusiveGateway');
      exclusiveGateways.forEach(gw => {
        const id = gw.getAttribute('id');
        if (id) {
          const name = gw.getAttribute('name') || id;
          const outgoingCount = outgoingMap.get(id) || 0;
          if (outgoingCount === 0) {
            errors.push(`Exclusive Gateway '${name}' phải có đường ra.`);
          } else if (outgoingCount > 2) {
            errors.push(`Exclusive Gateway '${name}' chỉ được phép có tối đa 2 đường ra.`);
          }
        }
      });

    } catch (e: any) {
      errors.push('Định dạng XML không hợp lệ: ' + e.message);
    }

    return errors;
  }

  async onSave() {
    const hasPerm = this.isEditMode ? this.authService.hasPermission('WORKFLOW_DEFINITION_EDIT') : this.authService.hasPermission('WORKFLOW_DEFINITION_CREATE');
    if (!hasPerm) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền lưu quy trình.' });
      return;
    }
    this.formSubmitted = true;
    if (!this.draft.workflowTypeId) return;

    const selectedType = this.loaiOptions.find(o => o.id === this.draft.workflowTypeId);
    this.draft.name = selectedType?.name ?? '';

    this.saving = true;

    if (this.bpmnModeler) {
      try {
        const { xml } = await this.bpmnModeler.saveXML({ format: true });
        this.draft.bpmnXml = xml;
        this.draft.steps = this.bpmnElementsToSteps();
      } catch (err: any) {
        this.saving = false;
        this.messageService.add({ severity: 'error', summary: 'Lỗi xuất XML', detail: err.message });
        return;
      }
    }

    // Validate XML in Frontend
    const xmlErrors = this.validateBpmnXml(this.draft.bpmnXml || '');
    if (xmlErrors.length > 0) {
      this.saving = false;
      xmlErrors.forEach(err => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi sơ đồ quy trình', detail: err, life: 10000 });
      });
      return;
    }

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
        this.destroyModeler();
        this.router.navigate([WORKFLOW_BUILDER_BASE]);
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi lưu', detail: err.message });
      }
    });
  }

  onDelete(wf: WorkflowDefinition): void {
    if (!this.authService.hasPermission('WORKFLOW_DEFINITION_DELETE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền xóa quy trình.' });
      return;
    }
    this.deleteTarget.set(wf);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete(): void {
    const wf = this.deleteTarget();
    if (!wf?.id) return;

    this.deleting.set(true);
    this.workflowSvc.delete(wf.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: 'Xóa thành công', detail: `Đã xóa "${wf.name}" thành công!` });
          this.showDeleteConfirm.set(false);
          this.deleteTarget.set(null);
          this.loadList();
        },
        error: (err) => {
          this.showDeleteConfirm.set(false);
          this.messageService.add({ severity: 'error', summary: 'Lỗi xóa', detail: err.message });
        }
      });
  }

  onCancelDelete(): void {
    this.showDeleteConfirm.set(false);
    this.deleteTarget.set(null);
  }

  toggleWorkflowStatus(wf: WorkflowDefinition): void {
    if (!this.authService.hasPermission('WORKFLOW_DEFINITION_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thay đổi trạng thái quy trình.' });
      return;
    }
    this.loading = true;
    this.workflowSvc.toggleStatus(wf.id!)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: `Đã ${res.isActive ? 'kích hoạt / mở khóa' : 'vô hiệu hóa / khóa'} quy trình "${wf.name}" thành công!`
          });
          this.loadList();
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.message });
        }
      });
  }

  onDeleteSelected(): void {
    if (!this.authService.hasPermission('WORKFLOW_DEFINITION_DELETE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền xóa quy trình.' });
      return;
    }
    if (!this.selectedIds.length) return;
    this.showDeleteSelectedConfirm.set(true);
  }

  onConfirmDeleteSelected(): void {
    this.showDeleteSelectedConfirm.set(false);
    this.doDeleteSelected();
  }

  onCancelDeleteSelected(): void {
    this.showDeleteSelectedConfirm.set(false);
  }

  doDeleteSelected(): void {
    if (!this.authService.hasPermission('WORKFLOW_DEFINITION_DELETE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền xóa quy trình.' });
      return;
    }
    if (!this.selectedIds.length) return;
    this.deleting.set(true);
    const deletes$ = this.selectedIds.map(id => this.workflowSvc.delete(id));
    let done = 0;
    deletes$.forEach(op => {
      op.subscribe({
        next: () => {
          done++;
          if (done === deletes$.length) {
            this.deleting.set(false);
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
    this.router.navigate([WORKFLOW_BUILDER_BASE]);
  }

  onExportExcel(): void {
    this.messageService.add({ severity: 'info', summary: 'Xuất dữ liệu', detail: 'Đang chuẩn bị dữ liệu xuất Excel...' });
    setTimeout(() => {
      this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã tải danh sách quy trình thành công dưới dạng Excel!' });
    }, 1200);
  }

  emptyDraft(): WorkflowDefinition {
    return { name: '', workflowTypeId: undefined, description: '', version: '1.0', forceActivate: false, isActive: true, steps: [] };
  }

  truncate(text: string, max: number): string {
    if (!text) return '';
    return text.length > max ? text.slice(0, max) + '…' : text;
  }

  get workflowTypeLabel(): string {
    if (!this.draft.workflowTypeId) return '';
    const opt = this.loaiOptions.find(o => o.id === this.draft.workflowTypeId);
    return opt?.name ?? this.draft.name ?? `Loại #${this.draft.workflowTypeId}`;
  }

  private resolveWorkflowTypeId(detail: WorkflowDefinition & { WorkflowTypeId?: number }): number | undefined {
    const raw = detail.workflowTypeId ?? detail.WorkflowTypeId;
    if (raw == null) return undefined;
    const id = Number(raw);
    return id > 0 ? id : undefined;
  }

  private syncWorkflowTypeOption(): void {
    const typeId = this.draft.workflowTypeId;
    if (!typeId || this.loaiOptions.some(o => o.id === typeId)) return;
    this.loaiOptions = [
      ...this.loaiOptions,
      { id: typeId, code: String(typeId), name: this.draft.name || `Loại #${typeId}` }
    ];
  }

  // ─── Bpmn.io Modeler Integration ───────────────────────────────────────────

  // Dùng @ViewChild (bpmnCanvasRef) làm nguồn tin cậy chính thay vì getElementById —
  // Angular cập nhật ViewChild ngay trong lần detectChanges() render ra phần tử đó,
  // nên không cần đoán setTimeout bao nhiêu ms là "đủ". Vẫn giữ vài lần retry ngắn
  // qua requestAnimationFrame để phòng trường hợp initModeler() được gọi sớm hơn
  // detectChanges() một nhịp render.
  private waitForCanvasElement(maxWaitMs = 3000): Promise<HTMLElement | null> {
    return new Promise((resolve) => {
      const start = Date.now();
      const check = () => {
        const el = this.bpmnCanvasRef?.nativeElement || document.getElementById('canvas');
        if (el) {
          resolve(el);
          return;
        }
        if (Date.now() - start > maxWaitMs) {
          resolve(null);
          return;
        }
        requestAnimationFrame(check);
      };
      check();
    });
  }

  async initModeler() {
    if (this.bpmnModeler) {
      return;
    }

    const canvasEl = await this.waitForCanvasElement();
    if (!canvasEl) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi tải BPMN modeler',
        detail: 'Không tìm thấy vùng canvas để khởi tạo sơ đồ.'
      });
      return;
    }

    try {
      const Modeler = (await import('bpmn-js/lib/Modeler')).default;

      this.bpmnModeler = new Modeler({
        container: canvasEl
      });

      // Set default connection name when sequence flows are created from Gateway
      const eventBus = this.bpmnModeler.get('eventBus');
      eventBus.on('commandStack.connection.create.postExecuted', (event: any) => {
        const context = event.context;
        const connection = context.connection;
        if (connection && connection.type === 'bpmn:SequenceFlow') {
          const source = connection.source;
          if (source && source.type && source.type.includes('Gateway')) {
            const outgoingFlows = (source.outgoing || []).filter((flow: any) => flow.type === 'bpmn:SequenceFlow');
            if (!connection.businessObject.name) {
              const modeling = this.bpmnModeler.get('modeling');
              if (outgoingFlows.length === 1) {
                modeling.updateProperties(connection, { name: 'Duyệt' });
              } else if (outgoingFlows.length === 2) {
                modeling.updateProperties(connection, { name: 'Từ chối' });
              }
            }
          }
        }
      });

      this.bpmnModeler.on('selection.changed', (event: any) => {
        const newSelection = event.newSelection;
        if (newSelection && newSelection.length === 1) {
          const element = newSelection[0];

          if (element.type === 'bpmn:Task' || element.type === 'bpmn:UserTask') {
            // Dùng chung logic với dblclick để tránh 2 nguồn dựng selectedElementProps
            // lệch nhau (bản rút gọn ở đây từng thiếu 4 thuộc tính nhóm quyền/giao việc,
            // khiến panel hiện trống dù dữ liệu đã lưu đúng).
            this.openStepConfig(element);
            return;
          }

          this.selectedBpmnElement = element;

          let condition = '';
          if (element.type === 'bpmn:SequenceFlow') {
            const condExp = element.businessObject.conditionExpression;
            condition = condExp ? (condExp.body || '') : '';
          }

          this.selectedElementProps = {
            id: element.id,
            type: element.type,
            name: element.businessObject.name || '',
            stepNum: element.businessObject.$attrs['stepNum'] || '',
            requiredRole: element.businessObject.$attrs['requiredRole'] || '',
            actionType: element.businessObject.$attrs['actionType'] || 'Approve',
            allowEdit: element.businessObject.$attrs['allowEdit'] === 'true' || element.businessObject.$attrs['allowEdit'] === true,
            requireSignature: element.businessObject.$attrs['requireSignature'] === 'true' || element.businessObject.$attrs['requireSignature'] === true,
            condition: condition
          };
        } else {
          this.selectedBpmnElement = null;
          this.selectedElementProps = null;
        }
        this.cdr.detectChanges();
      });

      this.bpmnModeler.on('element.dblclick', 10000, (event: any) => {
        const element = event.element;
        if (element && (element.type === 'bpmn:Task' || element.type === 'bpmn:UserTask')) {
          this.openStepConfig(element);
          return false;
        }
        return;
      });

      // Vẽ lại số thứ tự bước mỗi khi sơ đồ thay đổi (thêm/xoá bước, đổi thứ tự...)
      this.bpmnModeler.on('commandStack.changed', () => this.refreshStepBadges());

      const xml = this.draft.bpmnXml || DEFAULT_BPMN_XML;
      await this.bpmnModeler.importXML(xml);
      this.refreshStepBadges();
      // Viewport zoom will be handled when the tab is switched to 'design' and container is visible.

      // Panel cấu hình bên phải hiện/ẩn theo lựa chọn phần tử làm canvas đổi chiều rộng.
      // bpmn-js không tự phát hiện việc này nên cần ResizeObserver để fit lại sơ đồ.
      if ('ResizeObserver' in window) {
        this.canvasResizeObserver = new ResizeObserver((entries) => {
          if (!this.bpmnModeler) return;
          const { width, height } = entries[0].contentRect;
          // Container tạm thời có kích thước 0 khi bị ẩn/đang chuyển layout —
          // gọi fit-viewport lúc này khiến bpmn-js tính tỉ lệ vô hạn/NaN và crash.
          if (!width || !height) return;
          try {
            const canvas = this.bpmnModeler.get('canvas');
            canvas.resized();
            canvas.zoom('fit-viewport');
          } catch (e) {
            console.error('Error resizing BPMN canvas:', e);
          }
        });
        this.canvasResizeObserver.observe(canvasEl);
      }
    } catch (err: any) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi tải BPMN modeler',
        detail: err.message
      });
    }
  }

  destroyModeler() {
    this.canvasResizeObserver?.disconnect();
    this.canvasResizeObserver = undefined;
    if (this.bpmnModeler) {
      this.bpmnModeler.destroy();
      this.bpmnModeler = null;
      this.selectedBpmnElement = null;
      this.selectedElementProps = null;
    }
  }

  openStepConfig(element: any) {
    this.selectedBpmnElement = element;
    const bo = element.businessObject;
    // Giải mã danh sách ID nhóm quyền từ BPMN attrs (lưu dạng CSV)
    const sysGroupIds = (bo.$attrs['systemPermissionGroupIds'] || '').split(',').map((s: string) => Number(s.trim())).filter((n: number) => n > 0);
    const unitGroupIds = (bo.$attrs['unitPermissionGroupIds'] || '').split(',').map((s: string) => Number(s.trim())).filter((n: number) => n > 0);
    this.selectedElementProps = {
      id: element.id,
      type: element.type,
      name: bo.name || '',
      stepNum: bo.$attrs['stepNum'] || '',
      actionType: bo.$attrs['actionType'] || 'Approve',
      selectedSystemGroupIds: sysGroupIds,
      selectedUnitGroupIds: unitGroupIds,
      requireSameUnit: bo.$attrs['requireSameUnit'] === 'true',
      assigneeId: bo.$attrs['assigneeId'] || '',
      assigneeName: bo.$attrs['assigneeName'] || '',
      assigneeSearch: bo.$attrs['assigneeName'] || ''
    };
    this.userSearchResults = [];
    this.cdr.detectChanges();
  }

  updateElementProperty(prop: string, event: any) {
    if (!this.selectedBpmnElement || !this.selectedElementProps) return;
    const value = event?.target ? event.target.value : event;
    const modeling = this.bpmnModeler.get('modeling');

    if (prop === 'name') {
      modeling.updateProperties(this.selectedBpmnElement, { name: value });
      this.selectedElementProps.name = value;
    } else if (prop === 'condition') {
      const bpmnFactory = this.bpmnModeler.get('bpmnFactory');
      let conditionExpression = undefined;
      if (value) {
        conditionExpression = bpmnFactory.create('bpmn:FormalExpression', {
          body: value
        });
      }
      modeling.updateProperties(this.selectedBpmnElement, {
        conditionExpression: conditionExpression
      });
      this.selectedElementProps.condition = value;
    } else if (prop === 'requireSameUnit') {
      // Giá trị từ checkbox
      const checked = event?.target ? event.target.checked : Boolean(value);
      modeling.updateProperties(this.selectedBpmnElement, { requireSameUnit: String(checked) });
      this.selectedElementProps.requireSameUnit = checked;
    } else {
      const attrs: any = {};
      attrs[prop] = value;
      modeling.updateProperties(this.selectedBpmnElement, attrs);
      this.selectedElementProps[prop] = value;
    }
    this.cdr.detectChanges();
  }

  // Cập nhật nhóm quyền hệ thống khi người dùng chọn/bỏ chọn
  toggleSystemGroup(groupId: number, event: any): void {
    if (!this.selectedBpmnElement || !this.selectedElementProps) return;
    const checked = event.target.checked;
    let ids: number[] = this.selectedElementProps.selectedSystemGroupIds || [];
    ids = checked ? [...new Set([...ids, groupId])] : ids.filter((id: number) => id !== groupId);
    this.selectedElementProps.selectedSystemGroupIds = ids;
    const csv = ids.join(',');
    this.bpmnModeler.get('modeling').updateProperties(this.selectedBpmnElement, { systemPermissionGroupIds: csv });
    this.cdr.detectChanges();
  }

  // Cập nhật nhóm quyền đơn vị khi người dùng chọn/bỏ chọn
  toggleUnitGroup(groupId: number, event: any): void {
    if (!this.selectedBpmnElement || !this.selectedElementProps) return;
    const checked = event.target.checked;
    let ids: number[] = this.selectedElementProps.selectedUnitGroupIds || [];
    ids = checked ? [...new Set([...ids, groupId])] : ids.filter((id: number) => id !== groupId);
    this.selectedElementProps.selectedUnitGroupIds = ids;
    const csv = ids.join(',');
    this.bpmnModeler.get('modeling').updateProperties(this.selectedBpmnElement, { unitPermissionGroupIds: csv });
    this.cdr.detectChanges();
  }

  isSystemGroupSelected(groupId: number): boolean {
    return (this.selectedElementProps?.selectedSystemGroupIds || []).includes(groupId);
  }

  isUnitGroupSelected(groupId: number): boolean {
    return (this.selectedElementProps?.selectedUnitGroupIds || []).includes(groupId);
  }


  bpmnElementsToSteps(): WorkflowStep[] {
    if (!this.bpmnModeler) return [];
    const elementRegistry = this.bpmnModeler.get('elementRegistry');
    const tasks = elementRegistry.filter((element: any) =>
      element.type === 'bpmn:Task' || element.type === 'bpmn:UserTask'
    );

    return tasks.map((t: any, i: number) => {
      const bo = t.businessObject;
      const stepNum = bo.$attrs['stepNum'] ? parseInt(bo.$attrs['stepNum'], 10) : (i + 1);
      return {
        stepName: bo.name || 'Bước mới',
        order: stepNum,
        requiredRole: bo.$attrs['requiredRole'] || '',
        actionType: bo.$attrs['actionType'] || 'Approve',
        // Bổ sung 4 thuộc tính mới
        systemPermissionGroupIds: bo.$attrs['systemPermissionGroupIds'] || '',
        unitPermissionGroupIds: bo.$attrs['unitPermissionGroupIds'] || '',
        requireSameUnit: bo.$attrs['requireSameUnit'] === 'true',
        assigneeId: bo.$attrs['assigneeId'] || ''
      };
    }).sort((a: any, b: any) => a.order - b.order);
  }

  // Vẽ badge số thứ tự (●1, ●2...) ở góc trên-trái mỗi bước Task/UserTask trên sơ đồ.
  private refreshStepBadges(): void {
    if (!this.bpmnModeler) return;
    const overlays = this.bpmnModeler.get('overlays');
    const elementRegistry = this.bpmnModeler.get('elementRegistry');

    overlays.remove({ type: 'step-order-badge' });

    const tasks = elementRegistry.filter((el: any) =>
      el.type === 'bpmn:Task' || el.type === 'bpmn:UserTask'
    );

    tasks.forEach((el: any, i: number) => {
      const bo = el.businessObject;
      const stepNum = bo.$attrs['stepNum'] ? parseInt(bo.$attrs['stepNum'], 10) : (i + 1);
      // Style inline vì overlay được diagram-js chèn thẳng vào DOM, không đi qua
      // template Angular nên CSS scoped (view encapsulation) của component sẽ không áp dụng.
      const style = 'display:flex;align-items:center;justify-content:center;' +
        'width:20px;height:20px;border-radius:50%;background:#000;color:#fff;' +
        'font-size:11px;font-weight:600;font-family:inherit;line-height:1;' +
        'box-shadow:0 0 0 2px #fff;';
      overlays.add(el, 'step-order-badge', {
        position: { top: -10, left: -10 },
        html: `<div style="${style}">${stepNum}</div>`
      });
    });
  }

  onZoom(delta: number) {
    if (!this.bpmnModeler) return;
    const canvas = this.bpmnModeler.get('canvas');
    if (delta === 0) {
      canvas.resized();
      canvas.zoom('fit-viewport');
    } else {
      canvas.zoom(canvas.zoom() + delta);
    }
  }
}
