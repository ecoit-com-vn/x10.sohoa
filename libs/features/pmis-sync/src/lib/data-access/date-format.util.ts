/**
 * Backend trả timestamp dạng ISO KHÔNG có hậu tố 'Z'/offset (Dapper đọc Oracle TIMESTAMP luôn ra
 * DateTimeKind.Unspecified nên System.Text.Json không tự thêm 'Z' khi serialize), dù giá trị THẬT là
 * UTC (SYSTIMESTAMP trên server chạy UTC). Nếu truyền thẳng chuỗi đó vào `new Date(...)`, trình duyệt
 * hiểu nhầm là giờ local của máy người dùng (đúng theo chuẩn ISO 8601: chuỗi không có timezone designator
 * = local time) — kết quả là hiển thị y hệt số giờ UTC thay vì quy đổi đúng sang giờ Việt Nam (UTC+7),
 * lệch 7 tiếng so với giờ thật. Hàm này tự thêm 'Z' nếu chuỗi chưa có timezone designator, ép trình
 * duyệt hiểu đúng đây là UTC trước khi quy đổi sang giờ local để hiển thị.
 */
export function formatUtcDate(value: string | null | undefined): string {
  if (!value) return '---';
  const hasTimezone = /[Zz]|[+-]\d{2}:?\d{2}$/.test(value);
  const date = new Date(hasTimezone ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('vi-VN');
}
