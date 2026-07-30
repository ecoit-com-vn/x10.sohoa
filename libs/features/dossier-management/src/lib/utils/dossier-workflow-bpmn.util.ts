export interface WorkflowActionButton {
  label: string;
  targetNodeId: string;
  requiresUser: boolean;
  requiredRole: string;
  /** Cấu hình bổ sung của bước ĐÍCH (targetNodeId) — dùng để lọc/gợi ý người xử lý theo đúng ưu tiên nghiệp vụ. */
  unitGroupIds?: string;
  systemGroupIds?: string;
  requireSameUnit?: boolean;
  staticAssigneeId?: string;
}

export function isRejectWorkflowLabel(label?: string | null): boolean {
  const l = (label || '').toLowerCase();
  return l.includes('từ chối')
    || l.includes('hủy')
    || l.includes('reject')
    || l.includes('cancel')
    || l.includes('trả lại');
}

/** Nhận diện thao tác từ chối/trả lại từ API workflow (name/code). */
export function isRejectWorkflowAction(action: {
  name?: string | null;
  code?: string | null;
  label?: string | null;
  Name?: string | null;
  Code?: string | null;
}): boolean {
  const code = String(action.code ?? action.Code ?? '').toUpperCase();
  if (code === 'REJECT') return true;
  return isRejectWorkflowLabel(action.name ?? action.Name ?? action.label ?? '');
}

/** Đưa thao tác từ chối/trả lại xuống cuối danh sách (menu mở rộng, nút workflow). */
export function sortWorkflowActionsRejectLast<T>(actions: T[]): T[] {
  const rejects: T[] = [];
  const others: T[] = [];
  for (const action of actions) {
    const item = action as Record<string, unknown>;
    const isReject = isRejectWorkflowAction({
      name: String(item['name'] ?? item['Name'] ?? item['label'] ?? item['Label'] ?? ''),
      code: String(item['code'] ?? item['Code'] ?? ''),
    });
    if (isReject) rejects.push(action);
    else others.push(action);
  }
  return [...others, ...rejects];
}

export function sortWorkflowButtonsRejectLast(buttons: WorkflowActionButton[]): WorkflowActionButton[] {
  return sortWorkflowActionsRejectLast(buttons);
}

export function isApproveWorkflowLabel(label?: string | null): boolean {
  const l = (label || '').toLowerCase();
  return l.includes('đồng ý')
    || l.includes('phê duyệt')
    || l.includes('xác nhận')
    || l.includes('approve');
}

