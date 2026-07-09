export interface EavFormPermissionChecker {
  hasPermission(code: string): boolean;
}

function isSuperAdmin(auth: EavFormPermissionChecker): boolean {
  return auth.hasPermission('SUPER_ADMIN');
}

export function canViewDesign(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_VIEW');
}

export function canCreateForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_CREATE');
}

export function canEditForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_EDIT');
}

export function canSubmitForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_SUBMIT');
}

export function canDeleteForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_DELETE');
}

/** EavFormApprovalController */
export function canViewApproval(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_APPROVAL_VIEW');
}

export function canApproveForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_APPROVAL_APPROVE');
}

/** EavCompletedFormController */
export function canViewCompleted(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_COMPLETED_FORM_VIEW');
}

export function canManageCompletedForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_COMPLETED_FORM_MANAGE');
}

export function canDeleteCompletedForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_COMPLETED_FORM_DELETE');
}
