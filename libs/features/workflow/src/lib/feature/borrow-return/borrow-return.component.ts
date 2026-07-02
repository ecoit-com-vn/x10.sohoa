import { Component, OnInit, inject, signal, computed, PLATFORM_ID, effect } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FormsModule } from '@angular/forms';
import { WorkflowDefinition, AuthService } from '@sohoa.frontend/shared/core';
import { BorrowRecordService } from '../../data-access/borrow-record.service';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-borrow-return',
  standalone: true,
  imports: [CommonModule, ToastModule, FormsModule],
  providers: [MessageService],
  templateUrl: './borrow-return.component.html',
  styleUrl: './borrow-return.component.scss'
})
export class BorrowReturnComponent implements OnInit {
  private platformId = inject(PLATFORM_ID);

  // Tabs navigation
  activeTab = signal<'my-tasks' | 'records'>('my-tasks');

  // Yêu cầu mượn trả (Borrow/Return records list)
  requests = signal<any[]>([]);
  loading = signal<boolean>(false);
  currentPage = signal<number>(1);
  pageSize = signal<number>(10);
  totalCount = signal<number>(0);
  searchKeyword = signal<string>('');
  filterState = signal<string>('');

  // Nhiệm vụ chờ xử lý (User Pending Tasks)
  myTasks = signal<any[]>([]);
  loadingTasks = signal<boolean>(false);

  // Cache trạng thái workflow hiện tại của các hồ sơ: { [id]: { instanceId, status, stepName, assignedRole } }
  entityWorkflowStates = signal<{ [key: string]: any }>({});

  // View navigation: list, create or detail
  currentSubView = signal<'list' | 'create' | 'detail'>('list');
  newRequest = signal<{ dossierId: string; requesterId: string; reason: string }>({
    dossierId: '',
    requesterId: '',
    reason: ''
  });
  creatingRequest = signal<boolean>(false);
 
  // Dropdowns lookups
  users = signal<any[]>([]);
  dossiers = signal<any[]>([]);
  loadingLookups = signal<boolean>(false);
 
  // 2. Detail view: Chi tiết yêu cầu mượn trả & Quy trình
  activeDetailRecord = signal<any>(null);

  detailActiveTab = signal<'info' | 'workflow' | 'history'>('info');
  loadingBpmn = signal<boolean>(false);
  detailWorkflowXml = signal<string>('');
  detailCurrentNodeId = signal<string>('');
  detailHistoryLogs = signal<any[]>([]);
  detailPendingTask = signal<any>(null);
  detailActionComment = signal<string>('');
  detailActionSubmitting = signal<boolean>(false);
  detailDynamicButtons = signal<any[]>([]);
  selectedNextUserId = signal<string>('');

  hasForwardActionWithUserRequirement = computed(() => {
    return this.detailDynamicButtons().some(btn => btn.requiresUser && btn.label !== 'Từ chối' && !btn.label.includes('Từ chối') && btn.label !== 'Hủy' && !btn.label.includes('Hủy'));
  });

  filteredNextUsers = computed(() => {
    const forwardBtn = this.detailDynamicButtons().find(btn => btn.requiresUser && btn.label !== 'Từ chối' && !btn.label.includes('Từ chối') && btn.label !== 'Hủy' && !btn.label.includes('Hủy'));
    if (!forwardBtn || !forwardBtn.requiredRole) {
      return this.users();
    }
    const roles = forwardBtn.requiredRole.split(',').map((r: string) => r.trim().toUpperCase());
    return this.users().filter(u => {
      const userRoles = (u.roles || u.Roles || []).map((r: string) => r.toUpperCase());
      return userRoles.some((r: string) => roles.includes(r));
    });
  });

  // bpmn-js Viewer instance
  private bpmnViewer: any = null;

  public authService = inject(AuthService);
  private borrowRecordService = inject(BorrowRecordService);
  private messageService = inject(MessageService);

  // Computed signals for pagination
  paginatedRequests = computed(() => {
    return this.requests();
  });

