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

export function canViewApproval(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_APPROVAL_VIEW');
}

export function canApproveForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_APPROVE');
}

export function canViewCompleted(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_COMPLETED_VIEW');
}

export function canManageForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_MANAGE');
}

export function canDeleteForm(auth: EavFormPermissionChecker): boolean {
  return isSuperAdmin(auth) || auth.hasPermission('EAV_FORM_TEMPLATE_DELETE');
}
