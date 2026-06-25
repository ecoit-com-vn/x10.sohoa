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

export interface NormalizedDossierDetail {
  id: string;
  dossierTypeId: string;
  dossierTypeName: string;
  formId: string | null;
  formDataJson: string | null;
  infrastructureName: string;
  infrastructureId: string | null;
  gridTypeId: number | null;
  status: string;
  rowVersion: number;
  workflowInstanceId: string | null;
  raw: Record<string, unknown>;
}

/** Đọc field từ response API (camelCase hoặc PascalCase). */
export function readApiField<T>(obj: Record<string, unknown> | null | undefined, ...keys: string[]): T | undefined {
  if (!obj) return undefined;
  for (const key of keys) {
    if (obj[key] !== undefined && obj[key] !== null && obj[key] !== '') {
      return obj[key] as T;
    }
  }
  return undefined;
}

export function normalizeGuid(value: unknown): string {
  return value == null ? '' : String(value).trim().toLowerCase();
}

export function guidsEqual(a: unknown, b: unknown): boolean {
  const sa = normalizeGuid(a);
  const sb = normalizeGuid(b);
  return !!sa && !!sb && sa === sb;
}

/** Chuẩn hoá response GET /dossiers/{id}. */
export function normalizeDossierDetail(raw: unknown): NormalizedDossierDetail | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const id = readApiField<string>(o, 'id', 'Id');
  if (!id) return null;

  return {
    id: String(id),
    dossierTypeId: String(readApiField<string>(o, 'dossierTypeId', 'DossierTypeId') ?? ''),
    dossierTypeName: String(readApiField<string>(o, 'dossierTypeName', 'DossierTypeName') ?? ''),
    formId: readApiField<string>(o, 'formId', 'FormId') ?? null,
    formDataJson: readApiField<string>(o, 'formDataJson', 'FormDataJson') ?? null,
    infrastructureName: String(readApiField<string>(o, 'infrastructureName', 'InfrastructureName') ?? ''),
    infrastructureId: readApiField<string>(o, 'infrastructureId', 'InfrastructureId') ?? null,
    gridTypeId: readApiField<number>(o, 'gridTypeId', 'GridTypeId') ?? null,
    status: String(readApiField<string>(o, 'status', 'Status') ?? ''),
    rowVersion: Number(readApiField<number>(o, 'rowVersion', 'RowVersion') ?? 0),
    workflowInstanceId: readApiField<string>(o, 'workflowInstanceId', 'WorkflowInstanceId') ?? null,
    raw: o,
  };
}

export function parseFormDataJson(raw: unknown): Record<string, unknown> {
  if (!raw) return {};
  if (typeof raw === 'object' && !Array.isArray(raw)) {
    return raw as Record<string, unknown>;
  }
  if (typeof raw !== 'string' || !raw.trim()) return {};
  try {
    const parsed = JSON.parse(raw) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
}

export function readFormSchemaJson(template: unknown): string | null {
  if (!template || typeof template !== 'object') return null;
  const t = template as Record<string, unknown>;
  const schema = readApiField<string>(t, 'formSchema', 'FormSchema');
  return schema?.trim() ? schema : null;
}

export function normalizeField(raw: Record<string, unknown>): EavField {
  const key =
    firstNonEmptyString(raw['name'], raw['Name'], raw['key'], raw['Key'], raw['id'], raw['Id']) ?? '';
  return {
    ...(raw as unknown as EavField),
    key,
  };
}

function firstNonEmptyString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (value === undefined || value === null) continue;
    const text = String(value).trim();
    if (text) return text;
  }
  return undefined;
}

