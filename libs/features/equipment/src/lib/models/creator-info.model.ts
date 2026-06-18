/** Thông tin người dùng rút gọn để hiển thị trên danh sách. */
export interface CreatorInfo {
  id: string;
  username: string;
  name: string;
}

/** Hiển thị nhãn người tạo: ưu tiên creator, fallback createdBy. */
export function formatCreatorLabel(
  creator?: CreatorInfo | null,
  createdBy?: string | null
): string {
  if (creator?.name || creator?.username) {
    const displayName = creator.name || creator.username;
    return creator.username && creator.name
      ? `${creator.name} (${creator.username})`
      : displayName;
  }
  return createdBy?.trim() || 'System';
}
