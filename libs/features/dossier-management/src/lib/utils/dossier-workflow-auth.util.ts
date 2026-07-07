import { AuthService } from '@sohoa.frontend/shared/core';
import { DossierListTab, DossierMenuScope } from './dossier-status.util';
import { parseWorkflowActionButtons, sortWorkflowActionsRejectLast, sortWorkflowButtonsRejectLast } from './dossier-workflow-bpmn.util';

export function normalizeWorkflowUserId(val: unknown): string {
  return val ? String(val).replace(/-/g, '').toLowerCase().trim() : '';
}

/** Chỉ coi là userId (GUID), bỏ qua mã role trong currentAssignees. */
function isLikelyUserId(val: unknown): boolean {
  const norm = normalizeWorkflowUserId(val);
  return /^[0-9a-f]{32}$/.test(norm);
}

/**
 * Phân quyền thao tác workflow — chỉ theo userId đích danh.
 * Role chỉ dùng khi chọn người xử lý bước tiếp theo (UI picker), không dùng fallback ủy quyền.
 */
export function isUserAuthorizedForWorkflowAction(options: {
  authService: AuthService;
  menuScope: DossierMenuScope;
  assigneeUserId?: string | null;
  currentAssignees?: string[];
  statusId?: number | null;
  isCreator?: boolean;
  hasMyTask?: boolean;
}): boolean {
  if (options.hasMyTask) return true;

  const roles = options.authService.getUserRoles?.() ?? [];
  if (roles.includes('ADMIN') || roles.includes('OPERATOR')) return true;

  const userId = options.authService.getUserId();
  const normUserId = normalizeWorkflowUserId(userId);
  if (!normUserId) return false;

  const matchesUserId = (candidate: unknown): boolean =>
    isLikelyUserId(candidate) && normalizeWorkflowUserId(candidate) === normUserId;

  const assigneeId = options.assigneeUserId;
  if (assigneeId) {
    return matchesUserId(assigneeId);
  }

  const userAssignees = (options.currentAssignees ?? []).filter(isLikelyUserId);
  if (userAssignees.length > 0) {
    const matched = userAssignees.some(matchesUserId);
    if (options.menuScope === 'approver') return matched;
    if (options.statusId === 5 && (matched || options.isCreator)) return true;
    return false;
  }

  if (options.menuScope === 'creator' && options.statusId === 5 && options.isCreator) {
    return true;
  }

  return false;
}

export function mapAvailableActionsToButtons(actions: Array<Record<string, unknown>> | null | undefined) {
  if (!Array.isArray(actions) || actions.length === 0) return [];

  return sortWorkflowButtonsRejectLast(
    actions.map((action) => ({
      label: String(action['name'] ?? action['Name'] ?? ''),
      targetNodeId: String(action['nextNodeId'] ?? action['NextNodeId'] ?? ''),
      requiresUser: Boolean(action['requiresNextAssignee'] ?? action['RequiresNextAssignee'] ?? false),
      requiredRole: String(action['nextStepRole'] ?? action['NextStepRole'] ?? ''),
    })).filter((btn) => !!btn.label)
  );
}

function pickFirst<T>(...values: T[]): T | undefined {
  for (const v of values) {
    if (v !== undefined && v !== null && v !== '') return v;
  }
  return undefined;
}

export interface DossierListItemPatch {
  statusId: number;
  status?: unknown;
  statusName?: unknown;
  workflowInstanceId?: unknown;
  workflowStepName?: unknown;
  workflowStatusName?: unknown;
  currentAssignees: string[];
  availableActions: unknown[];
}

/** Ghép patch cho 1 dòng danh sách từ API Oracle + workflow (không chờ ES). */
export function buildListItemPatchFromSources(detail: any, workflowRes: any | null): DossierListItemPatch {
  const patch: DossierListItemPatch = {
    statusId: Number(detail?.statusId ?? detail?.StatusId ?? 0),
    status: detail?.status ?? detail?.Status ?? detail?.statusCode ?? detail?.StatusCode,
    statusName: detail?.statusName ?? detail?.StatusName,
    workflowInstanceId: detail?.workflowInstanceId ?? detail?.WorkflowInstanceId ?? null,
    workflowStepName: detail?.workflowStatusName ?? detail?.WorkflowStatusName ?? null,
    currentAssignees: [],
    availableActions: [],
  };

  const instance = workflowRes?.instance;
  if (!instance) {
    patch.availableActions = [];
    patch.currentAssignees = [];
    return patch;
  }

  patch.workflowInstanceId = pickFirst(
    instance.instanceId,
    instance.InstanceId,
    instance.id,
    instance.Id,
    patch.workflowInstanceId
  );
  patch.workflowStepName = pickFirst(
    instance.currentStepName,
    instance.CurrentStepName,
    patch.workflowStepName
  );
  patch.workflowStatusName = patch.workflowStepName;

  const pendingList: any[] = Array.isArray(instance.pendingTasks)
    ? instance.pendingTasks
    : Array.isArray(instance.PendingTasks)
      ? instance.PendingTasks
      : [];

  const assignees = pendingList
    .map((task) => task?.assigneeUserId ?? task?.AssigneeUserId)
    .filter((id) => isLikelyUserId(id));
  patch.currentAssignees = [...new Set(assignees)];

  const rawActions = instance.availableActions ?? instance.AvailableActions;
  if (Array.isArray(rawActions) && rawActions.length > 0) {
    patch.availableActions = sortWorkflowActionsRejectLast(rawActions);
  } else {
    const bpmnXml = workflowRes?.definition?.bpmnXml
      ?? workflowRes?.definition?.BpmnXml
      ?? workflowRes?.definition?.workflowXml
      ?? workflowRes?.definition?.WorkflowXml;
    const nodeId = pickFirst(instance.currentNodeId, instance.CurrentNodeId);
    const stepName = pickFirst(
      pendingList[0]?.stepName,
      pendingList[0]?.StepName,
      instance.currentStepName,
      instance.CurrentStepName
    ) ?? '';
    if (bpmnXml && nodeId) {
      patch.availableActions = parseWorkflowActionButtons(bpmnXml, stepName, nodeId).map((btn) => ({
        name: btn.label,
        nextNodeId: btn.targetNodeId,
        requiresNextAssignee: btn.requiresUser,
        nextStepRole: btn.requiredRole,
      }));
    } else {
      patch.availableActions = [];
    }
  }

  return patch;
}

/** Sau thao tác nhanh: hồ sơ còn thuộc tab hiện tại không? */
export function shouldKeepItemOnTab(
  tab: DossierListTab,
  patch: { statusId?: number; workflowInstanceId?: unknown }
): boolean {
  const statusId = Number(patch.statusId ?? 0);
  const hasWorkflow = !!patch.workflowInstanceId;

  switch (tab) {
    case 'draft':
      return !hasWorkflow && (statusId === 1 || statusId === 2);
    case 'returned':
      return statusId === 5;
    case 'pending-action':
    case 'in-progress':
    case 'completed':
      return hasWorkflow || statusId >= 3;
    default:
      return true;
  }
}
