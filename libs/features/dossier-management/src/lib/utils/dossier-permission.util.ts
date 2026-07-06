import { AuthService } from '@sohoa.frontend/shared/core';

export function isDigitizationKind(kindId?: number | null): boolean {
  return kindId === 1;
}

/** Chuẩn hóa kindId từ API/route — mặc định fallback (thường từ route data). */
export function normalizeDossierKindId(raw: unknown, fallback = 2): number {
  const parsed = Number(raw);
  if (parsed === 1) return 1;
  if (parsed === 2) return 2;
  return fallback === 1 ? 1 : 2;
}

export function hasDossierCreatePermission(auth: AuthService, isDigitization: boolean): boolean {
  if (auth.hasPermission('SUPER_ADMIN')) return true;
  return isDigitization
    ? auth.hasPermission('DOSSIER_DIGITIZATION_CREATE')
    : auth.hasPermission('DOSSIER_CREATE');
}

export function hasDossierEditPermission(auth: AuthService, isDigitization: boolean): boolean {
  if (auth.hasPermission('SUPER_ADMIN')) return true;
  return isDigitization
    ? auth.hasPermission('DOSSIER_DIGITIZATION_EDIT')
    : auth.hasPermission('DOSSIER_EDIT');
}

/** Menu creator: sửa hồ sơ nháp / tài liệu cần CREATE hoặc EDIT theo loại hồ sơ. */
export function canMutateDossierOnCreatorMenu(auth: AuthService, isDigitization: boolean): boolean {
  return hasDossierCreatePermission(auth, isDigitization) || hasDossierEditPermission(auth, isDigitization);
}

/** Quyền gọi API bóc tách lại / OCR tài liệu hồ sơ (DOSSIER_*_IMPORT). */
export function hasDossierDigitizationImportPermission(auth: AuthService, isDigitization: boolean): boolean {
  if (auth.hasPermission('SUPER_ADMIN')) return true;
  return isDigitization
    ? auth.hasPermission('DOSSIER_DIGITIZATION_IMPORT')
    : auth.hasPermission('DOSSIER_IMPORT');
}
