/** Cấu hình tab Danh sách hồ sơ — mỗi báo cáo thống kê thêm một entry. */
export interface ReportStatisticsDossierListConfig {
  reportCode: string;
  listSegment: string;
}

/** Cấu hình View Lưới hồ sơ theo trạm — tab Báo cáo thống kê. */
export interface ReportStatisticsStationGridConfig {
  reportCode: string;
  gridSegment: string;
}

/** Cấu hình View Lưới hồ sơ theo loại thiết bị — tab Báo cáo thống kê. */
export interface ReportStatisticsEquipmentTypeGridConfig {
  reportCode: string;
  gridSegment: string;
}

export const REPORT_STATISTICS_DOSSIER_LIST_CONFIGS = {
  DOSSIER_BY_YEAR: {
    reportCode: 'REPORT_DOSSIER_BY_YEAR',
    listSegment: 'dossier-by-year'
  },
  DOSSIER_BY_MONTH: {
    reportCode: 'REPORT_DOSSIER_BY_MONTH',
    listSegment: 'dossier-by-month'
  },
  DOSSIER_BY_VOLTAGE_GRID: {
    reportCode: 'REPORT_DOSSIER_BY_VOLTAGE_GRID',
    listSegment: 'dossier-by-voltage-grid'
  },
  DOSSIER_BY_EQUIPMENT_TYPE: {
    reportCode: 'REPORT_DOSSIER_BY_EQUIPMENT',
    listSegment: 'dossier-by-equipment-type'
  }
} as const satisfies Record<string, ReportStatisticsDossierListConfig>;

export const REPORT_STATISTICS_STATION_GRID_CONFIGS = {
  DOSSIER_BY_YEAR: {
    reportCode: 'REPORT_DOSSIER_BY_YEAR',
    gridSegment: 'dossier-by-year'
  },
  DOSSIER_BY_MONTH: {
    reportCode: 'REPORT_DOSSIER_BY_MONTH',
    gridSegment: 'dossier-by-month'
  },
  DOSSIER_BY_VOLTAGE_GRID: {
    reportCode: 'REPORT_DOSSIER_BY_VOLTAGE_GRID',
    gridSegment: 'dossier-by-voltage-grid'
  }
} as const satisfies Record<string, ReportStatisticsStationGridConfig>;

export const REPORT_STATISTICS_EQUIPMENT_TYPE_GRID_CONFIGS = {
  DOSSIER_BY_EQUIPMENT_TYPE: {
    reportCode: 'REPORT_DOSSIER_BY_EQUIPMENT',
    gridSegment: 'dossier-by-equipment-type'
  }
} as const satisfies Record<string, ReportStatisticsEquipmentTypeGridConfig>;
