export const DOSSIER_STATUS_LABELS: Record<string, string> = {
  New: 'Tạo mới',
  CompletedInput: 'Hoàn thành',
  PendingApproval: 'Chờ duyệt',
  InProgress: 'Đang xử lý',
  Returned: 'Trả lại',
  Approved: 'Đã duyệt',
};

export const DOSSIER_STATUS_LABELS_BY_ID: Record<number, string> = {
  1: 'Tạo mới',
  2: 'Hoàn thành',
  3: 'Chờ duyệt',
  4: 'Đang xử lý',
  5: 'Trả lại',
  6: 'Đã duyệt',
};

export function getStatusCodeById(id?: number | null): string {
  switch (id) {
    case 1: return 'New';
    case 2: return 'CompletedInput';
    case 3: return 'PendingApproval';
    case 4: return 'InProgress';
    case 5: return 'Returned';
    case 6: return 'Approved';
    default: return '';
  }
}

export function getDossierStatusPillClass(status?: string | number | null): string {
  if (status === null || status === undefined || status === '') return 'status-pill';
  const statusNum = Number(status);
  const statusVal = !isNaN(statusNum) ? getStatusCodeById(statusNum) : status;
  switch (statusVal) {
    case 'New':
      return 'status-pill status-new';
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

export function getDossierStatusLabel(status?: string | number | null, statusName?: string | null): string {
  if (statusName) return statusName;
  if (status === null || status === undefined || status === '') return '—';
  const statusNum = Number(status);
  if (!isNaN(statusNum)) {
    return DOSSIER_STATUS_LABELS_BY_ID[statusNum] ?? '—';
  }
  return DOSSIER_STATUS_LABELS[status] ?? String(status);
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

export function getTabsForMenuScope(scope: DossierMenuScope, _kindId?: number): DossierListTab[] {
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
