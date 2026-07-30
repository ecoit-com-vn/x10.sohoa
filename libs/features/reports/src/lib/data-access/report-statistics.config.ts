/** Cấu hình tab Danh sách hồ sơ — mỗi báo cáo thống kê thêm một entry. */
export interface ReportStatisticsDossierListConfig {
  reportCode: string;
  listSegment: string;
  columnMode?: 'dynamic-bhs' | 'fixed-dossier';
  title?: string;
}

/** Cấu hình tab Danh sách tài liệu — báo cáo thống kê theo loại văn bản. */
export interface ReportStatisticsDocumentListConfig {
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

/** Cấu hình View Lưới hồ sơ theo thiết bị (từng thiết bị cụ thể) — tab Báo cáo thống kê. */
export interface ReportStatisticsEquipmentGridConfig {
  reportCode: string;
  gridSegment: string;
}

/** Cấu hình View Lưới hồ sơ theo thiết bị (kèm tình trạng thiết bị) — tab Báo cáo thống kê. */
export interface ReportStatisticsEquipmentStatusGridConfig {
  reportCode: string;
  gridSegment: string;
}

/** Cấu hình View Lưới hồ sơ được tra cứu nhiều nhất — kèm số lượt tra cứu. */
export interface ReportStatisticsDossierViewGridConfig {
  reportCode: string;
  gridSegment: string;
}

/** Cấu hình View Lưới hồ sơ theo loại hồ sơ — tab Báo cáo thống kê. */
export interface ReportStatisticsDossierTypeGridConfig {
  reportCode: string;
  gridSegment: string;
}

/** Cấu hình View Lưới hồ sơ theo loại văn bản — tab Báo cáo thống kê. */
export interface ReportStatisticsDocumentTypeGridConfig {
  reportCode: string;
  gridSegment: string;
}

/** Cấu hình View Lưới hồ sơ theo kệ lưu trữ — tab Báo cáo thống kê. */
export interface ReportStatisticsShelfGridConfig {
  reportCode: string;
  gridSegment: string;
}

/** Cấu hình View Lưới hồ sơ theo hộp lưu trữ — tab Báo cáo thống kê. */
export interface ReportStatisticsBoxGridConfig {
  reportCode: string;
  gridSegment: string;
}

/** Cấu hình View Lưới hồ sơ theo tầng lưu trữ — tab Báo cáo thống kê. */
export interface ReportStatisticsFloorGridConfig {
  reportCode: string;
  gridSegment: string;
}

/** Cấu hình View Lưới hồ sơ theo người tạo — tab Báo cáo thống kê (phân bổ). */
export interface ReportStatisticsCreatorGridConfig {
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
  },
  DOSSIER_BY_ALLOCATION: {
    reportCode: 'REPORT_DOSSIER_BY_ALLOCATION',
    listSegment: 'dossier-by-allocation'
  },
  DOSSIER_BY_DOSSIER_TYPE: {
    reportCode: 'REPORT_DOSSIER_BY_DOSSIER_TYPE',
    listSegment: 'dossier-by-dossier-type'
  },
  DOSSIER_BY_SHELF: {
    reportCode: 'REPORT_DOSSIER_BY_SHELF',
    listSegment: 'dossier-by-shelf'
  },
  DOSSIER_BY_BOX: {
    reportCode: 'REPORT_DOSSIER_BY_BOX',
    listSegment: 'dossier-by-box'
  },
  DOSSIER_BY_FLOOR: {
    reportCode: 'REPORT_DOSSIER_BY_FLOOR',
    listSegment: 'dossier-by-floor'
  },
  DOSSIER_BY_STATION: {
    reportCode: 'REPORT_DOSSIER_BY_STATION',
    listSegment: 'dossier-by-station'
  },
  DOSSIER_BY_LINE: {
    reportCode: 'REPORT_DOSSIER_BY_LINE',
    listSegment: 'dossier-by-line'
  },
  DOSSIER_BY_OPERATION_YEAR: {
    reportCode: 'REPORT_DOSSIER_BY_OPERATION_YEAR',
    listSegment: 'dossier-by-operation-year'
  },
  DOSSIER_BY_OPERATION_TIME: {
    reportCode: 'REPORT_DOSSIER_BY_OPERATION_TIME',
    listSegment: 'dossier-by-operation-time'
  },
  DOSSIER_BY_MANUFACTURE_YEAR: {
    reportCode: 'REPORT_DOSSIER_BY_MANUFACTURE_YEAR',
    listSegment: 'dossier-by-manufacture-year'
  },
  DOSSIER_BY_EQUIPMENT_STATUS: {
    reportCode: 'REPORT_DOSSIER_BY_EQUIPMENT_STATUS',
    listSegment: 'dossier-by-equipment-status'
  },
  DOSSIER_GENERAL_INPUT: {
    reportCode: 'REPORT_DOSSIER_GENERAL_INPUT',
    listSegment: 'dossier-general-input'
  },
  DOSSIER_BY_INPUT_OFFICER: {
    reportCode: 'REPORT_DOSSIER_BY_INPUT_OFFICER',
    listSegment: 'dossier-by-input-officer',
    columnMode: 'fixed-dossier',
    title: 'Danh sách hồ sơ'
  }
} as const satisfies Record<string, ReportStatisticsDossierListConfig>;

