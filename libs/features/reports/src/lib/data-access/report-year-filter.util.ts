export const ALL_YEARS_VALUE = 0;

export interface YearOption {
  label: string;
  value: number;
}

export function buildYearOptions(years: number[]): YearOption[] {
  return [
    { label: 'Tất cả các năm', value: ALL_YEARS_VALUE },
    ...years.map((year) => ({ label: String(year), value: year }))
  ];
}

/** Chỉ gửi `year` lên API khi chọn năm cụ thể (> 0). */
export function yearToFilterParam(year: number): { year?: number } {
  return year > 0 ? { year } : {};
}

export function buildExportYearSuffix(year: number): string {
  return year > 0 ? `Nam_${year}` : 'TatCaCacNam';
}
