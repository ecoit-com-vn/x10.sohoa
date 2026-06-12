import { Component, OnInit, ViewChild, ElementRef, inject, PLATFORM_ID, ChangeDetectorRef } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ToastModule } from 'primeng/toast';
import { MessageService, ConfirmationService } from 'primeng/api';
import {
  WorkflowService,
  WorkflowDefinition,
  WorkflowStep,
  AuthService
} from '@sohoa.frontend/shared/core';
import { finalize } from 'rxjs/operators';
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

// ─── Component ────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-workflow-builder',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule],
  providers: [MessageService],
  templateUrl: './workflow-builder.component.html',
  styleUrl: './workflow-builder.component.scss'
})
export class WorkflowBuilderComponent implements OnInit {

  // ─── Platform check ────────────────────────────────────────────────────────
  private platformId = inject(PLATFORM_ID);
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);

  // ─── View state ─────────────────────────────────────────────────────────────
  viewMode: 'list' | 'edit' = 'list';
  activeTab: 'general' | 'design' = 'general';
  isEditMode = false;
  formSubmitted = false;

  selectTab(tab: 'general' | 'design'): void {
    this.activeTab = tab;
    if (tab === 'design') {
      setTimeout(() => {
        if (this.bpmnModeler) {
          try {
            const canvas = this.bpmnModeler.get('canvas');
            canvas.resized();
            canvas.zoom('fit-viewport');
          } catch (e) {
            console.error('Error resizing BPMN canvas:', e);
          }
        }
      }, 50);
    }
  }

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
  loaiOptions: string[] = [];
  workflows: WorkflowDefinition[] = [];
  selectedIds: string[] = [];

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

  // ─── Delete state ───────────────────────────────────────────────────────────
  showDeleteOneConfirm = false;
  showDeleteSelectedConfirm = false;
  deleteTarget: WorkflowDefinition | null = null;

  // ─── Bpmn.io Modeler state ──────────────────────────────────────────────────
  bpmnModeler: any = null;
  selectedBpmnElement: any = null;
  selectedElementProps: any = null;

  constructor(
    private workflowSvc: WorkflowService,
    private messageService: MessageService,
    private confirmationService: ConfirmationService,
    public authService: AuthService
  ) {}

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.loadList();
      this.loadRoles();
      this.loadWorkflowTypes();
    }
  }

  rolesList: any[] = [];

  loadRoles(): void {
    const apiUrl = `${environment.apiGatewayUrl}/api/v1/roles/lookup`;
    this.http.get<any>(apiUrl).subscribe({
      next: (res) => {
        this.rolesList = Array.isArray(res) ? res : (res && Array.isArray(res.items) ? res.items : (res && Array.isArray(res.value) ? res.value : []));
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to load roles list:', err);
        this.cdr.detectChanges();
      }
    });
  }

  loadWorkflowTypes(): void {
    const apiUrl = `${environment.apiGatewayUrl}/api/workflowdefinitions/get-workflow-type`;
    this.http.get<any>(apiUrl).subscribe({
      next: (res) => {
        this.loaiOptions = Array.isArray(res)
          ? res
          : (res && Array.isArray(res.items)
            ? res.items
            : (res && Array.isArray(res.value)
              ? res.value
              : (res && Array.isArray(res.data)
                ? res.data
                : [])));
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
    if (!this.authService.hasPermission('WORKFLOW_CREATE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền thêm mới quy trình.' });
      return;
    }
    this.isEditMode = false;
    this.formSubmitted = false;
    this.draft = this.emptyDraft();
    this.draft.bpmnXml = DEFAULT_BPMN_XML;
    this.activeTab = 'general';
    this.viewMode = 'edit';
    setTimeout(() => this.initModeler(), 50);
  }

  onEdit(wf: WorkflowDefinition): void {
    if (!this.authService.hasPermission('WORKFLOW_EDIT')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền chỉnh sửa quy trình.' });
      return;
    }
    this.loading = true;
    this.loadingMsg = 'Đang tải chi tiết quy trình...';
    this.cdr.detectChanges();
    this.workflowSvc.getById(wf.id!)
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (detail) => {
          this.isEditMode = true;
          this.formSubmitted = false;
          this.draft = { ...detail, steps: detail.steps || [] };
          this.activeTab = 'general';
          this.viewMode = 'edit';
          setTimeout(() => this.initModeler(), 50);
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.message });
          this.cdr.detectChanges();
        }
      });
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
    const hasPerm = this.isEditMode ? this.authService.hasPermission('WORKFLOW_EDIT') : this.authService.hasPermission('WORKFLOW_CREATE');
    if (!hasPerm) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền lưu quy trình.' });
      return;
    }
    this.formSubmitted = true;
    if (!this.draft.name) return;

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
        this.viewMode = 'list';
        this.loadList();
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi lưu', detail: err.message });
      }
    });
  }

  promptDeleteOne(wf: WorkflowDefinition): void {
    if (!this.authService.hasPermission('WORKFLOW_DELETE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền xóa quy trình.' });
      return;
    }
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
    if (!this.authService.hasPermission('WORKFLOW_DELETE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền xóa quy trình.' });
      return;
    }
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
    if (!this.authService.hasPermission('WORKFLOW_DELETE')) {
      this.messageService.add({ severity: 'error', summary: 'Không có quyền', detail: 'Bạn không có quyền xóa quy trình.' });
      return;
    }
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
    this.destroyModeler();
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

  // ─── Bpmn.io Modeler Integration ───────────────────────────────────────────

  async initModeler() {
    if (this.bpmnModeler) return;

    try {
      const Modeler = (await import('bpmn-js/lib/Modeler')).default;

      this.bpmnModeler = new Modeler({
        container: '#canvas',
        keyboard: {
          bindTo: window
        }
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

      const xml = this.draft.bpmnXml || DEFAULT_BPMN_XML;
      await this.bpmnModeler.importXML(xml);
      // Viewport zoom will be handled when the tab is switched to 'design' and container is visible.
    } catch (err: any) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi tải BPMN modeler',
        detail: err.message
      });
    }
  }

  destroyModeler() {
    if (this.bpmnModeler) {
      this.bpmnModeler.destroy();
      this.bpmnModeler = null;
      this.selectedBpmnElement = null;
      this.selectedElementProps = null;
    }
  }

  openStepConfig(element: any) {
    this.selectedBpmnElement = element;
    this.selectedElementProps = {
      id: element.id,
      type: element.type,
      name: element.businessObject.name || '',
      stepNum: element.businessObject.$attrs['stepNum'] || '',
      requiredRole: element.businessObject.$attrs['requiredRole'] || '',
      actionType: element.businessObject.$attrs['actionType'] || 'Approve'
    };
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
    } else {
      const attrs: any = {};
      attrs[prop] = value;
      modeling.updateProperties(this.selectedBpmnElement, attrs);
      this.selectedElementProps[prop] = value;
    }
    this.cdr.detectChanges();
  }

  isRoleSelected(roleCode: string): boolean {
    if (!this.selectedElementProps?.requiredRole) return false;
    const roles = this.selectedElementProps.requiredRole.split(',').map((r: string) => r.trim());
    return roles.includes(roleCode);
  }

  toggleRoleSelection(roleCode: string, event: any): void {
    if (!this.selectedBpmnElement || !this.selectedElementProps) return;
    const checked = event.target.checked;
    let roles = this.selectedElementProps.requiredRole
      ? this.selectedElementProps.requiredRole.split(',').map((r: string) => r.trim()).filter((r: string) => r)
      : [];

    if (checked) {
      if (!roles.includes(roleCode)) {
        roles.push(roleCode);
      }
    } else {
      roles = roles.filter((r: string) => r !== roleCode);
    }

    const newValue = roles.join(',');
    this.selectedElementProps.requiredRole = newValue;

    const modeling = this.bpmnModeler.get('modeling');
    modeling.updateProperties(this.selectedBpmnElement, {
      requiredRole: newValue
    });
    this.cdr.detectChanges();
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
        actionType: bo.$attrs['actionType'] || 'Approve'
      };
    }).sort((a: any, b: any) => a.order - b.order);
  }

  onZoom(delta: number) {
    if (!this.bpmnModeler) return;
    const canvas = this.bpmnModeler.get('canvas');
    if (delta === 0) {
      canvas.zoom('fit-viewport');
    } else {
      canvas.zoom(canvas.zoom() + delta);
    }
  }
}
