import {
  DigitizationProcessOption,
  DocumentOcrProgress,
  DossierDocumentItem,
} from '../data-access/dossier-document.service';

export type OcrMode = 'none' | 'OcrAndExtract' | 'ExtractOnly';

const ACTIVE_STATUSES = new Set(['Pending', 'Running', 'OcrCompleted', 'Extracting']);

export function isActiveDigitizationStatus(status: string | undefined | null): boolean {
  return !!status && ACTIVE_STATUSES.has(status);
}

export function getDigitizationStatusLabel(status: string | undefined | null): string {
  switch (status) {
    case 'Pending':
      return 'Chờ xử lý';
    case 'Running':
      return 'Đang OCR';
    case 'OcrCompleted':
      return 'OCR xong';
    case 'Extracting':
      return 'Đang bóc tách';
    case 'Completed':
      return 'Hoàn tất';
    case 'Failed':
      return 'Lỗi';
    default:
      return status || '—';
  }
}

export function getExtractionStatusLabel(status: string | undefined | null): string {
  switch (status) {
    case 'Pending':
      return 'Chờ bóc tách';
    case 'Running':
    case 'Extracting':
      return 'Đang bóc tách';
    case 'Completed':
      return 'Hoàn tất';
    case 'Failed':
      return 'Lỗi';
    default:
      return status || '—';
  }
}

export function getDigitizationStatusClass(status: string | undefined | null): string {
  switch (status) {
    case 'Completed':
      return 'status-active';
    case 'Failed':
      return 'status-inactive';
    case 'Pending':
      return 'status-pending';
    default:
      return 'status-pending';
  }
}

export function formatDigitizationProgress(progress: DocumentOcrProgress | undefined): string {
  if (!progress) return '—';
  if (progress.totalPages > 0) {
    return `${progress.progress}% (${progress.currentPage}/${progress.totalPages} trang)`;
  }
  return `${progress.progress}%`;
}

/** Phần trăm thanh tiến độ OCR (0–100). */
export function getOcrBarPercent(ocr: DocumentOcrProgress | undefined | null): number | null {
  if (!ocr) return null;
  if (ocr.status === 'Failed' && (ocr.phase ?? 'ocr') === 'ocr') return 0;
  if (
    ocr.status === 'OcrCompleted' ||
    ocr.status === 'Extracting' ||
    ocr.status === 'Completed' ||
    (ocr.status === 'Failed' && ocr.phase === 'extraction')
  ) {
    return 100;
  }
  if (ocr.status === 'Pending' || ocr.status === 'Running') {
    return Math.min(100, Math.max(0, ocr.progress ?? 0));
  }
  return null;
}

export function getOcrColumnLabel(ocr: DocumentOcrProgress | undefined | null): string {
  if (!ocr) return '—';
  if (ocr.status === 'Failed' && (ocr.phase ?? 'ocr') === 'ocr') return 'Lỗi OCR';
  if (ocr.status === 'Pending') return 'Chờ OCR';
  if (ocr.status === 'Running') return 'Đang OCR';
  if (
    ocr.status === 'OcrCompleted' ||
    ocr.status === 'Extracting' ||
    ocr.status === 'Completed' ||
    (ocr.status === 'Failed' && ocr.phase === 'extraction')
  ) {
    return 'OCR xong';
  }
  return getDigitizationStatusLabel(ocr.status);
}

export function isOcrBarFailed(ocr: DocumentOcrProgress | undefined | null): boolean {
  return !!ocr && ocr.status === 'Failed' && (ocr.phase ?? 'ocr') === 'ocr';
}

export function isOcrBarActive(ocr: DocumentOcrProgress | undefined | null): boolean {
  return !!ocr && (ocr.status === 'Pending' || ocr.status === 'Running');
}

/** Cho phép mở màn sửa tài liệu (xem file + form). */
export function canEditDossierDocument(doc: {
  ocrProgress?: { status?: string } | null;
  extractionResult?: { status?: string } | null;
}): boolean {
  const ocr = doc.ocrProgress;
  const ext = doc.extractionResult;
  if (!ocr) return true;
  const status = ocr.status;
  if (status === 'OcrCompleted') return true;
  if (status === 'Completed' && ext?.status === 'Completed') return true;
  if (status === 'Failed' || ext?.status === 'Failed') return true;
  return false;
}

export function canRetryDigitization(doc: DossierDocumentItem): boolean {
  const ocr = doc.ocrProgress;
  const ext = doc.extractionResult;
  if (ocr?.status === 'Failed') return true;
  if (ext?.status === 'Failed') return true;
  return false;
}

export function isRetryingDigitization(docId: string, retryingIds: Set<string>): boolean {
  return retryingIds.has(docId);
}

/** Gợi ý tùy chọn xử lý lại: chỉ bóc tách nếu OCR đã xong nhưng bóc tách lỗi. */
export function getRetryProcessOption(doc: DossierDocumentItem): DigitizationProcessOption {
  const ocr = doc.ocrProgress;
  const ext = doc.extractionResult;
  if (ext?.status === 'Failed' && ocr && ocr.status !== 'Failed') {
    return 'ExtractOnly';
  }
  if (ocr?.status === 'Failed' && ocr.phase === 'extraction') {
    return 'ExtractOnly';
  }
  return 'OcrAndExtract';
}

export function getExtractionColumnState(doc: DossierDocumentItem): {
  label: string;
  statusClass: string;
  showProgress: boolean;
} {
  const ocr = doc.ocrProgress;
  const ext = doc.extractionResult;

  if (!ocr && !ext) {
    return { label: '—', statusClass: '', showProgress: false };
  }

  if (ocr?.status === 'Extracting') {
    return { label: 'Đang bóc tách', statusClass: 'status-pending', showProgress: true };
  }

  if (ext?.status) {
    return {
      label: getExtractionStatusLabel(ext.status),
      statusClass: getDigitizationStatusClass(ext.status === 'Failed' ? 'Failed' : ext.status === 'Completed' ? 'Completed' : 'Pending'),
      showProgress: ext.status !== 'Completed' && ext.status !== 'Failed',
    };
  }

  if (ocr?.status === 'OcrCompleted') {
    return { label: 'Chờ bóc tách', statusClass: 'status-pending', showProgress: true };
  }

  if (ocr?.status === 'Completed') {
    return { label: 'Hoàn tất', statusClass: 'status-active', showProgress: false };
  }

  if (ocr?.status === 'Failed' && ocr.phase === 'extraction') {
    return { label: 'Lỗi', statusClass: 'status-inactive', showProgress: false };
  }

  return { label: '—', statusClass: '', showProgress: false };
}
