/** Field trong FormSchema EAV — key/name/id chuẩn hoá về `key`. */
export interface EavField {
  key: string;
  name?: string;
  id?: string;
  label: string;
  type: 'text' | 'number' | 'date' | 'textarea' | 'select' | 'checkbox';
  required?: boolean;
  placeholder?: string;
  options?: { label: string; value: string }[];
  unit?: string;
}

export function normalizeField(raw: Record<string, unknown>): EavField {
  return {
    ...(raw as unknown as EavField),
    key: String(raw['key'] ?? raw['name'] ?? raw['id'] ?? ''),
  };
}

/** Chỉ giữ key có trong schema hiện tại — loại orphan key (vd. truong_van_ban_* cũ). */
export function pickFormDataForSchema(
  fields: ReadonlyArray<Pick<EavField, 'key'>>,
  data: Record<string, unknown> | null | undefined
): Record<string, unknown> {
  const source = data ?? {};
  const result: Record<string, unknown> = {};

  for (const field of fields) {
    const key = field.key?.trim();
    if (!key) continue;
    if (Object.prototype.hasOwnProperty.call(source, key)) {
      result[key] = source[key];
    }
  }

  return result;
}

export function serializeFormDataForSchema(
  fields: ReadonlyArray<Pick<EavField, 'key'>>,
  data: Record<string, unknown> | null | undefined
): string {
  return JSON.stringify(pickFormDataForSchema(fields, data));
}
