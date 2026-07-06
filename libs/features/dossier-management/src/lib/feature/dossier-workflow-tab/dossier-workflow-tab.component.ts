import {
  Component,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  SimpleChanges,
  PLATFORM_ID,
  inject,
  signal,
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { finalize } from 'rxjs';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import { isTechnicalWorkflowLabel } from '../../utils/dossier-status.util';

function pickFirst<T>(...values: T[]): T | undefined {
  for (const v of values) {
    if (v !== undefined && v !== null && v !== '') return v;
  }
  return undefined;
}

@Component({
  selector: 'app-dossier-workflow-tab',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div style="display: flex; flex-direction: column; gap: 20px;">
      <div *ngIf="loading()" style="text-align: center; padding: 40px 0; color: #6b7280;">
        <i class="pi pi-spin pi-spinner" style="font-size: 2rem; display: block; margin-bottom: 12px; color: #002D72;"></i>
        Đang tải thông tin quy trình...
      </div>

      <ng-container *ngIf="!loading()">
        <div *ngIf="!workflowXml()" style="text-align: center; padding: 36px; background: #fffbeb; border: 1px solid #fef3c7; border-radius: 8px; color: #b45309;">
          <i class="pi pi-exclamation-triangle" style="font-size: 2rem; display: block; margin-bottom: 10px;"></i>
          <p style="margin: 0; font-weight: 600;">Hồ sơ chưa được đưa vào quy trình phê duyệt</p>
        </div>

        <div *ngIf="workflowXml()" style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
          <div style="background: #f8fafc; padding: 10px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #374151; display: flex; justify-content: space-between; align-items: center; font-size: 0.9rem;">
            <span><i class="pi pi-sitemap" style="margin-right: 6px; color: #002D72;"></i>Sơ đồ quy trình</span>
            <span *ngIf="headerStepLabel()" class="status-pill"
                  [ngStyle]="{ background: '#eff6ff', color: '#1d4ed8', border: '1px solid #bfdbfe', fontSize: '0.78rem' }">
              {{ headerStepLabel() }}
            </span>
          </div>
          <div style="padding: 12px 16px 4px;">
            <div [id]="canvasId" style="height: 360px; border: 1px solid #cbd5e1; border-radius: 8px; background: #fafafa; position: relative;"></div>
            <p style="font-size: 0.75rem; color: #94a3b8; margin: 6px 0 8px 2px;">
              <i class="pi pi-info-circle" style="margin-right: 4px;"></i>Nút viền xanh là bước phê duyệt hiện tại.
            </p>
          </div>
        </div>

        <div *ngIf="workflowXml() && pendingTask()" style="background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 8px; padding: 10px 16px; display: flex; justify-content: space-between; align-items: center; font-size: 0.85rem;">
          <span style="color: #1e40af;">
            <i class="pi pi-spin pi-spinner" style="font-size: 0.75rem; margin-right: 6px;"></i>
            Đang ở bước: <b>{{ pendingTask()?.stepName || pendingTask()?.StepName }}</b>
          </span>
          <code *ngIf="pendingTask()?.assignedRole || pendingTask()?.AssignedRole"
                style="background: #dbeafe; color: #1e40af; padding: 2px 8px; border-radius: 4px; font-size: 0.75rem;">
            {{ pendingTask()?.assignedRole || pendingTask()?.AssignedRole }}
          </code>
        </div>

        <div style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;" *ngIf="workflowXml()">
          <div style="background: #f8fafc; padding: 10px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #374151; font-size: 0.9rem;">
            <i class="pi pi-history" style="margin-right: 6px; color: #002D72;"></i>Lịch sử xử lý luồng duyệt
          </div>
          <div style="padding: 16px;">
            <div class="wf-timeline-wrapper" *ngIf="historyLogs().length > 0">
              <div class="wf-timeline">
                <div class="timeline-item" *ngFor="let h of historyLogs()">
                  <div class="timeline-badge"
                       [class.badge-submit]="h.action === 'Submit'"
                       [class.badge-approve]="h.action === 'Approve'"
                       [class.badge-reject]="h.action === 'Reject'">
                  </div>
                  <div class="timeline-content">
                    <div class="timeline-header">
                      <span class="timeline-title">
                        {{ h.stepName || h.actionLabel || 'Xử lý' }}
                        <span style="font-weight: 500; font-size: 11px; margin-left: 6px; color: #94a3b8;">
                          ({{ h.action === 'Submit' ? 'Khởi tạo' : (h.action === 'Approve' ? 'Duyệt qua' : (h.action === 'Reject' ? 'Trả về' : h.action)) }})
                        </span>
                      </span>
                      <span class="timeline-time">{{ (h.actionDate || h.createdDate) | date:'dd/MM/yyyy HH:mm:ss' }}</span>
                    </div>
                    <div class="timeline-user">
                      Thực hiện bởi:
                      <b>{{ h.actionByUsername && h.actionByFullName ? h.actionByUsername + ' - ' + h.actionByFullName : (h.actionByUsername || h.actorName || h.actionByUserId || h.actorId || 'Hệ thống') }}</b>
                    </div>
                    <div class="timeline-comment" *ngIf="h.comment"><b>Ý kiến:</b> {{ h.comment }}</div>
                  </div>
                </div>
              </div>
            </div>

            <div *ngIf="!historyLogs().length" style="text-align: center; padding: 32px; background: #f8fafc; border-radius: 8px; color: #64748b;">
              <i class="pi pi-history" style="font-size: 28px; display: block; margin-bottom: 10px; color: #94a3b8;"></i>
              <p style="margin: 0; font-weight: 500;">Chưa có lịch sử xử lý quy trình nào.</p>
            </div>
          </div>
        </div>
      </ng-container>
    </div>
  `,
  styles: [`
    ::ng-deep .highlight-active-node:not(.djs-connection) .djs-visual > :first-child {
      fill: #dbeafe !important;
      stroke: #002D72 !important;
      stroke-width: 3px !important;
    }
    .wf-timeline-wrapper {
      max-height: 400px; overflow-y: auto; padding: 8px 16px 8px 8px; margin-top: 4px; border-radius: 8px;
    }
    .wf-timeline-wrapper::-webkit-scrollbar { width: 6px; }
    .wf-timeline-wrapper::-webkit-scrollbar-track { background: transparent; }
    .wf-timeline-wrapper::-webkit-scrollbar-thumb { background: #cbd5e1; border-radius: 3px; }
    .wf-timeline { display: flex; flex-direction: column; position: relative; padding-left: 24px; }
    .wf-timeline::before { content: ''; position: absolute; top: 0; bottom: 0; left: 7px; width: 2px; background-color: #e2e8f0; }
    .timeline-item { position: relative; padding-bottom: 20px; }
    .timeline-item:last-child { padding-bottom: 0; }
    .timeline-badge {
      position: absolute; left: -24px; top: 4px; width: 16px; height: 16px;
      border-radius: 50%; background-color: #cbd5e1; border: 3px solid white;
    }
    .timeline-badge.badge-submit  { background-color: #3b82f6; }
    .timeline-badge.badge-approve { background-color: #10b981; }
    .timeline-badge.badge-reject  { background-color: #ef4444; }
    .timeline-content {
      background-color: #f8fafc; padding: 10px 14px; border-radius: 8px; border: 1px solid #f1f5f9;
    }
    .timeline-header { display: flex; justify-content: space-between; margin-bottom: 4px; font-size: 12px; }
    .timeline-title  { font-weight: 700; color: #1e293b; }
    .timeline-time   { color: #94a3b8; }
    .timeline-user   { font-size: 11px; color: #64748b; font-weight: 600; margin-bottom: 6px; }
    .timeline-comment {
      font-size: 12px; font-style: italic; color: #64748b;
      background-color: #e2e8f0; padding: 6px 10px; border-radius: 4px;
      margin-top: 6px; border-left: 3px solid #cbd5e1;
    }
  `],
})
export class DossierWorkflowTabComponent implements OnInit, OnChanges, OnDestroy {
  @Input({ required: true }) dossierId!: string;
  @Input() kindId?: number;
  @Input() canvasId = 'bpmn-canvas-dossier-form';
  @Input() refreshToken = 0;

  private platformId = inject(PLATFORM_ID);
  private service = inject(DossierManagementService);

  loading = signal(true);
  workflowXml = signal('');
  currentNodeId = signal('');
  pendingTask = signal<any>(null);
  historyLogs = signal<any[]>([]);
  instanceStatus = signal('');
  headerStepLabel = signal('');

  private bpmnViewer: any = null;

  ngOnInit(): void {
    this.loadWorkflow();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['refreshToken'] && !changes['refreshToken'].firstChange) {
      this.loadWorkflow();
    }
  }

  ngOnDestroy(): void {
    if (this.bpmnViewer) {
      this.bpmnViewer.destroy();
      this.bpmnViewer = null;
    }
  }

  loadWorkflow(): void {
    this.loading.set(true);
    this.service.getWorkflowDetail(this.dossierId, this.kindId).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: (res) => {
        if (!res?.instance) {
          this.workflowXml.set('');
          this.historyLogs.set(Array.isArray(res?.history) ? res.history : []);
          this.pendingTask.set(null);
          this.currentNodeId.set('');
          this.instanceStatus.set('');
          this.headerStepLabel.set('');
          return;
        }

        const instance = res.instance;
        const pendingList = Array.isArray(instance.pendingTasks)
          ? instance.pendingTasks
          : Array.isArray(instance.PendingTasks)
            ? instance.PendingTasks
            : [];
        this.pendingTask.set(pendingList.length > 0 ? pendingList[0] : null);
        this.currentNodeId.set(pickFirst(instance.currentNodeId, instance.CurrentNodeId) || '');
        this.historyLogs.set(Array.isArray(res.history) ? res.history : []);
        const rawStatus = String(pickFirst(instance.status, instance.Status) ?? '');
        this.instanceStatus.set(rawStatus);
        const pending = pendingList.length > 0 ? pendingList[0] : null;
        const stepName = String(
          pickFirst(
            pending?.stepName,
            pending?.StepName,
            instance.currentNodeName,
            instance.CurrentNodeName
          ) ?? ''
        ).trim();
        this.headerStepLabel.set(
          isTechnicalWorkflowLabel(stepName)
            ? ''
            : stepName || (isTechnicalWorkflowLabel(rawStatus) ? '' : rawStatus)
        );

        const bpmnXml = res.definition?.bpmnXml ?? res.definition?.BpmnXml;
        if (bpmnXml) {
          this.workflowXml.set(bpmnXml);
          setTimeout(() => this.initBpmnViewer(bpmnXml, this.currentNodeId()), 150);
        } else {
          this.workflowXml.set('');
        }
      },
      error: () => {
        this.workflowXml.set('');
        this.historyLogs.set([]);
      },
    });
  }

  private async initBpmnViewer(xml: string, currentNodeId: string): Promise<void> {
    if (!xml || !isPlatformBrowser(this.platformId)) return;

    if (this.bpmnViewer) {
      this.bpmnViewer.destroy();
      this.bpmnViewer = null;
    }

    try {
      const Viewer = (await import('bpmn-js/lib/Viewer')).default;
      this.bpmnViewer = new Viewer({ container: `#${this.canvasId}` });
      await this.bpmnViewer.importXML(xml);
      const canvas = this.bpmnViewer.get('canvas');
      canvas.zoom('fit-viewport');
      if (currentNodeId) {
        canvas.addMarker(currentNodeId, 'highlight-active-node');
      }
    } catch (err) {
      console.error('BPMN render error', err);
    }
  }
}
