export function removeAscent(str: string): string {
  if (!str) return '';
  return str
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D');
}

export function getErrorMessage(errors: any, label: string): { key: string; required?: any } {
  if (!errors) return { key: '' };

  if (errors.required) {
    return { key: `${label} không được để trống`, required: {} };
  }
  if (errors.maxlength) {
    return { key: `${label} nhập tối đa ${errors.maxlength.requiredLength} ký tự`, required: { max: errors.maxlength.requiredLength } };
  }
  if (errors.pattern) {
    return { key: `${label} không đúng định dạng`, required: {} };
  }
  
  const firstKey = Object.keys(errors)[0];
  return { key: `${label} không hợp lệ`, required: errors[firstKey] };
}

export function isNotEmpty(value: any): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === 'string' && value.trim() === '') return false;
  if (Array.isArray(value) && value.length === 0) return false;
  return true;
}

export function isEmpty(value: any): boolean {
  return !isNotEmpty(value);
}
