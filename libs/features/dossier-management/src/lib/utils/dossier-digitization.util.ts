import {
  DigitizationProcessOption,
  DocumentOcrProgress,
  DossierDocumentItem,
} from '../data-access/dossier-document.service';

export type OcrMode = 'none' | 'OcrAndExtract' | 'ExtractOnly';

const ACTIVE_STATUSES = new Set(['Pending', 'Running', 'OcrCompleted', 'Extracting']);
/** Đang chạy worker — chặn gửi job mới. Không gồm OcrCompleted/Completed (OCR đã xong vẫn cho chạy lại). */
const IN_PROGRESS_STATUSES = new Set(['Pending', 'Running', 'Extracting']);

export function isActiveDigitizationStatus(status: string | undefined | null): boolean {
  return !!status && ACTIVE_STATUSES.has(status);
}

export function isDigitizationInProgress(status: string | undefined | null): boolean {
  return !!status && IN_PROGRESS_STATUSES.has(status);
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

/** Phần trăm thanh tiến độ OCR (0–100). Chỉ tính khi phase = ocr. */
export function getOcrBarPercent(ocr: DocumentOcrProgress | undefined | null): number | null {
  if (!ocr) return null;
  if (isOcrComplete(ocr)) return null;
  if (ocr.status === 'Failed' && (ocr.phase ?? 'ocr') === 'ocr') return 0;
  if (ocr.status === 'Pending' || ocr.status === 'Running') {
    if ((ocr.phase ?? 'ocr') !== 'ocr') return null;
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

export function isOcrComplete(ocr: DocumentOcrProgress | undefined | null): boolean {
  if (!ocr) return false;
  if (ocr.status === 'Failed' && (ocr.phase ?? 'ocr') === 'ocr') return false;
  return (
    ocr.status === 'OcrCompleted' ||
    ocr.status === 'Extracting' ||
    ocr.status === 'Completed' ||
    (ocr.status === 'Failed' && ocr.phase === 'extraction')
  );
}

export function isExtractionComplete(doc: DossierDocumentItem): boolean {
  const ext = doc.extractionResult;
  const ocr = doc.ocrProgress;
  return ext?.status === 'Completed' || ocr?.status === 'Completed';
}

export function isExtractionFailed(doc: DossierDocumentItem): boolean {
  const ext = doc.extractionResult;
  const ocr = doc.ocrProgress;
  return ext?.status === 'Failed' || (!!ocr && ocr.status === 'Failed' && ocr.phase === 'extraction');
}

/** Phần trăm thanh tiến độ bóc tách (0–100); null nếu chưa bắt đầu hoặc đã kết thúc. */
export function getExtractionBarPercent(doc: DossierDocumentItem): number | null {
  const ocr = doc.ocrProgress;
  const ext = doc.extractionResult;

  if (!ocr && !ext) return null;
  if (isExtractionComplete(doc) || isExtractionFailed(doc)) return null;

  // OCR chưa xong — cột bóc tách không chạy % (tránh mirror ocr.progress khi ext = Pending)
  if (ocr && !isOcrComplete(ocr)) return null;

  // OCR xong, chờ worker bóc tách
  if (ocr?.status === 'OcrCompleted' || (ext?.status === 'Pending' && (ocr?.phase ?? 'ocr') !== 'extraction')) {
    return 0;
  }

  // Đang bóc tách — progress chỉ có ý nghĩa ở phase extraction
  if (ocr?.status === 'Extracting' || ocr?.phase === 'extraction') {
    return Math.min(100, Math.max(0, ocr.progress ?? 0));
  }

  if (ext?.status === 'Running' || ext?.status === 'Extracting') {
    return Math.min(100, Math.max(0, ocr?.progress ?? 0));
  }

  return null;
}

export function shouldShowExtractionProgress(doc: DossierDocumentItem): boolean {
  if (isExtractionComplete(doc) || isExtractionFailed(doc)) return true;
  const ocr = doc.ocrProgress;
  if (!ocr) return !!doc.extractionResult;
  return isOcrComplete(ocr);
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
  if (isActiveDigitizationStatus(ocr?.status)) return false;
  if (ocr?.status === 'Failed' && (ocr.phase ?? 'ocr') === 'ocr') return true;
  if (ext?.status === 'Failed' && ocr?.status !== 'Failed') return false;
  if (ext?.status === 'Failed') return true;
  return false;
}

/** Cho phép gửi OCR + bóc tách (lần đầu hoặc chạy lại) — kể cả khi OCR đã hoàn tất trước đó. */
export function canSubmitOcrAndExtract(doc: DossierDocumentItem): boolean {
  if (!doc.latestVersionId) return false;
  return !isDigitizationInProgress(doc.ocrProgress?.status);
}

/** Cho phép bóc tách lại (OCR đã xong, không đang xử lý). Form EAV được load mới trên server. */
export function canReExtract(doc: DossierDocumentItem): boolean {
  const ocr = doc.ocrProgress;
  if (!ocr) return false;
  if (isDigitizationInProgress(ocr.status)) return false;
  if (ocr.status === 'Failed' && (ocr.phase ?? 'ocr') === 'ocr') return false;
  return (
    ocr.status === 'OcrCompleted' ||
    ocr.status === 'Completed' ||
    (ocr.status === 'Failed' && ocr.phase === 'extraction') ||
    doc.extractionResult?.status === 'Completed' ||
    doc.extractionResult?.status === 'Failed'
  );
}

export function isReExtracting(docId: string, reExtractingIds: Set<string>): boolean {
  return reExtractingIds.has(docId);
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
