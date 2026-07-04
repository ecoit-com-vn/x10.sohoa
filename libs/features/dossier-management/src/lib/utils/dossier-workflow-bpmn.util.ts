export interface WorkflowActionButton {
  label: string;
  targetNodeId: string;
  requiresUser: boolean;
  requiredRole: string;
}

export function isRejectWorkflowLabel(label?: string | null): boolean {
  const l = (label || '').toLowerCase();
  return l.includes('từ chối')
    || l.includes('hủy')
    || l.includes('reject')
    || l.includes('cancel')
    || l.includes('trả lại');
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
      return gwFlows.map(flow => {
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
        };
      });
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
    }];
  } catch {
    return [
      { label: 'Đồng ý', targetNodeId: '', requiresUser: true, requiredRole: '' },
      { label: 'Từ chối', targetNodeId: '', requiresUser: false, requiredRole: '' },
    ];
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