  totalPages = computed(() => {
    return Math.ceil(this.totalCount() / this.pageSize());
  });

  ngOnInit() {
    this.loadMyTasks();
    this.loadRequests();
    this.loadLookups();
  }

  // Chuyển tab chính
  switchTab(tab: 'my-tasks' | 'records') {
    this.activeTab.set(tab);
    if (tab === 'my-tasks') {
      this.loadMyTasks();
    } else {
      this.loadRequests();
    }
  }

  // Tải các nhiệm vụ cần xử lý của tôi
  loadMyTasks() {
    this.loadingTasks.set(true);
    this.borrowRecordService.getMyTasks()
      .pipe(
        finalize(() => {
          this.loadingTasks.set(false);
        })
      )
      .subscribe({
        next: (tasks) => {
          this.myTasks.set(Array.isArray(tasks) ? tasks : (tasks && Array.isArray((tasks as any).items) ? (tasks as any).items : (tasks && Array.isArray((tasks as any).value) ? (tasks as any).value : [])));
        },
        error: (err) => {
          console.error('Error loading tasks', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể kết nối tới dịch vụ để lấy danh sách nhiệm vụ cần làm.'
          });
          this.myTasks.set([]);
        }
      });
  }

  constructor() {
    effect(() => {
      const kw = this.searchKeyword();
      const state = this.filterState();
      this.currentPage.set(1);
    }, { allowSignalWrites: true });

    effect(() => {
      const page = this.currentPage();
      const size = this.pageSize();
      const kw = this.searchKeyword();
      const state = this.filterState();
      this.loadRequests();
    }, { allowSignalWrites: true });
  }

  // Tải toàn bộ yêu cầu mượn trả
  loadRequests() {
    this.loading.set(true);
    this.borrowRecordService.getBorrowRecords(this.currentPage(), this.pageSize(), this.searchKeyword(), this.filterState())
      .pipe(
        finalize(() => {
          this.loading.set(false);
        })
      )
      .subscribe({
        next: (res) => {
          const items = res?.items || [];
          const list = items.map((item: any) => ({
            id: item.id,
            requester: item.requesterId || 'Chuyên viên kỹ thuật',
            recordName: item.dossierId,
            reason: item.reason,
            createdAt: new Date(item.requestDate),
            approvedDate: item.approvedDate ? new Date(item.approvedDate) : null,
            borrowedDate: item.borrowedDate ? new Date(item.borrowedDate) : null,
            returnedDate: item.returnedDate ? new Date(item.returnedDate) : null,
            status: this.mapBackendState(item.state)
          }));
          this.requests.set(list);
          this.totalCount.set(res?.totalCount || 0);
        },
        error: (err) => {
          console.error('Error loading requests via API', err);
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi tải dữ liệu',
            detail: 'Không thể kết nối tới máy chủ để tải yêu cầu mượn/trả.'
          });
          this.requests.set([]);
          this.totalCount.set(0);
        }
      });
  }

  // Lấy trạng thái workflow instance của hồ sơ từ backend
  loadEntityWorkflowState(recordId: string) {
    this.borrowRecordService.getWorkflowByEntity(recordId, 2).subscribe({
      next: (instance) => {
        if (instance) {
          const pendingTask = instance.pendingTasks && instance.pendingTasks.length > 0 ? instance.pendingTasks[0] : null;
          this.entityWorkflowStates.update(states => ({
            ...states,
            [recordId]: {
              instanceId: instance.instanceId,
              status: instance.status,
              stepName: pendingTask ? pendingTask.stepName : 'Hoàn thành',
              assignedRole: pendingTask ? pendingTask.assignedRole : ''
            }
          }));
        }
      },
      error: () => {
        this.entityWorkflowStates.update(states => ({
          ...states,
          [recordId]: null
        }));
      }
    });
  }

  // 1. Tạo mới yêu cầu mượn trả
  openCreateView() {
    this.newRequest.set({ dossierId: '', requesterId: '', reason: '' });
    this.currentSubView.set('create');
    this.loadLookups();
  }

  closeCreateView() {
    this.currentSubView.set('list');
  }

  loadLookups() {
    this.loadingLookups.set(true);
    // Tải danh sách người dùng
    this.borrowRecordService.getUsersLookup().subscribe({
      next: (res) => {
        this.users.set(Array.isArray(res) ? res : (res && Array.isArray((res as any).items) ? (res as any).items : (res && Array.isArray((res as any).value) ? (res as any).value : [])));
      },
      error: (err) => {
        console.error('Lỗi khi tải danh sách người dùng', err);
      }
    });

    // Tải danh sách hồ sơ kỹ thuật
    this.borrowRecordService.getDossiers().subscribe({
      next: (res) => {
        this.dossiers.set(Array.isArray(res) ? res : (res && Array.isArray((res as any).items) ? (res as any).items : (res && Array.isArray((res as any).value) ? (res as any).value : [])));
      },
      error: (err) => {
        console.error('Lỗi khi tải danh sách hồ sơ kỹ thuật', err);
      }
    });
  }

  createRequest() {
    const draft = this.newRequest();
    if (!draft.dossierId.trim() || !draft.requesterId.trim() || !draft.reason.trim()) {
      this.messageService.add({
        severity: 'error',
        summary: 'Nhập dữ liệu',
        detail: 'Vui lòng nhập đầy đủ thông tin: Hồ sơ, Người mượn và Lý do.'
      });
      return;
    }

    this.creatingRequest.set(true);
    this.borrowRecordService.createBorrowRecord(draft)
      .pipe(finalize(() => this.creatingRequest.set(false)))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: res.state === 1 || res.state === 'Approved'
              ? 'Tạo mới đơn mượn hồ sơ thành công! Hệ thống tự động phê duyệt trực tiếp (chưa cấu hình quy trình).'
              : 'Tạo đơn và khởi chạy quy trình phê duyệt luân chuyển thành công!'
          });
          this.closeCreateView();
          this.loadRequests();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err.message || 'Không thể tạo đơn mượn trả.'
          });
        }
      });
  }

  getRequesterFullName(username: string): string {
    const user = this.users().find(u => u.username === username);
    return user ? user.fullName : username;
  }

  getDossierTitle(dossierId: string): string {
    const dossier = this.dossiers().find(d => d.id === dossierId);
    return dossier ? dossier.title : dossierId;
  }

  // 2. Chi tiết đơn & Quy trình (View con)
  openDetailDialog(record: any) {
    this.detailActiveTab.set('info');
    this.detailHistoryLogs.set([]);
    this.detailPendingTask.set(null);
    this.detailWorkflowXml.set('');
    this.detailCurrentNodeId.set('');
    this.detailDynamicButtons.set([]);
    this.detailActionComment.set('Đồng ý phê duyệt.');
    this.selectedNextUserId.set('');
    this.currentSubView.set('detail');

    this.loadingBpmn.set(true);

    // Fetch the full borrow record details to ensure all fields are populated
    this.borrowRecordService.getBorrowRecordById(record.id).subscribe({
      next: (item) => {
        const mappedRecord = {
          id: item.id,
          requesterId: item.requesterId,
          dossierId: item.dossierId,
          requesterName: this.getRequesterFullName(item.requesterId),
          dossierTitle: this.getDossierTitle(item.dossierId),
          reason: item.reason,
          createdAt: new Date(item.requestDate),
          approvedDate: item.approvedDate ? new Date(item.approvedDate) : null,
          borrowedDate: item.borrowedDate ? new Date(item.borrowedDate) : null,
          returnedDate: item.returnedDate ? new Date(item.returnedDate) : null,
          status: this.mapBackendState(item.state)
        };
        this.activeDetailRecord.set(mappedRecord);
      },
      error: (err) => {
        console.error('Không thể tải chi tiết đơn mượn trả:', err);
        // Fallback to basic passed record if api call fails
        this.activeDetailRecord.set(record);
      }
    });

    // Tải thông tin instance quy trình đi kèm của đơn này thông qua service gộp
    this.borrowRecordService.getWorkflowDetail(record.id)
      .pipe(finalize(() => this.loadingBpmn.set(false)))
      .subscribe({
        next: (detail) => {
          if (detail && detail.instance) {
            const instance = detail.instance;
            const pendingTask = instance.pendingTasks && instance.pendingTasks.length > 0 ? instance.pendingTasks[0] : null;
            this.detailPendingTask.set(pendingTask);
            this.detailCurrentNodeId.set(instance.currentNodeId || '');

            if (detail.definition && detail.definition.bpmnXml) {
              this.detailWorkflowXml.set(detail.definition.bpmnXml);
              if (pendingTask) {
                this.parseDynamicButtons(detail.definition.bpmnXml, pendingTask.stepName, instance.currentNodeId);
              }
            }

            this.detailHistoryLogs.set(detail.history || []);

            // Cập nhật trạng thái cục bộ của record để hiển thị ở list nếu cần
            this.entityWorkflowStates.update(states => ({
              ...states,
              [record.id]: {
                instanceId: instance.instanceId,
                status: instance.status,
                stepName: pendingTask ? pendingTask.stepName : 'Hoàn thành',
                assignedRole: pendingTask ? pendingTask.assignedRole : ''
              }
            }));
          }
        },
        error: (err) => {
          console.error('Không thể tải thông tin quy trình chi tiết:', err);
        }
      });
  }

  closeDetailDialog() {
    if (this.bpmnViewer) {
      this.bpmnViewer.destroy();
      this.bpmnViewer = null;
    }
    this.currentSubView.set('list');
    this.activeDetailRecord.set(null);
  }

  selectDetailTab(tab: 'info' | 'workflow' | 'history') {
    this.detailActiveTab.set(tab);
    if (tab === 'workflow') {
      setTimeout(() => {
        const xml = this.detailWorkflowXml();
        const nodeId = this.detailCurrentNodeId();
        this.initBpmnViewer(xml, nodeId);
      }, 150);
    }
  }

  async initBpmnViewer(xml: string, currentNodeId: string) {
    if (!xml || !isPlatformBrowser(this.platformId)) return;

    if (this.bpmnViewer) {
      this.bpmnViewer.destroy();
      this.bpmnViewer = null;
    }

    try {
      const Viewer = (await import('bpmn-js/lib/Viewer')).default;
      this.bpmnViewer = new Viewer({
        container: '#bpmn-canvas'
      });

      await this.bpmnViewer.importXML(xml);
      const canvas = this.bpmnViewer.get('canvas');
      canvas.zoom('fit-viewport');

      if (currentNodeId) {
        canvas.addMarker(currentNodeId, 'highlight-active-node');
      }
    } catch (err) {
      console.error('Error drawing BPMN diagram', err);
    }
  }

  // Xác thực quyền thực hiện thao tác duyệt của người dùng đăng nhập hiện tại
  get isUserAuthorizedForDetailAction(): boolean {
    const task = this.detailPendingTask();
    if (!task) return false;

    const userRoles = this.authService.getUserRoles();
    if (userRoles.includes('ADMIN') || userRoles.includes('OPERATOR')) {
      return true;
    }

    const taskRoles = task.assignedRole ? task.assignedRole.split(',').map((r: string) => r.trim()) : [];
    return taskRoles.some((r: string) => userRoles.includes(r));
  }

  parseDynamicButtons(xml: string, stepName: string, currentNodeId?: string) {
    try {
      const parser = new DOMParser();
      const doc = parser.parseFromString(xml, 'application/xml');

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

      const tasks = [
        ...getElementsByLocalName('task'),
        ...getElementsByLocalName('userTask')
      ];

      // Tìm node XML đại diện cho step hiện tại
      let currentTaskElement = currentNodeId ? tasks.find(t => t.getAttribute('id') === currentNodeId) : null;
      if (!currentTaskElement) {
        currentTaskElement = tasks.find(t => t.getAttribute('name') === stepName);
      }

      if (!currentTaskElement) {
        this.detailDynamicButtons.set([{ label: 'Xác nhận', targetNodeId: 'approve', requiresUser: false }]);
        return;
      }

      const currentTaskId = currentTaskElement.getAttribute('id');
      const sequenceFlows = getElementsByLocalName('sequenceFlow');
      const outgoingFlow = sequenceFlows.find(f => f.getAttribute('sourceRef') === currentTaskId);
      if (!outgoingFlow) {
        this.detailDynamicButtons.set([{ label: 'Xác nhận', targetNodeId: 'approve', requiresUser: false }]);
        return;
      }

      const targetRef = outgoingFlow.getAttribute('targetRef');
      if (!targetRef) {
        this.detailDynamicButtons.set([{ label: 'Xác nhận', targetNodeId: 'approve', requiresUser: false }]);
        return;
      }

      const allElements = doc.getElementsByTagName('*');
      let targetElement: Element | null = null;
      for (let i = 0; i < allElements.length; i++) {
        if (allElements[i].getAttribute('id') === targetRef) {
          targetElement = allElements[i];
          break;
        }
      }

      if (!targetElement) {
        this.detailDynamicButtons.set([{ label: 'Xác nhận', targetNodeId: 'approve', requiresUser: false }]);
        return;
      }

      const isElementATask = (id: string): boolean => {
        for (let i = 0; i < allElements.length; i++) {
          if (allElements[i].getAttribute('id') === id) {
            const ln = allElements[i].localName;
            return ln === 'task' || ln === 'userTask';
          }
        }
        return false;
      };

      const getRequiredRoleForElement = (id: string): string => {
        for (let i = 0; i < allElements.length; i++) {
          if (allElements[i].getAttribute('id') === id) {
            return allElements[i].getAttribute('requiredRole') || '';
          }
        }
        return '';
      };

      const isRejectionLabel = (label: string): boolean => {
        const lower = label.toLowerCase();
        return lower.includes('từ chối') || lower.includes('hủy') || lower.includes('reject') || lower.includes('cancel');
      };

      // Check if this step is an approval step
      const pendingTask = this.detailPendingTask();
      const actionType = pendingTask?.actionType || '';
      const isApproveStep = !actionType || 
                            actionType.toLowerCase().includes('duyệt') || 
                            actionType.toLowerCase().includes('approve');

      if (!isApproveStep) {
        // Automatically generate a single "Tiếp tục" button
        if (targetElement.localName.includes('Gateway')) {
          const gatewayFlows = sequenceFlows.filter(f => f.getAttribute('sourceRef') === targetRef);
          const approveFlow = gatewayFlows.find(f => {
            const name = f.getAttribute('name') || '';
            return !isRejectionLabel(name);
          }) || (gatewayFlows.length > 0 ? gatewayFlows[0] : null);

          const firstTarget = approveFlow ? approveFlow.getAttribute('targetRef') || '' : '';
          this.detailDynamicButtons.set([{
            label: 'Tiếp tục',
            targetNodeId: firstTarget,
            requiresUser: isElementATask(firstTarget),
            requiredRole: getRequiredRoleForElement(firstTarget)
          }]);
        } else {
          this.detailDynamicButtons.set([{
            label: 'Tiếp tục',
            targetNodeId: targetRef,
            requiresUser: isElementATask(targetRef),
            requiredRole: getRequiredRoleForElement(targetRef)
          }]);
        }
        return;
      }

      // Nếu target tiếp theo là Gateway => sinh ra các nút rẽ nhánh
      if (targetElement.localName.includes('Gateway')) {
        const gatewayFlows = sequenceFlows.filter(f => f.getAttribute('sourceRef') === targetRef);
        this.detailDynamicButtons.set(gatewayFlows.map(flow => {
          const flowTargetRef = flow.getAttribute('targetRef') || '';
          const label = flow.getAttribute('name') || 'Tiếp tục';
          const isReject = isRejectionLabel(label);
          return {
            label: label,
            targetNodeId: flowTargetRef,
            requiresUser: !isReject && isElementATask(flowTargetRef),
            requiredRole: isReject ? '' : getRequiredRoleForElement(flowTargetRef)
          };
        }));
      } else {
        // Chỉ đi tiếp bình thường hoặc kết thúc
        const isEnd = targetElement.localName === 'endEvent';
        this.detailDynamicButtons.set([{
          label: isEnd ? 'Hoàn thành' : 'Chuyển tiếp',
          targetNodeId: targetRef,
          requiresUser: !isEnd && isElementATask(targetRef),
          requiredRole: isEnd ? '' : getRequiredRoleForElement(targetRef)
        }]);
      }
    } catch (err) {
      console.error('Error parsing dynamic transition buttons', err);
      this.detailDynamicButtons.set([
        { label: 'Duyệt', targetNodeId: 'approve', requiresUser: true, requiredRole: '' }, 
        { label: 'Từ chối', targetNodeId: 'reject', requiresUser: false, requiredRole: '' }
      ]);
    }
  }

  submitDetailMoveAction(targetNodeId: string, actionLabel: string, requiresUser?: boolean) {
    const record = this.activeDetailRecord();
    if (!record) return;

    const isCancel = actionLabel === 'Từ chối' || actionLabel.includes('Từ chối') || actionLabel === 'Hủy' || actionLabel.includes('Hủy');

    if (requiresUser && !isCancel && !this.selectedNextUserId()) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi',
        detail: 'Vui lòng chọn người xử lý ở bước tiếp theo.'
      });
      return;
    }

    this.detailActionSubmitting.set(true);

    this.borrowRecordService.moveWorkflow(
      record.id,
      targetNodeId,
      actionLabel,
      this.detailActionComment(),
      (!isCancel && requiresUser) ? this.selectedNextUserId() : undefined
    )
    .pipe(finalize(() => this.detailActionSubmitting.set(false)))
    .subscribe({
      next: (res) => {
        this.messageService.add({
          severity: 'success',
          summary: 'Thành công',
          detail: res.message || 'Phê duyệt/luân chuyển bước quy trình hoàn tất.'
        });
        this.closeDetailDialog();
        this.loadMyTasks();
        this.loadRequests();
      },
      error: (err: any) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: err.message || 'Lỗi khi thực hiện chuyển bước quy trình.'
        });
      }
    });
  }

  // --- Helpers ---
  mapBackendState(state: any): string {
    switch (state) {
      case 0:
      case 'Requested':
        return 'PENDING';
      case 1:
      case 'Approved':
        return 'APPROVED';
      case 2:
      case 'Borrowed':
        return 'BORROWED';
      case 3:
      case 'Returned':
        return 'RETURNED';
      default:
        return 'PENDING';
    }
  }

  mapStateToBackendEnum(status: string): number {
    switch (status) {
      case 'APPROVED': return 1;
      case 'BORROWED': return 2;
      case 'RETURNED': return 3;
      default: return 0;
    }
  }

  nextPage() {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(p => p + 1);
    }
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
    }
  }

  goToPage(page: any) {
    const p = Number(page);
    if (p >= 1 && p <= this.totalPages()) {
      this.currentPage.set(p);
    }
  }

  onPageSizeChange(event: any) {
    this.pageSize.set(Number(event.target.value));
    this.currentPage.set(1);
  }

  updateStatus(id: string, status: string) {
    const backendState = this.mapStateToBackendEnum(status);
    
    this.borrowRecordService.updateState(id, backendState).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success', 
          summary: 'Thành công', 
          detail: `Đã cập nhật trạng thái yêu cầu thành công!`
        });
        this.loadRequests();
      },
      error: (err) => {
        console.error('Error updating status via API', err);
        this.messageService.add({
          severity: 'error',
          summary: 'Lỗi',
          detail: 'Không thể cập nhật trạng thái yêu cầu trên hệ thống.'
        });
      }
    });
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'PENDING': return 'Chờ phê duyệt';
      case 'APPROVED': return 'Đã duyệt';
      case 'BORROWED': return 'Đang mượn';
      case 'RETURNED': return 'Bị trả lại';
      case 'REJECTED': return 'Từ chối';
      default: return status;
    }
  }
}
