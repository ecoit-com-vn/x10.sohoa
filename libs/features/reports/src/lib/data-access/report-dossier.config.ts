export type ReportDossierType =
  | 'dossier-by-grid-type'
  | 'dossier-by-equipment'
  | 'dossier-by-line';

export interface ReportDossierConfig {
  apiSegment: ReportDossierType;
  viewPermission: string;
  exportPermission: string;
  dimensionLabel: string;
  secondaryFilterLabel: string;
  secondaryLookup: 'gridTypes' | 'equipments' | 'lines';
  dimensionField: 'gridTypeName' | 'equipmentName' | 'infrastructureName';
  listRoute: string;
}

export const REPORT_DOSSIER_CONFIGS: Record<ReportDossierType, ReportDossierConfig> = {
  'dossier-by-grid-type': {
    apiSegment: 'dossier-by-grid-type',
    viewPermission: 'REPORT_DOSSIER_BY_GRIDTYPE_VIEW',
    exportPermission: 'REPORT_DOSSIER_BY_GRIDTYPE_EXPORT',
    dimensionLabel: 'Loại lưới điện',
    secondaryFilterLabel: 'Loại lưới điện',
    secondaryLookup: 'gridTypes',
    dimensionField: 'gridTypeName',
    listRoute: '/reports/dossier-by-grid-type'
  },
  'dossier-by-equipment': {
    apiSegment: 'dossier-by-equipment',
    viewPermission: 'REPORT_DOSSIER_BY_EQUIPMENT_VIEW',
    exportPermission: 'REPORT_DOSSIER_BY_EQUIPMENT_EXPORT',
    dimensionLabel: 'Thiết bị',
    secondaryFilterLabel: 'Thiết bị',
    secondaryLookup: 'equipments',
    dimensionField: 'equipmentName',
    listRoute: '/reports/dossier-by-equipment'
  },
  'dossier-by-line': {
    apiSegment: 'dossier-by-line',
    viewPermission: 'REPORT_DOSSIER_BY_LINE_VIEW',
    exportPermission: 'REPORT_DOSSIER_BY_LINE_EXPORT',
    dimensionLabel: 'Trạm / Đường dây',
    secondaryFilterLabel: 'Chọn đường dây',
    secondaryLookup: 'lines',
    dimensionField: 'infrastructureName',
    listRoute: '/reports/dossier-by-line'
  }
};

export function getReportDossierConfig(type: ReportDossierType): ReportDossierConfig {
  return REPORT_DOSSIER_CONFIGS[type];
}
