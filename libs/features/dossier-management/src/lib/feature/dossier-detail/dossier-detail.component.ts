import { Component, OnInit, signal, computed, inject, Output, EventEmitter, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DossierManagementService } from '../../data-access/dossier-management.service';
import { AuthService } from '@sohoa.frontend/shared/core';

interface EavField {
  key: string;
  label: string;
  type: 'text' | 'number' | 'date' | 'textarea' | 'select' | 'checkbox';
  required?: boolean;
  placeholder?: string;
  options?: { label: string; value: string }[];
  unit?: string;
}

@Component({
  selector: 'app-dossier-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule],
  template: `
    <div class="wf-card" style="position: relative;">
      <!-- Header -->
      <div class="edit-header">
        <div style="display: flex; align-items: flex-start; gap: 10px;">
          <button (click)="onCancel()" class="btn-back btn-small" title="Quay lại" style="margin-top: 2px;">
            <i class="pi pi-arrow-left"></i>
          </button>
          <div>
            <h2 class="edit-title">Chi tiết Hồ sơ</h2>
            <div style="display: flex; flex-wrap: wrap; gap: 16px; margin-top: 8px; font-size: 0.83rem;">
              <span class="text-muted"><i class="pi pi-tag" style="margin-right: 4px;"></i> Loại hồ sơ: <b style="color: #374151;">{{ dossier()?.dossierTypeName }}</b></span>
              <span class="text-muted"><i class="pi pi-map-marker" style="margin-right: 4px;"></i> Trạm/ĐZ: <b style="color: #374151;">{{ dossier()?.infrastructureName || '-' }}</b></span>
              <span class="text-muted" style="display: inline-flex; align-items: center; gap: 6px;">
                Trạng thái:
                <span class="status-pill" [ngStyle]="getStatusStyle(dossier()?.status)">
                  {{ getStatusText(dossier()?.status) }}
                </span>
              </span>
            </div>
          </div>
        </div>
        <div class="edit-actions">
          <!-- Lưu dữ liệu: chỉ hiện khi tab info + Draft/Returned + có fields -->
          <button *ngIf="activeTab() === 'info' && isDraftOrReturned && dynamicFields().length > 0"
                  (click)="saveFormData()" class="btn-save" [disabled]="savingForm()">
            <i class="pi pi-save" *ngIf="!savingForm()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="savingForm()"></i>
            Lưu dữ liệu
          </button>
          <button *ngIf="dossier()?.status === 'Draft' || dossier()?.status === 'Returned'"
                  (click)="submitForApproval()" class="btn-green" [disabled]="submitting()">
            <i class="pi pi-send" *ngIf="!submitting()"></i>
            <i class="pi pi-spin pi-spinner" *ngIf="submitting()"></i>
            Gửi duyệt
          </button>
        </div>
      </div>

      <!-- TABS -->
      <div class="tab-bar">
        <button class="tab-item" [class.tab-active]="activeTab() === 'info'" (click)="activeTab.set('info')">
          <i class="pi pi-info-circle" style="margin-right: 6px;"></i> Dữ liệu Hồ sơ
        </button>
        <button class="tab-item" [class.tab-active]="activeTab() === 'documents'" (click)="activeTab.set('documents')">
          <i class="pi pi-file" style="margin-right: 6px;"></i> Tài liệu đính kèm
        </button>
        <button class="tab-item" [class.tab-active]="activeTab() === 'versions'" (click)="onOpenVersionsTab()">
          <i class="pi pi-history" style="margin-right: 6px;"></i> Lịch sử phiên bản
        </button>
        <button class="tab-item" [class.tab-active]="activeTab() === 'workflow'" (click)="activeTab.set('workflow')">
          <i class="pi pi-sitemap" style="margin-right: 6px;"></i> Quy trình & Lịch sử
        </button>
      </div>

      <!-- TAB CONTENT -->
      <div class="tab-content">

        <!-- ═══ Tab: Dữ liệu Hồ sơ ═══ -->
        <div *ngIf="activeTab() === 'info'" style="display: flex; flex-direction: column; gap: 20px;">
          <!-- Loading form -->
          <div *ngIf="loadingType()" style="display: flex; align-items: center; gap: 8px; color: #6b7280; padding: 12px 0;">
            <i class="pi pi-spin pi-spinner"></i> Đang tải biểu mẫu...
          </div>

          <!-- Dynamic form fields — event-based, không dùng ngModel để tránh bleeding -->
          <div *ngIf="!loadingType() && dynamicFields().length > 0"
               style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px;">
            <ng-container *ngFor="let field of dynamicFields(); trackBy: trackByFieldKey">
              <div class="form-group" [style.grid-column]="field.type === 'textarea' ? '1 / -1' : 'auto'">
                <label class="form-label">
                  {{ field.label }}
                  <span class="required" *ngIf="field.required">*</span>
                </label>
                <ng-container [ngSwitch]="field.type">
                  <input *ngSwitchCase="'text'" type="text" class="wf-input w-full"
                         [placeholder]="field.placeholder || ''"
                         [value]="detailFormData[field.key] ?? ''"
                         [disabled]="!isDraftOrReturned"
                         (input)="setDetailField(field.key, $event)">

                  <div *ngSwitchCase="'number'" style="display: flex; gap: 6px; align-items: center;">
                    <input type="number" class="wf-input" style="flex: 1;"
                           [placeholder]="field.placeholder || ''"
                           [value]="detailFormData[field.key] ?? ''"
                           [disabled]="!isDraftOrReturned"
                           (input)="setDetailFieldNumber(field.key, $event)">
                    <span *ngIf="field.unit" style="font-size: 0.85rem; color: #6b7280; white-space: nowrap;">{{ field.unit }}</span>
                  </div>

                  <input *ngSwitchCase="'date'" type="date" class="wf-input w-full"
                         [value]="detailFormData[field.key] ?? ''"
                         [disabled]="!isDraftOrReturned"
                         (input)="setDetailField(field.key, $event)">

                  <textarea *ngSwitchCase="'textarea'" class="wf-textarea w-full" rows="3"
                            [placeholder]="field.placeholder || ''"
                            [value]="detailFormData[field.key] ?? ''"
                            [disabled]="!isDraftOrReturned"
                            (input)="setDetailField(field.key, $event)"></textarea>

                  <select *ngSwitchCase="'select'" class="wf-select w-full"
                          [disabled]="!isDraftOrReturned"
                          (change)="setDetailField(field.key, $event)">
                    <option value="">-- Chọn --</option>
                    <option *ngFor="let opt of field.options" [value]="opt.value"
                            [selected]="detailFormData[field.key] === opt.value">{{ opt.label }}</option>
                  </select>

                  <label *ngSwitchCase="'checkbox'" style="display: flex; align-items: center; gap: 8px; cursor: pointer; margin-top: 4px;">
                    <input type="checkbox"
                           [checked]="detailFormData[field.key]"
                           [disabled]="!isDraftOrReturned"
                           (change)="setDetailCheckbox(field.key, $event)"
                           style="width: 16px; height: 16px; accent-color: #002D72; cursor: pointer;">
                    <span style="font-size: 0.9rem;">{{ field.placeholder || field.label }}</span>
                  </label>

                  <input *ngSwitchDefault type="text" class="wf-input w-full"
                         [value]="detailFormData[field.key] ?? ''"
                         [disabled]="!isDraftOrReturned"
                         (input)="setDetailField(field.key, $event)">
                </ng-container>
              </div>
            </ng-container>
          </div>

          <!-- Chưa có biểu mẫu -->
          <div *ngIf="!loadingType() && dynamicFields().length === 0"
               style="padding: 32px; text-align: center; color: #9ca3af; background: #f8fafc; border-radius: 8px; border: 1px dashed #e2e8f0; font-size: 0.85rem;">
            <i class="pi pi-file-edit" style="font-size: 2rem; display: block; margin-bottom: 8px;"></i>
            Loại hồ sơ này chưa được cấu hình mẫu dữ liệu động.
          </div>
        </div>

        <!-- ═══ Tab: Tài liệu ═══ -->
        <div *ngIf="activeTab() === 'documents'" style="padding: 40px; text-align: center; color: #9ca3af;">
          <i class="pi pi-cloud-upload" style="font-size: 2.5rem; display: block; margin-bottom: 12px; color: #cbd5e1;"></i>
          <p style="margin: 0;">Tính năng đính kèm file số hóa (pdf, docx) đang được cập nhật.</p>
        </div>

        <!-- ═══ Tab: Lịch sử phiên bản ═══ -->
        <div *ngIf="activeTab() === 'versions'" style="display: flex; flex-direction: column; gap: 12px;">
          <div *ngIf="loadingVersions()" style="display: flex; align-items: center; gap: 8px; color: #6b7280; padding: 12px 0;">
            <i class="pi pi-spin pi-spinner"></i> Đang tải lịch sử...
          </div>

          <div *ngIf="!loadingVersions() && versions().length === 0"
               style="padding: 32px; text-align: center; color: #9ca3af; background: #f8fafc; border-radius: 8px; border: 1px dashed #e2e8f0; font-size: 0.85rem;">
            Chưa có phiên bản nào được lưu.
          </div>

          <div *ngFor="let v of versions()" style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
            <div style="background: #f8fafc; padding: 10px 16px; border-bottom: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center;">
              <span style="font-weight: 600; color: #374151;">Phiên bản #{{ v.versionNumber }}</span>
              <span style="font-size: 0.78rem; color: #6b7280;">{{ v.createdDate | date:'dd/MM/yyyy HH:mm' }} — {{ v.createdBy }}</span>
            </div>
            <div style="padding: 12px 16px;">
              <div *ngIf="v.changeNote" style="font-size: 0.85rem; color: #374151; margin-bottom: 8px;">
                <i class="pi pi-comment" style="margin-right: 4px; color: #6b7280;"></i>{{ v.changeNote }}
              </div>
              <details style="cursor: pointer;">
                <summary style="font-size: 0.8rem; color: #6b7280;">Xem dữ liệu JSON</summary>
                <pre style="font-size: 0.76rem; background: #f1f5f9; padding: 10px; border-radius: 6px; margin-top: 8px; overflow-x: auto; white-space: pre-wrap; word-break: break-all;">{{ v.formDataJson | json }}</pre>
              </details>
            </div>
          </div>
        </div>

        <!-- ═══ Tab: Quy trình ═══ -->
        <div *ngIf="activeTab() === 'workflow'" style="display: flex; flex-direction: column; gap: 20px;">
          <!-- Lịch sử luân chuyển -->
          <div style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;">
            <div style="background: #f8fafc; padding: 12px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #374151; display: flex; justify-content: space-between; align-items: center;">
              <span>Lịch sử luân chuyển</span>
              <span *ngIf="workflowDetail()?.instance" class="status-pill"
                    [ngStyle]="{ background: '#eff6ff', color: '#1d4ed8', border: '1px solid #bfdbfe' }">
                Trạng thái: {{ workflowDetail()?.instance?.status }}
              </span>
            </div>
            <div style="padding: 16px;" *ngIf="workflowDetail()?.history?.length > 0">
              <div style="position: relative; border-left: 2px solid #bfdbfe; margin-left: 12px; display: flex; flex-direction: column; gap: 24px; padding-bottom: 16px;">
                <div *ngFor="let h of workflowDetail().history" style="position: relative; padding-left: 24px;">
                  <div style="position: absolute; width: 16px; height: 16px; background: #3b82f6; border-radius: 50%; left: -9px; top: 4px; border: 4px solid #fff; box-shadow: 0 1px 3px rgba(0,0,0,0.15);"></div>
                  <div style="font-weight: 600; color: #1e293b;">{{ h.actionLabel || 'Xử lý' }}</div>
                  <div style="font-size: 0.83rem; color: #6b7280; margin-top: 4px;">
                    <b style="color: #374151;">{{ h.actorName || h.actorId }}</b>
                    <span *ngIf="h.nextNodeName"> &#8594; {{ h.nextNodeName }}</span>
                  </div>
                  <div style="font-size: 0.83rem; color: #9ca3af; margin-top: 4px;" *ngIf="h.comment">
                    <i class="pi pi-comment" style="font-size: 0.7rem; margin-right: 4px;"></i>"{{ h.comment }}"
                  </div>
                  <div class="text-muted" style="font-size: 0.72rem; margin-top: 4px;">{{ h.createdDate | date:'dd/MM/yyyy HH:mm' }}</div>
                </div>
              </div>
            </div>
            <div *ngIf="!workflowDetail()?.history?.length" style="padding: 24px; text-align: center; color: #9ca3af; font-size: 0.85rem;">
              Chưa có lịch sử quy trình.
            </div>
          </div>

          <!-- Hành động phê duyệt -->
          <div style="border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;" *ngIf="myTask()">
            <div style="background: #eff6ff; padding: 12px 16px; border-bottom: 1px solid #bfdbfe; font-weight: 600; color: #1e40af;">
              <i class="pi pi-check-square" style="margin-right: 6px;"></i> Nhiệm vụ của bạn: {{ myTask().name }}
            </div>
            <div style="padding: 16px; background: #fff; display: flex; flex-direction: column; gap: 16px;">
              <div class="form-group">
                <label class="form-label">Ý kiến xử lý</label>
                <textarea class="wf-textarea" rows="3" [(ngModel)]="actionComment"
                          placeholder="Nhập ý kiến (nếu có)..."></textarea>
              </div>
              <div style="display: flex; gap: 8px; flex-wrap: wrap;">
                <button *ngFor="let btn of getDynamicButtons()"
                        (click)="executeAction(btn)"
                        [class]="btn.actionType === 'Reject' || btn.actionType === 'Return' ? 'btn-cancel' : 'btn-green'"
                        [disabled]="actionSubmitting()">
                  <i class="pi pi-spin pi-spinner" *ngIf="actionSubmitting() && currentAction() === btn.label"></i>
                  {{ btn.label }}
                </button>
              </div>
            </div>
          </div>
        </div>

      </div>

      <!-- Loading Overlay -->
      <div *ngIf="loading()" style="position: absolute; inset: 0; background: rgba(255,255,255,0.6); display: flex; align-items: center; justify-content: center; z-index: 50; border-radius: 12px;">
        <i class="pi pi-spin pi-spinner" style="font-size: 2rem; color: #002D72;"></i>
      </div>
    </div>
  `,
  styles: []
})
export class DossierDetailComponent implements OnInit {
  @Input() dossierId!: string;
  @Output() cancel = new EventEmitter<void>();