export function parseWorkflowActionButtons(
  xml: string,
  stepName: string,
  currentNodeId?: string
): WorkflowActionButton[] {
  try {
    const parser = new DOMParser();
    const doc = parser.parseFromString(xml, 'application/xml');

    const byLocalName = (name: string): Element[] => {
      const res: Element[] = [];
      const all = doc.getElementsByTagName('*');
      for (let i = 0; i < all.length; i++) {
        if (all[i].localName === name) res.push(all[i]);
      }
      return res;
    };

    const allElements = doc.getElementsByTagName('*');

    const getElementById = (id: string): Element | null => {
      for (let i = 0; i < allElements.length; i++) {
        if (allElements[i].getAttribute('id') === id) return allElements[i];
      }
      return null;
    };

    const isTask = (id: string) => {
      const el = getElementById(id);
      return el ? el.localName === 'task' || el.localName === 'userTask' : false;
    };

    const getRole = (id: string) => getElementById(id)?.getAttribute('requiredRole') || '';

    // Cấu hình người xử lý của bước ĐÍCH — khớp đúng tên thuộc tính BPMN do workflow-builder ghi ra.
    const getAssigneeConfig = (id: string) => {
      const el = getElementById(id);
      return {
        unitGroupIds: el?.getAttribute('unitPermissionGroupIds') || '',
        systemGroupIds: el?.getAttribute('systemPermissionGroupIds') || '',
        requireSameUnit: el?.getAttribute('requireSameUnit') === 'true',
        staticAssigneeId: el?.getAttribute('assigneeId') || '',
      };
    };

    const tasks = [...byLocalName('task'), ...byLocalName('userTask')];
    const seqFlows = byLocalName('sequenceFlow');

    let currentEl = currentNodeId ? tasks.find(t => t.getAttribute('id') === currentNodeId) : null;
    if (!currentEl) currentEl = tasks.find(t => t.getAttribute('name') === stepName) ?? null;

    if (!currentEl) {
      return [{ label: 'Xác nhận', targetNodeId: '', requiresUser: false, requiredRole: '' }];
    }

    const currentId = currentEl.getAttribute('id');

    const startEvents = byLocalName('startEvent');
    const isFirstStep = startEvents.some(se => {
      const startId = se.getAttribute('id');
      return seqFlows.some(f => f.getAttribute('sourceRef') === startId && f.getAttribute('targetRef') === currentId);
    });

    const outFlow = seqFlows.find(f => f.getAttribute('sourceRef') === currentId);
    if (!outFlow) {
      return [{ label: 'Xác nhận', targetNodeId: '', requiresUser: false, requiredRole: '' }];
    }

    const targetRef = outFlow.getAttribute('targetRef') || '';
    const targetEl = getElementById(targetRef);
    if (!targetEl) {
      return [{ label: 'Xác nhận', targetNodeId: '', requiresUser: false, requiredRole: '' }];
    }

    if (targetEl.localName.includes('Gateway')) {
      const gwFlows = seqFlows.filter(f => f.getAttribute('sourceRef') === targetRef);
      return sortWorkflowButtonsRejectLast(gwFlows.map(flow => {
        const ftRef = flow.getAttribute('targetRef') || '';
        let label = flow.getAttribute('name') || 'Tiếp tục';
        const isReject = isRejectWorkflowLabel(label);
        if (isFirstStep && !isReject) {
          label = 'Gửi duyệt';
        }
        return {
          label,
          targetNodeId: ftRef,
          requiresUser: !isReject && isTask(ftRef),
          requiredRole: isReject ? '' : getRole(ftRef),
          ...(isReject ? {} : getAssigneeConfig(ftRef)),
        };
      }));
    }

    if (targetEl.localName === 'endEvent') {
      return [{ label: 'Hoàn thành', targetNodeId: targetRef, requiresUser: false, requiredRole: '' }];
    }

    const label = isFirstStep ? 'Gửi duyệt' : 'Chuyển tiếp';
    return [{
      label,
      targetNodeId: targetRef,
      requiresUser: isTask(targetRef),
      requiredRole: getRole(targetRef),
      ...getAssigneeConfig(targetRef),
    }];
  } catch {
    return sortWorkflowButtonsRejectLast([
      { label: 'Đồng ý', targetNodeId: '', requiresUser: true, requiredRole: '' },
      { label: 'Từ chối', targetNodeId: '', requiresUser: false, requiredRole: '' },
    ]);
  }
}

export function resolveWorkflowActionButton(
  buttons: WorkflowActionButton[],
  action: { name?: string; nextNodeId?: string }
): WorkflowActionButton | null {
  const nextNodeId = action.nextNodeId?.trim();
  if (nextNodeId) {
    const byNode = buttons.find(b => b.targetNodeId === nextNodeId);
    if (byNode) return byNode;
  }

  const actionName = (action.name ?? '').trim().toLowerCase();
  if (!actionName) return null;

  return buttons.find(b => b.label.trim().toLowerCase() === actionName) ?? null;
}

/** Lấy tên node BPMN theo id. */
export function getBpmnNodeName(bpmnXml: string, nodeId: string): string {
  if (!bpmnXml?.trim() || !nodeId?.trim()) return '';
  try {
    const parser = new DOMParser();
    const doc = parser.parseFromString(bpmnXml, 'application/xml');
    const all = doc.getElementsByTagName('*');
    for (let i = 0; i < all.length; i++) {
      if (all[i].getAttribute('id') === nodeId) {
        return all[i].getAttribute('name')?.trim() || '';
      }
    }
  } catch {
    /* ignore */
  }
  return '';
}

