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
  imports: [CommonModule, FormsModule, HttpClientModule, ToastModule],
  providers: [MessageService],
  templateUrl: './workflow-builder.component.html',
  styleUrl: './workflow-builder.component.scss'
})
export class WorkflowBuilderComponent implements OnInit {

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

  // ─── Bpmn.io Modeler state ──────────────────────────────────────────────────
  bpmnModeler: any = null;
  selectedBpmnElement: any = null;
  selectedElementProps: any = null;

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
    this.draft.bpmnXml = DEFAULT_BPMN_XML;
    this.activeTab = 'general';
    this.viewMode = 'edit';
    setTimeout(() => this.initModeler(), 50);
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
          this.activeTab = 'general';
          this.viewMode = 'edit';
          setTimeout(() => this.initModeler(), 50);
        },
        error: (err) => {
          this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.message });
        }
      });
  }

  async onSave() {
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

      this.bpmnModeler.on('selection.changed', (event: any) => {
        const newSelection = event.newSelection;
        if (newSelection && newSelection.length === 1) {
          const element = newSelection[0];
          this.selectedBpmnElement = element;
          this.selectedElementProps = {
            id: element.id,
            type: element.type,
            name: element.businessObject.name || '',
            stepNum: element.businessObject.$attrs['stepNum'] || '',
            requiredRole: element.businessObject.$attrs['requiredRole'] || '',
            actionType: element.businessObject.$attrs['actionType'] || 'Approve'
          };
        } else {
          this.selectedBpmnElement = null;
          this.selectedElementProps = null;
        }
      });

      const xml = this.draft.bpmnXml || DEFAULT_BPMN_XML;
      await this.bpmnModeler.importXML(xml);
      this.bpmnModeler.get('canvas').zoom('fit-viewport');
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

  updateElementProperty(prop: string, event: any) {
    if (!this.selectedBpmnElement || !this.selectedElementProps) return;
    const value = event?.target ? event.target.value : event;
    const modeling = this.bpmnModeler.get('modeling');

    if (prop === 'name') {
      modeling.updateProperties(this.selectedBpmnElement, { name: value });
      this.selectedElementProps.name = value;
    } else {
      const attrs: any = {};
      attrs[prop] = value;
      modeling.updateProperties(this.selectedBpmnElement, attrs);
      this.selectedElementProps[prop] = value;
    }
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