  private service = inject(DossierManagementService);
  private authService = inject(AuthService);
  private messageService = inject(MessageService);

  loading = signal<boolean>(true);
  submitting = signal<boolean>(false);
  activeTab = signal<'info' | 'documents' | 'versions' | 'workflow'>('info');

  dossier = signal<any>(null);

  // EAV Form
  loadingType = signal<boolean>(false);
  formTemplate = signal<any>(null);
  dynamicFields = signal<EavField[]>([]);
  detailFormData: Record<string, any> = {};
  savingForm = signal<boolean>(false);

  // Phiên bản
  versions = signal<any[]>([]);
  loadingVersions = signal<boolean>(false);

  // Workflow
  workflowDetail = signal<any>(null);
  myTask = signal<any>(null);
  actionComment = '';
  actionSubmitting = signal<boolean>(false);
  currentAction = signal<string>('');

  get isDraftOrReturned(): boolean {
    const status = this.dossier()?.status;
    return status === 'Draft' || status === 'Returned';
  }

  ngOnInit() {
    this.loadDetail();
  }

  loadDetail() {
    this.loading.set(true);
    this.service.getDossierById(this.dossierId).subscribe({
      next: (res) => {
        this.dossier.set(res);

        // Parse dữ liệu form đã lưu vào detailFormData
        try {
          this.detailFormData = res.formDataJson ? JSON.parse(res.formDataJson) : {};
        } catch {
          this.detailFormData = {};
        }

        // Load Form Template based on DossierTypeId
        if (res.dossierTypeId) {
          this.loadFormTemplate(res.dossierTypeId);
        } else {
          this.loading.set(false);
        }

        // Load Workflow data
        this.loadWorkflow();
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: 'Không thể tải chi tiết hồ sơ' });
        this.loading.set(false);
      }
    });
  }

  loadFormTemplate(dossierTypeId: string) {
    this.loadingType.set(true);
    this.service.getDossierTypeLookup().subscribe({
      next: (types) => {
        const found = types.find((t: any) => t.id === dossierTypeId);
        const formId: string | null = found?.formId ?? null;

        if (!formId) {
          this.formTemplate.set(null);
          this.dynamicFields.set([]);
          this.loadingType.set(false);
          return;
        }

        this.service.getFormTemplate(formId).subscribe({
          next: (template) => {
            this.formTemplate.set(template);
            if (template?.formSchema) {
              try {
                const fields: EavField[] = JSON.parse(template.formSchema);
                this.dynamicFields.set(Array.isArray(fields) ? fields : []);
              } catch {
                this.dynamicFields.set([]);
              }
            } else {
              this.dynamicFields.set([]);
            }
            this.loadingType.set(false);
          },
          error: () => {
            this.formTemplate.set(null);
            this.dynamicFields.set([]);
            this.loadingType.set(false);
          }
        });
      },
      error: () => {
        this.loadingType.set(false);
      }
    });
  }

  loadWorkflow() {
    this.service.getWorkflowDetail(this.dossierId).subscribe({
      next: (res) => {
        this.workflowDetail.set(res);
        
        // Find if user has pending task
        this.service.getMyTasks().subscribe(tasks => {
          const task = tasks.find(t => t.targetEntityId === this.dossierId);
          this.myTask.set(task || null);
          this.loading.set(false);
        });
      },
      error: () => this.loading.set(false)
    });
  }

  saveFormData() {
    this.savingForm.set(true);
    this.service.saveFormData(this.dossierId, {
      formDataJson: JSON.stringify(this.detailFormData),
      rowVersion: this.dossier()?.rowVersion,
      changeNote: 'Cập nhật dữ liệu từ giao diện chi tiết'
    }).subscribe({
      next: (res) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã lưu dữ liệu' });
        if (res?.data) this.dossier.set(res.data);
        this.savingForm.set(false);
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể lưu dữ liệu' });
        this.savingForm.set(false);
      }
    });
  }

  submitForApproval() {
    if (!confirm('Bạn có chắc chắn muốn gửi duyệt hồ sơ này?')) return;
    
    this.submitting.set(true);
    this.service.submitForApproval(this.dossierId).subscribe({
      next: (res) => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: 'Đã gửi duyệt' });
        this.dossier.set(res.data);
        this.submitting.set(false);
        this.loadWorkflow(); // reload workflow tab
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể gửi duyệt' });
        this.submitting.set(false);
      }
    });
  }

  getDynamicButtons() {
    const task = this.myTask();
    if (!task) return [];

    // Parse từ bước hiện tại của workflow template
    const def = this.workflowDetail()?.definition;
    if (!def || !def.nodes) return [];

    const currentNode = def.nodes.find((n: any) => n.id === task.nodeId);
    if (!currentNode || !currentNode.routes) return [];

    return currentNode.routes.map((r: any) => ({
      label: r.label || 'Chuyển tiếp',
      targetNodeId: r.targetId,
      actionType: r.label?.includes('Từ chối') ? 'Reject' : (r.label?.includes('Trả lại') ? 'Return' : 'Approve')
    }));
  }

  executeAction(btn: any) {
    this.actionSubmitting.set(true);
    this.currentAction.set(btn.label);
    
    this.service.moveWorkflow(this.dossierId, {
      nextNodeId: btn.targetNodeId,
      actionLabel: btn.label,
      comment: this.actionComment
    }).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Thành công', detail: `Đã thực hiện: ${btn.label}` });
        this.actionSubmitting.set(false);
        this.actionComment = '';
        this.loadDetail(); // reload all
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Lỗi', detail: err.error?.message || 'Không thể thực hiện' });
        this.actionSubmitting.set(false);
      }
    });
  }

  onCancel() {
    this.cancel.emit();
  }

  /** Event-based setters — tránh ngModel bleeding trong *ngFor+*ngSwitch */
  setDetailField(key: string, event: Event) {
    this.detailFormData[key] = (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value;
  }

  setDetailFieldNumber(key: string, event: Event) {
    const raw = (event.target as HTMLInputElement).value;
    this.detailFormData[key] = raw === '' ? null : Number(raw);
  }

  setDetailCheckbox(key: string, event: Event) {
    this.detailFormData[key] = (event.target as HTMLInputElement).checked;
  }

  /** Mở tab phiên bản và load dữ liệu nếu chưa có */
  onOpenVersionsTab() {
    this.activeTab.set('versions');
    if (this.versions().length === 0 && !this.loadingVersions()) {
      this.loadVersions();
    }
  }

  loadVersions() {
    this.loadingVersions.set(true);
    this.service.getVersions(this.dossierId).subscribe({
      next: (res) => {
        this.versions.set(res || []);
        this.loadingVersions.set(false);
      },
      error: () => this.loadingVersions.set(false)
    });
  }

  trackByFieldKey(_index: number, field: EavField): string {
    return field.key;
  }

  getStatusText(status?: string): string {
    switch (status) {
      case 'Draft': return 'Nháp';
      case 'PendingApproval': return 'Đang chờ duyệt';
      case 'InProgress': return 'Đang xử lý';
      case 'Returned': return 'Bị trả lại';
      case 'Approved': return 'Đã phê duyệt';
      default: return status || '';
    }
  }

  getStatusStyle(status?: string): { [key: string]: string } {
    switch (status) {
      case 'Draft': return { background: '#f1f5f9', color: '#475569', border: '1px solid #e2e8f0' };
      case 'PendingApproval': return { background: '#eff6ff', color: '#1d4ed8', border: '1px solid #bfdbfe' };
      case 'InProgress': return { background: '#f5f3ff', color: '#6d28d9', border: '1px solid #ddd6fe' };
      case 'Returned': return { background: '#fef2f2', color: '#dc2626', border: '1px solid #fecaca' };
      case 'Approved': return { background: '#dcfce7', color: '#15803d', border: '1px solid #bbf7d0' };
      default: return { background: '#f1f5f9', color: '#475569', border: '1px solid #e2e8f0' };
    }
  }
}