/** Fallback role từ WORKFLOW_STEPS khi BPMN XML không có requiredRole. */
export function resolveRequiredRoleFromDefinition(
  definition: Record<string, unknown> | null | undefined,
  targetNodeId: string,
  bpmnXml: string
): string {
  if (!definition || !targetNodeId?.trim()) return '';
  const steps = definition['steps'] ?? definition['Steps'];
  if (!Array.isArray(steps) || steps.length === 0) return '';

  const nodeName = getBpmnNodeName(bpmnXml, targetNodeId);
  if (!nodeName) return '';

  const normalized = nodeName.toLowerCase();
  const match = steps.find((s: Record<string, unknown>) =>
    String(s['stepName'] ?? s['StepName'] ?? '').toLowerCase() === normalized
  );
  return String(match?.['requiredRole'] ?? match?.['RequiredRole'] ?? '').trim();
}

export interface NextStepAssigneeInfo {
  staticAssigneeId?: string | null;
  unitGroupIds?: string | null;
  systemGroupIds?: string | null;
}

/**
 * Tham số gọi API eligible-assignees để xây danh sách người xử lý của bước tiếp theo,
 * ưu tiên: Nhóm quyền đơn vị > Nhóm quyền hệ thống.
 * Lưu ý: giao việc đích danh KHÔNG rút gọn danh sách xuống 1 người — người chuyển bước vẫn
 * được chọn người khác — nó chỉ quyết định giá trị được CHỌN SẴN (xem resolveDefaultNextAssignee).
 * Trả về null khi bước không cấu hình nhóm quyền nào (dropdown khi đó dùng danh sách người dùng đầy đủ).
 */
export function resolveEligibleAssigneeGroupParams(
  info: NextStepAssigneeInfo | null | undefined
): { unitGroupIds?: string; systemGroupIds?: string } | null {
  if (!info) return null;
  if (info.unitGroupIds) return { unitGroupIds: info.unitGroupIds };
  if (info.systemGroupIds) return { systemGroupIds: info.systemGroupIds };
  return null;
}

/** Giá trị chọn sẵn mặc định cho dropdown người xử lý — ưu tiên hàng đầu là người được giao việc đích danh. */
export function resolveDefaultNextAssignee(info: NextStepAssigneeInfo | null | undefined): string {
  return info?.staticAssigneeId ? String(info.staticAssigneeId) : '';
}

const getUserKey = (u: any): string => String(u?.id ?? u?.Id ?? u?.userId ?? u?.username ?? '');

/**
 * Danh sách người xử lý hiển thị trong dropdown theo đúng ưu tiên nghiệp vụ:
 * Nhóm quyền đơn vị/hệ thống (nếu cấu hình) > requiredRole cũ > toàn bộ người dùng.
 * Người được giao việc đích danh LUÔN có mặt trong danh sách (dù có khớp nhóm quyền hay không) —
 * vì đây là lựa chọn ưu tiên hàng đầu theo nghiệp vụ, chỉ là không bắt buộc phải chọn đúng người đó.
 */
export function resolveNextUserCandidates(params: {
  info: NextStepAssigneeInfo & { requiredRole?: string | null } | null | undefined;
  allUsers: any[];
  eligibleUsers: any[];
}): any[] {
  const { info, allUsers, eligibleUsers } = params;
  if (!info) return [];

  let list: any[];
  if (info.unitGroupIds || info.systemGroupIds) list = eligibleUsers;
  else if (info.requiredRole) list = filterUsersByRequiredRole(allUsers, info.requiredRole);
  else list = allUsers;

  if (info.staticAssigneeId) {
    const key = String(info.staticAssigneeId);
    if (!list.some((u) => getUserKey(u) === key)) {
      const known = allUsers.find((u) => getUserKey(u) === key);
      list = [known ?? { id: info.staticAssigneeId, fullName: info.staticAssigneeId }, ...list];
    }
  }

  return list;
}

/** Lọc user theo role (hỗ trợ nhiều role phân tách bằng dấu phẩy). */
export function filterUsersByRequiredRole(users: any[], requiredRole?: string | null): any[] {
  if (!requiredRole?.trim()) return users;
  const roles = requiredRole.split(',').map((r) => r.trim().toUpperCase()).filter(Boolean);
  if (roles.length === 0) return users;
  return users.filter((u) => {
    const uRoles: string[] = (u.roles || u.Roles || []).map((r: string) => String(r).toUpperCase());
    return uRoles.some((r) => roles.includes(r));
  });
}
