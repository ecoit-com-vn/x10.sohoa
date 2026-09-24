/**
 * Tên trạm biến áp/đường dây mà thiết bị/tài liệu trực thuộc — đọc từ dataContent (JSON gốc PMIS trả
 * về, hoặc JSON do backend tự dựng cho dòng tài liệu — xem PmisSyncExecutionService.SyncDocumentsForOwnerAsync).
 * Backend serialize bằng PascalCase (TenTBA/TenDuongDay) nên tra cứu phải không phân biệt hoa/thường.
 * Dùng chung giữa pmis-manual-sync và pmis-schedule (trước đây 2 bản sao tay riêng, dễ lệch nhau khi
 * PMIS/backend đổi field — xem finding review 2026-09-23).
 */
export function getParentName(dataContent: string | null | undefined): string {
  if (!dataContent) return '---';
  try {
    const obj = JSON.parse(dataContent);
    const key = Object.keys(obj).find((k) => k.toLowerCase() === 'tentba' || k.toLowerCase() === 'tenduongday');
    return (key && obj[key]) || '---';
  } catch {
    return '---';
  }
}
