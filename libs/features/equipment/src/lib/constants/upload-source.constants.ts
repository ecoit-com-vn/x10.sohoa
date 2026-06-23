/** Nguồn upload tài liệu — đồng bộ với UPLOAD_SOURCE trên Oracle */
export const UPLOAD_SOURCE = {
  FOLDER: 1,
  SCAN: 2,
  WEB: 3,
} as const;

export type UploadSource = (typeof UPLOAD_SOURCE)[keyof typeof UPLOAD_SOURCE];
