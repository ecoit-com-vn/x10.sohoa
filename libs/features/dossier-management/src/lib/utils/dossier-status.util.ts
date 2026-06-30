export const DOSSIER_STATUS_LABELS: Record<string, string> = {
  New: 'Tạo mới',
  CompletedInput: 'Hoàn thành nhập liệu',
  PendingApproval: 'Chờ duyệt',
  InProgress: 'Đang duyệt',
  Returned: 'Trả lại',
  Approved: 'Đã duyệt',
};

export function getDossierStatusPillClass(status?: string | null): string {
  switch (status) {
    case 'New':
      return 'wf-status-field';
    case 'CompletedInput':
      return 'status-pill status-completed-input';
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
  if (status === 'New' || status === 'CompletedInput' || status === 'Approved') return null;
  return step;
}

export type DossierListTab =
  | 'draft'
  | 'pending-action'
  | 'in-progress'
  | 'completed'
  | 'returned'
  | 'pending-publish'
  | 'published'
  | 'unpublished';

export type DossierMenuScope = 'creator' | 'approver' | 'publisher';

export const DOSSIER_CREATOR_TABS: DossierListTab[] = ['draft', 'returned', 'in-progress', 'completed'];
export const DOSSIER_APPROVER_TABS: DossierListTab[] = ['pending-action', 'in-progress', 'completed'];
export const DOSSIER_PUBLISHER_TABS: DossierListTab[] = ['pending-publish', 'published', 'unpublished'];

export function getDefaultTabForMenuScope(scope: DossierMenuScope): DossierListTab {
  if (scope === 'approver') return 'pending-action';
  if (scope === 'publisher') return 'pending-publish';
  return 'draft';
}

export function getTabsForMenuScope(scope: DossierMenuScope): DossierListTab[] {
  if (scope === 'approver') return DOSSIER_APPROVER_TABS;
  if (scope === 'publisher') return DOSSIER_PUBLISHER_TABS;
  return DOSSIER_CREATOR_TABS;
}

export interface DossierTabCounts {
  draft: number;
  pendingAction: number;
  inProgress: number;
  completed: number;
  returned: number;
  pendingPublish?: number;
  published?: number;
  unpublished?: number;
}