/** Chỉ giữ key có trong schema — fallback name/id nếu JSON cũ dùng alias. */
export function pickFormDataForSchema(
  fields: ReadonlyArray<Pick<EavField, 'key' | 'name' | 'id'>>,
  data: Record<string, unknown> | null | undefined
): Record<string, unknown> {
  const source = data ?? {};
  const result: Record<string, unknown> = {};

  for (const field of fields) {
    const key = field.key?.trim();
    if (!key) continue;

    const aliases = [key, field.name, field.id].filter(Boolean) as string[];
    for (const alias of aliases) {
      if (Object.prototype.hasOwnProperty.call(source, alias)) {
        result[key] = source[alias];
        break;
      }
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

/** Parse FormSchema JSON → danh sách field EAV. */
export function parseFormSchemaFields(schemaJson: string | null | undefined): EavField[] {
  if (!schemaJson?.trim()) return [];
  try {
    const raw = JSON.parse(schemaJson) as unknown;
    if (Array.isArray(raw)) {
      return raw.map((f) => normalizeField(f as Record<string, unknown>)).filter((f) => !!f.key?.trim());
    }
    if (raw && typeof raw === 'object' && Array.isArray((raw as { fields?: unknown[] }).fields)) {
      return ((raw as { fields: Record<string, unknown>[] }).fields)
        .map((f) => normalizeField(f))
        .filter((f) => !!f.key?.trim());
    }
    return [];
  } catch {
    return [];
  }
}

/** Gộp mảng { page, data } → object flat (fallback nếu BE chưa có mergedDataJson). */
export function mergeExtractionPageResults(resultJson: string | null | undefined): Record<string, unknown> {
  if (!resultJson?.trim()) return {};
  try {
    const pages = JSON.parse(resultJson) as unknown;
    if (!Array.isArray(pages)) return {};

    const merged: Record<string, unknown> = {};
    for (const page of pages) {
      if (!page || typeof page !== 'object') continue;
      const data = (page as { data?: Record<string, unknown> }).data ?? page;
      if (!data || typeof data !== 'object' || Array.isArray(data)) continue;

      for (const [key, value] of Object.entries(data as Record<string, unknown>)) {
        if (value === null || value === undefined) continue;
        if (typeof value === 'string' && !value.trim()) continue;
        merged[key] = value;
      }
    }
    return merged;
  } catch {
    return {};
  }
}

export function parseMergedDataJson(raw: string | null | undefined): Record<string, unknown> {
  if (!raw?.trim()) return {};
  try {
    const parsed = JSON.parse(raw) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
}

export type ApplyExtractionMode = 'fillEmpty' | 'overwrite';

/** Áp dụng dữ liệu bóc tách đã chọn vào formData hiện tại. */
export function applyExtractionToFormData(
  fields: ReadonlyArray<Pick<EavField, 'key'>>,
  currentData: Record<string, unknown>,
  proposedData: Record<string, unknown>,
  selectedKeys: ReadonlySet<string>,
  mode: ApplyExtractionMode
): Record<string, unknown> {
  const result = { ...currentData };

  for (const field of fields) {
    const key = field.key?.trim();
    if (!key || !selectedKeys.has(key)) continue;
    if (!(key in proposedData)) continue;

    const proposed = proposedData[key];
    const current = result[key];
    const currentEmpty = current === null || current === undefined || current === '';

    if (mode === 'overwrite' || currentEmpty) {
      result[key] = proposed;
    }
  }

  return result;
}

export function displayFieldValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'boolean') return value ? 'Có' : 'Không';
  return String(value);
}

/** Hiển thị giá trị trường EAV — màn xem read-only (tên trường : giá trị). */
export function formatFieldDisplayValue(field: EavField, value: unknown): string {
  if (value === null || value === undefined || value === '') return '—';

  if (field.type === 'checkbox') {
    return value === true || value === 'true' || value === 1 || value === '1' ? 'Có' : 'Không';
  }

  if (field.type === 'select') {
    const str = String(value);
    const opt = field.options?.find((o) => o.value === str);
    return opt?.label ?? str;
  }

  if (field.type === 'date') {
    const text = String(value);
    const d = new Date(text);
    if (!Number.isNaN(d.getTime())) {
      return d.toLocaleDateString('vi-VN');
    }
    return text;
  }

  if (field.type === 'number' && field.unit) {
    return `${value} ${field.unit}`;
  }

  return displayFieldValue(value);
}

export function hasExtractedValue(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === 'string') return !!value.trim();
  return true;
}
