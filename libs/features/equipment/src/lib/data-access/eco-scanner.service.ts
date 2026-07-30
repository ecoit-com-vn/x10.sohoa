import { Injectable, inject } from '@angular/core';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

export type ScanFormat = 'pdf' | 'png' | 'jpg' | 'jpeg' | 'bmp' | 'gif' | 'tiff';
export type ScanMode = 's' | 'm';

export interface ScanOptions {
  format?: ScanFormat;
  mode?: ScanMode;
  wsUrl?: string;
  timeoutMs?: number;
  fileName?: string;
}

export type ScanPhase = 'idle' | 'connecting' | 'scanning' | 'receiving' | 'done' | 'error';

export interface ScanProgress {
  phase: ScanPhase;
  message?: string;
}

const CLIENT_BUSY_MESSAGE = 'Client khác đang kết nối';
const NO_DATA_MESSAGE = 'Không có dữ liệu';
const SESSION_BUSY_PREFIX = 'Định dạng file đã được cấu hình';
const UNSUPPORTED_FORMAT_PREFIX = 'Chưa hỗ trợ định dạng';
const NO_ACTION_PREFIX = 'No action for';

/** Thời gian chờ tối thiểu khi quét — không được đặt dưới 120 giây (docs/WEB_PROTOCOL.md §7). */
const MIN_SCAN_TIMEOUT_MS = 120_000;

@Injectable({
  providedIn: 'root',
})
export class EcoScannerService {
  private config = inject(APP_CONFIG);

  private activeSocket: WebSocket | null = null;
  private cancelRequested = false;