export const REPORT_STATISTICS_DOCUMENT_LIST_CONFIGS = {
  DOSSIER_BY_DOCUMENT_TYPE: {
    reportCode: 'REPORT_DOSSIER_BY_DOCUMENT_TYPE',
    listSegment: 'dossier-by-document-type'
  }
} as const satisfies Record<string, ReportStatisticsDocumentListConfig>;

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
  },
  DOSSIER_BY_STATION: {
    reportCode: 'REPORT_DOSSIER_BY_STATION',
    gridSegment: 'dossier-by-station'
  },
  DOSSIER_BY_LINE: {
    reportCode: 'REPORT_DOSSIER_BY_LINE',
    gridSegment: 'dossier-by-line'
  },
  DOSSIER_BY_OPERATION_YEAR: {
    reportCode: 'REPORT_DOSSIER_BY_OPERATION_YEAR',
    gridSegment: 'dossier-by-operation-year'
  },
  DOSSIER_BY_OPERATION_TIME: {
    reportCode: 'REPORT_DOSSIER_BY_OPERATION_TIME',
    gridSegment: 'dossier-by-operation-time'
  },
  DOSSIER_GENERAL_INPUT: {
    reportCode: 'REPORT_DOSSIER_GENERAL_INPUT',
    gridSegment: 'dossier-general-input'
  }
} as const satisfies Record<string, ReportStatisticsStationGridConfig>;

export const REPORT_STATISTICS_CREATOR_GRID_CONFIGS = {
  DOSSIER_BY_ALLOCATION: {
    reportCode: 'REPORT_DOSSIER_BY_ALLOCATION',
    gridSegment: 'dossier-by-allocation'
  },
  DOSSIER_BY_INPUT_OFFICER: {
    reportCode: 'REPORT_DOSSIER_BY_INPUT_OFFICER',
    gridSegment: 'dossier-by-input-officer'
  }
} as const satisfies Record<string, ReportStatisticsCreatorGridConfig>;

export const REPORT_STATISTICS_EQUIPMENT_TYPE_GRID_CONFIGS = {
  DOSSIER_BY_EQUIPMENT_TYPE: {
    reportCode: 'REPORT_DOSSIER_BY_EQUIPMENT',
    gridSegment: 'dossier-by-equipment-type'
  }
} as const satisfies Record<string, ReportStatisticsEquipmentTypeGridConfig>;

export const REPORT_STATISTICS_EQUIPMENT_GRID_CONFIGS = {
  DOSSIER_BY_MANUFACTURE_YEAR: {
    reportCode: 'REPORT_DOSSIER_BY_MANUFACTURE_YEAR',
    gridSegment: 'dossier-by-manufacture-year'
  }
} as const satisfies Record<string, ReportStatisticsEquipmentGridConfig>;

export const REPORT_STATISTICS_EQUIPMENT_STATUS_GRID_CONFIGS = {
  DOSSIER_BY_EQUIPMENT_STATUS: {
    reportCode: 'REPORT_DOSSIER_BY_EQUIPMENT_STATUS',
    gridSegment: 'dossier-by-equipment-status'
  }
} as const satisfies Record<string, ReportStatisticsEquipmentStatusGridConfig>;

export const REPORT_STATISTICS_DOSSIER_VIEW_GRID_CONFIGS = {
  DOSSIER_MOST_VIEWED: {
    reportCode: 'REPORT_DOSSIER_MOST_VIEWED',
    gridSegment: 'dossier-most-viewed'
  }
} as const satisfies Record<string, ReportStatisticsDossierViewGridConfig>;

export const REPORT_STATISTICS_DOSSIER_TYPE_GRID_CONFIGS = {
  DOSSIER_BY_DOSSIER_TYPE: {
    reportCode: 'REPORT_DOSSIER_BY_DOSSIER_TYPE',
    gridSegment: 'dossier-by-dossier-type'
  }
} as const satisfies Record<string, ReportStatisticsDossierTypeGridConfig>;

export const REPORT_STATISTICS_DOCUMENT_TYPE_GRID_CONFIGS = {
  DOSSIER_BY_DOCUMENT_TYPE: {
    reportCode: 'REPORT_DOSSIER_BY_DOCUMENT_TYPE',
    gridSegment: 'dossier-by-document-type'
  }
} as const satisfies Record<string, ReportStatisticsDocumentTypeGridConfig>;

export const REPORT_STATISTICS_SHELF_GRID_CONFIGS = {
  DOSSIER_BY_SHELF: {
    reportCode: 'REPORT_DOSSIER_BY_SHELF',
    gridSegment: 'dossier-by-shelf'
  }
} as const satisfies Record<string, ReportStatisticsShelfGridConfig>;

export const REPORT_STATISTICS_BOX_GRID_CONFIGS = {
  DOSSIER_BY_BOX: {
    reportCode: 'REPORT_DOSSIER_BY_BOX',
    gridSegment: 'dossier-by-box'
  }
} as const satisfies Record<string, ReportStatisticsBoxGridConfig>;

export const REPORT_STATISTICS_FLOOR_GRID_CONFIGS = {
  DOSSIER_BY_FLOOR: {
    reportCode: 'REPORT_DOSSIER_BY_FLOOR',
    gridSegment: 'dossier-by-floor'
  }
} as const satisfies Record<string, ReportStatisticsFloorGridConfig>;
