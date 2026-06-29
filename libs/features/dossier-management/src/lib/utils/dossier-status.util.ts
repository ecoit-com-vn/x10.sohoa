/** Label hiển thị pill — thống nhất toàn module hồ sơ */
export const DOSSIER_STATUS_LABELS: Record<string, string> = {
  Draft: 'Nháp',
  PendingApproval: 'Chờ duyệt',
  InProgress: 'Đang duyệt',
  Returned: 'Trả lại',
  Approved: 'Đã duyệt',
};

export function getDossierStatusPillClass(status?: string | null): string {
  switch (status) {
    case 'Draft':
      return 'status-pill status-inactive';
    case 'PendingApproval':
    case 'InProgress':
      return 'status-pill status-pending';
    case 'Approved':
      return 'status-pill status-active';
    case 'Returned':
      return 'status-pill status-inactive';
    default:
      return 'status-pill';
  }
}

export function getDossierStatusLabel(status?: string | null): string {
  if (!status) return '—';
  return DOSSIER_STATUS_LABELS[status] ?? status;
}

const TECHNICAL_WORKFLOW_LABELS = new Set([
  'running',
  'completed',
  'terminated',
  'pending',
]);

function isTechnicalWorkflowLabel(value?: string | null): boolean {
  const normalized = value?.trim().toLowerCase();
  return !!normalized && TECHNICAL_WORKFLOW_LABELS.has(normalized);
}

export { isTechnicalWorkflowLabel };

function isWeakWorkflowStepLabel(value?: string | null): boolean {
  if (isTechnicalWorkflowLabel(value)) return true;
  const trimmed = value?.trim();
  if (!trimmed) return true;
  if (/^\d+$/.test(trimmed)) return true;
  return trimmed.startsWith('Activity_');
}

export function getDossierWorkflowStepSubtitle(
  status?: string | null,
  workflowStepName?: string | null
): string | null {
  const step = workflowStepName?.trim();
  if (!step || isWeakWorkflowStepLabel(step)) return null;
  if (status === 'Draft' || status === 'Approved') return null;
  return step;
}

export type DossierListTab =
  | 'draft'
  | 'pending-action'
  | 'in-progress'
  | 'completed'
  | 'returned';

export type DossierMenuScope = 'creator' | 'approver';

export const DOSSIER_CREATOR_TABS: DossierListTab[] = ['draft', 'returned', 'in-progress', 'completed'];
export const DOSSIER_APPROVER_TABS: DossierListTab[] = ['pending-action', 'in-progress', 'completed'];

export function getDefaultTabForMenuScope(scope: DossierMenuScope): DossierListTab {
  return scope === 'approver' ? 'pending-action' : 'draft';
}

export function getTabsForMenuScope(scope: DossierMenuScope): DossierListTab[] {
  return scope === 'approver' ? DOSSIER_APPROVER_TABS : DOSSIER_CREATOR_TABS;
}

export interface DossierTabCounts {
  draft: number;
  pendingAction: number;
  inProgress: number;
  completed: number;
  returned: number;
}