  /**
   * Quét tài liệu qua EcoScanner (WebSocket localhost).
   * Giao thức: docs/WEB_PROTOCOL.md — mode `s`: Binary → Text "1" → Close.
   */
  scan(options?: ScanOptions, onProgress?: (progress: ScanProgress) => void): Promise<File[]> {
    const format = options?.format ?? 'pdf';
    const mode = options?.mode ?? 's';
    const wsUrl = options?.wsUrl ?? this.config.ecoScannerWsUrl ?? 'ws://127.0.0.1:8282';
    const timeoutMs = Math.max(options?.timeoutMs ?? MIN_SCAN_TIMEOUT_MS, MIN_SCAN_TIMEOUT_MS);
    const debug = this.config.ecoScannerDebug ?? !this.config.production;

    this.cancelRequested = false;

    return new Promise((resolve, reject) => {
      const files: File[] = [];
      let settled = false;
      let scanDone = false;
      let scanFailed = false;
      let pendingBinaryReads = 0;
      let expectedPageCount: number | null = null;

      const log = (message: string, detail?: unknown) => {
        if (debug) {
          console.debug(`[EcoScanner] ${message}`, detail ?? '');
        }
      };

      const emit = (progress: ScanProgress) => onProgress?.(progress);

      const cleanup = (socket: WebSocket) => {
        clearTimeout(timeoutId);
        if (this.activeSocket === socket) {
          this.activeSocket = null;
        }
      };

      const fail = (socket: WebSocket, message: string, closeSocket = false) => {
        if (settled) return;
        settled = true;
        scanFailed = true;
        cleanup(socket);
        if (
          closeSocket &&
          (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)
        ) {
          socket.close();
        }
        log('FAIL', message);
        emit({ phase: 'error', message });
        reject(new Error(message));
      };

      const succeed = (socket: WebSocket) => {
        if (settled) return;
        settled = true;
        scanDone = true;
        cleanup(socket);
        log('OK', { fileCount: files.length, sizes: files.map((f) => f.size) });
        emit({ phase: 'done', message: 'Quét thành công' });
        resolve([...files]);
      };

      const tryComplete = (socket: WebSocket) => {
        if (settled || pendingBinaryReads > 0 || files.length === 0) return;

        if (mode === 's' && scanDone) {
          succeed(socket);
          return;
        }

        if (
          mode === 'm' &&
          scanDone &&
          expectedPageCount !== null &&
          files.length >= expectedPageCount
        ) {
          succeed(socket);
        }
      };

      const onBinaryReceived = (socket: WebSocket, data: ArrayBuffer) => {
        if (settled) return;
        if (data.byteLength === 0) {
          log('Binary frame rỗng (0 byte) — bỏ qua');
          return;
        }

        log('Binary frame', { bytes: data.byteLength });
        emit({ phase: 'receiving', message: 'Đang nhận file quét...' });
        files.push(this.bufferToFile(data, format, files.length + 1, options?.fileName));
        tryComplete(socket);
      };

      const tryParseMisframedPdf = (socket: WebSocket, raw: string): boolean => {
        if (format !== 'pdf' || !raw.startsWith('%PDF')) return false;
        log('Text frame chứa PDF (frame sai loại) — thử parse');
        const encoder = new TextEncoder();
        onBinaryReceived(socket, encoder.encode(raw).buffer);
        return true;
      };

      const handleTextMessage = (socket: WebSocket, raw: string) => {
        const text = raw.trim();
        if (!text) return;

        log('Text frame', text);

        if (tryParseMisframedPdf(socket, raw)) {
          return;
        }

        // Giao thức mở rộng (tùy chọn — docs/WEB_PROTOCOL.md §11)
        if (text.startsWith('status:')) {
          const statusMessage = text.slice('status:'.length).trim() || text;
          emit({ phase: 'scanning', message: statusMessage });
          return;
        }

        if (text.startsWith('filesize:')) {
          const sizeHint = text.slice('filesize:'.length).trim();
          emit({
            phase: 'receiving',
            message: sizeHint
              ? `Đang chờ nhận file quét (${sizeHint} byte)...`
              : 'Đang chờ nhận file quét...',
          });
          return;
        }

        if (text.startsWith('error:')) {
          fail(socket, text.slice('error:'.length).trim() || text, true);
          return;
        }

        if (text.includes(CLIENT_BUSY_MESSAGE)) {
          fail(
            socket,
            `${CLIENT_BUSY_MESSAGE}. Vui lòng đóng tab hoặc ứng dụng khác đang dùng máy quét.`,
            true
          );
          return;
        }

        if (text.startsWith(SESSION_BUSY_PREFIX)) {
          emit({
            phase: 'scanning',
            message: 'Phiên quét trước chưa kết thúc — vui lòng đợi hoặc khởi động lại EcoScanner.',
          });
          return;
        }

        if (text.startsWith(UNSUPPORTED_FORMAT_PREFIX)) {
          fail(socket, text, true);
          return;
        }

        if (text.startsWith(NO_ACTION_PREFIX)) {
          fail(socket, `Lệnh quét không hợp lệ: ${text}`, true);
          return;
        }

        if (text === NO_DATA_MESSAGE) {
          if (this.cancelRequested) {
            fail(socket, 'Đã hủy quét tài liệu', true);
            return;
          }
          fail(
            socket,
            'Không có dữ liệu quét. Hãy hoàn tất thao tác quét trên cửa sổ EcoScanner.',
            true
          );
          return;
        }

        if (text === '1' && mode === 's') {
          scanDone = true;
          emit({ phase: 'receiving', message: 'EcoScanner báo đã gửi file — đang nhận dữ liệu...' });
          tryComplete(socket);
          return;
        }

        if (mode === 'm' && /^\d+$/.test(text)) {
          scanDone = true;
          expectedPageCount = Number.parseInt(text, 10);
          emit({
            phase: 'receiving',
            message: `EcoScanner báo đã gửi ${expectedPageCount} file — đang nhận dữ liệu...`,
          });
          tryComplete(socket);
          return;
        }

        // Các message khác — chỉ log / hiện trạng thái, KHÔNG báo lỗi (docs/WEB_PROTOCOL.md §4)
        emit({ phase: 'scanning', message: text });
      };

      emit({ phase: 'connecting', message: 'Đang kết nối tới dịch vụ scan...' });
      log('Connecting', wsUrl);

      const socket = new WebSocket(wsUrl);
      this.activeSocket = socket;
      // BẮT BUỘC — docs/WEB_PROTOCOL.md §1
      socket.binaryType = 'arraybuffer';

      const timeoutId = setTimeout(() => {
        if (!scanDone && files.length === 0) {
          fail(socket, 'Hết thời gian chờ quét (120s). Vui lòng thử lại.', true);
        }
      }, timeoutMs);

      socket.onopen = () => {
        log('Connected, sending command', `get ${format} ${mode}`);
        emit({
          phase: 'scanning',
          message: 'Đã kết nối. Chuyển sang cửa sổ EcoScanner → chọn máy quét → bấm Quét...',
        });
        socket.send(`get ${format} ${mode}`);
      };

      socket.onmessage = (event: MessageEvent) => {
        const data = event.data;

        if (data instanceof ArrayBuffer) {
          onBinaryReceived(socket, data);
          return;
        }

        if (data instanceof Blob) {
          log('Blob frame', { size: data.size, type: data.type });
          pendingBinaryReads++;
          data
            .arrayBuffer()
            .then((buffer) => onBinaryReceived(socket, buffer))
            .catch(() => undefined)
            .finally(() => {
              pendingBinaryReads--;
              tryComplete(socket);
            });
          return;
        }

        if (ArrayBuffer.isView(data)) {
          const view = new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
          onBinaryReceived(socket, view.slice().buffer);
          return;
        }

        if (typeof data === 'string') {
          handleTextMessage(socket, data);
          return;
        }

        log('Unknown frame type', typeof data);
      };

      socket.onclose = (event) => {
        log('Socket closed', { code: event.code, reason: event.reason, wasClean: event.wasClean });
        cleanup(socket);

        // Đã hoàn tất hoặc đã xử lý lỗi — không báo thêm (docs/WEB_PROTOCOL.md §6)
        if (settled) return;
        if (scanDone && files.length > 0) {
          succeed(socket);
          return;
        }
        if (scanFailed) return;

        if (this.cancelRequested) {
          fail(socket, 'Đã hủy quét tài liệu');
          return;
        }

        if (scanDone && files.length === 0) {
          fail(
            socket,
            'EcoScanner báo đã gửi file nhưng trình duyệt không nhận được frame Binary. ' +
              'Thử refresh trang hoặc kiểm tra tab WS trong DevTools.'
          );
          return;
        }

        // Có binary nhưng chưa nhận "1" — vẫn chấp nhận file khi socket đóng
        if (files.length > 0) {
          succeed(socket);
          return;
        }

        fail(
          socket,
          'Không nhận được dữ liệu quét. Hãy hoàn tất thao tác quét trên cửa sổ EcoScanner (chọn máy quét và bấm Quét).'
        );
      };

      socket.onerror = () => {
        const hint = this.buildConnectionErrorHint();
        fail(socket, hint, false);
      };
    });
  }

  /** Hủy phiên quét đang mở (nếu có). */
  cancelActiveScan(): void {
    this.cancelRequested = true;
    if (!this.activeSocket) return;
    const socket = this.activeSocket;
    this.activeSocket = null;
    if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
      socket.close();
    }
  }

  private buildConnectionErrorHint(): string {
    const base =
      'Không thể kết nối WebSocket tới EcoScanner (ws://127.0.0.1:8282). ' +
      'EcoScanner phải đang chạy trên máy tính đang mở trình duyệt (127.0.0.1 = localhost của máy client, không phải server web).';

    if (typeof window === 'undefined') {
      return base;
    }

    const host = window.location.hostname;
    const isLocalPortal =
      host === 'localhost' || host === '127.0.0.1' || host === '[::1]';
    const isSecure = window.location.protocol === 'https:';

    if (!isLocalPortal && isSecure) {
      return (
        base +
        ' Trang đang chạy HTTPS — trình duyệt có thể chặn ws:// (Mixed Content). Xem docs/WEB_PROTOCOL.md mục 13.'
      );
    }

    if (!isLocalPortal) {
      return (
        base +
        ' Trang đang mở từ domain ' +
        host +
        ' — Chrome/Edge có thể chặn WebSocket tới localhost (Local Network Access). Thử cấp quyền Local network trong Site settings, policy IT, hoặc mở portal qua localhost khi dev.'
      );
    }

    return base + ' Kiểm tra EcoScanner đã bật, cổng 8282 không bị firewall chặn.';
  }

  private bufferToFile(
    data: ArrayBuffer,
    format: ScanFormat,
    index: number,
    fileNameInput?: string
  ): File {
    const mimeType = format === 'pdf' ? 'application/pdf' : `image/${format === 'jpg' ? 'jpeg' : format}`;
    const ext = format === 'jpeg' ? 'jpg' : format;
    const finalFileName = fileNameInput
      ? `${fileNameInput.trim()}.${ext}`
      : `scan_${this.buildTimestamp()}_${index}.${ext}`;
    return new File([data], finalFileName, { type: mimeType });
  }

  private buildTimestamp(): string {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}_${pad(d.getHours())}${pad(d.getMinutes())}${pad(d.getSeconds())}`;
  }
}
