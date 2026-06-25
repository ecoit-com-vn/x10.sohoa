import { Injectable, inject } from '@angular/core';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

export type ScanFormat = 'pdf' | 'png' | 'jpg' | 'jpeg' | 'bmp' | 'gif' | 'tiff';
export type ScanMode = 's' | 'm';

export interface ScanOptions {
  format?: ScanFormat;
  mode?: ScanMode;
  wsUrl?: string;
  timeoutMs?: number;
}

export type ScanPhase = 'idle' | 'connecting' | 'scanning' | 'receiving' | 'done' | 'error';

export interface ScanProgress {
  phase: ScanPhase;
  message?: string;
}

const CLIENT_BUSY_MESSAGE = 'Client khác đang kết nối';
const NO_DATA_MESSAGE = 'Không có dữ liệu';

/** Chờ thêm sau text "Không có dữ liệu" — EcoScanner đôi khi gửi text trước Binary hoặc đang convert PDF. */
const NO_DATA_GRACE_MS = 8_000;

const CONVERT_SEND_FAILURE_HINT =
  'EcoScanner đã quét xong nhưng không gửi được file qua WebSocket (bước ghép/convert PDF). ' +
  'Kiểm tra thư mục file_temp của EcoScanner, log ứng dụng, thử quét lại hoặc liên hệ nhà cung cấp EcoScanner.';

@Injectable({
  providedIn: 'root',
})
export class EcoScannerService {
  private config = inject(APP_CONFIG);

  private activeSocket: WebSocket | null = null;
  private cancelRequested = false;

  /**
   * Quét tài liệu qua EcoScanner (WebSocket localhost).
   * Luồng mode `s`: Binary (file) → Text ("1") → Close — xem docs/SCAN_WEB_INTEGRATION.md mục 5.
   */
  scan(options?: ScanOptions, onProgress?: (progress: ScanProgress) => void): Promise<File[]> {
    const format = options?.format ?? 'pdf';
    const mode = options?.mode ?? 's';
    const wsUrl = options?.wsUrl ?? this.config.ecoScannerWsUrl ?? 'ws://127.0.0.1:8282';
    const timeoutMs = options?.timeoutMs ?? 120_000;
    const debug = this.config.ecoScannerDebug ?? !this.config.production;

    this.cancelRequested = false;

    return new Promise((resolve, reject) => {
      const files: File[] = [];
      let settled = false;
      let socketClosed = false;
      let pendingBinaryReads = 0;
      let completionAck: number | null = null;
      let noDataSignal = false;
      let noDataGraceTimer: ReturnType<typeof setTimeout> | null = null;

      const log = (message: string, detail?: unknown) => {
        if (debug) {
          console.debug(`[EcoScanner] ${message}`, detail ?? '');
        }
      };

      const emit = (progress: ScanProgress) => onProgress?.(progress);

      const cleanup = (socket: WebSocket) => {
        clearTimeout(timeoutId);
        if (noDataGraceTimer) {
          clearTimeout(noDataGraceTimer);
          noDataGraceTimer = null;
        }
        if (this.activeSocket === socket) {
          this.activeSocket = null;
        }
      };

      const fail = (socket: WebSocket, message: string, closeSocket = false) => {
        if (settled) return;
        settled = true;
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
        cleanup(socket);

        if (files.length === 0) {
          let message: string;
          if (this.cancelRequested) {
            message = 'Đã hủy quét tài liệu';
          } else if (noDataSignal) {
            message = CONVERT_SEND_FAILURE_HINT;
          } else if (completionAck !== null && completionAck > 0) {
            message =
              'EcoScanner báo đã gửi file nhưng trình duyệt không nhận được frame Binary. ' +
              'Thử refresh trang hoặc kiểm tra tab WS trong DevTools.';
          } else {
            message =
              'Không nhận được dữ liệu quét. Hãy hoàn tất thao tác quét trên cửa sổ EcoScanner (chọn máy quét và bấm Quét).';
          }
          log('FAIL empty files', { completionAck, cancelRequested: this.cancelRequested, noDataSignal });
          emit({ phase: 'error', message });
          reject(new Error(message));
          return;
        }

        log('OK', { fileCount: files.length, sizes: files.map((f) => f.size) });
        emit({ phase: 'done', message: 'Quét thành công' });
        resolve([...files]);
      };

      const tryFinish = (socket: WebSocket) => {
        if (settled || pendingBinaryReads > 0) return;

        if (files.length > 0) {
          if (completionAck !== null && files.length >= completionAck) {
            succeed(socket);
            return;
          }
          if (socketClosed) {
            succeed(socket);
          }
          return;
        }

        if (!socketClosed) return;

        if (noDataSignal && noDataGraceTimer) return;

        succeed(socket);
      };

      const scheduleNoDataGrace = (socket: WebSocket) => {
        if (noDataGraceTimer) return;
        noDataGraceTimer = setTimeout(() => {
          noDataGraceTimer = null;
          if (settled || files.length > 0) return;
          log('Grace period hết sau "Không có dữ liệu", vẫn chưa có Binary');
          tryFinish(socket);
        }, NO_DATA_GRACE_MS);
      };

      const onBinaryReceived = (socket: WebSocket, data: ArrayBuffer) => {
        if (settled) return;
        if (data.byteLength === 0) {
          log('Binary frame rỗng (0 byte) — bỏ qua');
          return;
        }

        noDataSignal = false;
        if (noDataGraceTimer) {
          clearTimeout(noDataGraceTimer);
          noDataGraceTimer = null;
        }

        log('Binary frame', { bytes: data.byteLength });
        emit({ phase: 'receiving', message: 'Đang nhận file quét...' });
        files.push(this.bufferToFile(data, format, files.length + 1));
        tryFinish(socket);
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

        if (text.includes(CLIENT_BUSY_MESSAGE)) {
          fail(
            socket,
            `${CLIENT_BUSY_MESSAGE}. Vui lòng đóng tab hoặc ứng dụng khác đang dùng máy quét.`,
            true
          );
          return;
        }

        if (text === NO_DATA_MESSAGE) {
          if (this.cancelRequested) {
            fail(socket, 'Đã hủy quét tài liệu', true);
            return;
          }

          // Không fail/đóng socket ngay — EcoScanner có thể đang convert PDF sau khi quét xong.
          noDataSignal = true;
          log('Nhận "Không có dữ liệu" — chờ Binary thêm (không đóng socket)');
          emit({
            phase: 'receiving',
            message: 'EcoScanner báo không có dữ liệu — đang chờ file quét (nếu đã quét xong, vui lòng đợi)...',
          });
          scheduleNoDataGrace(socket);
          tryFinish(socket);
          return;
        }

        if (/^\d+$/.test(text)) {
          completionAck = Number.parseInt(text, 10);
          emit({
            phase: 'receiving',
            message: `EcoScanner báo đã gửi ${completionAck} file — đang nhận dữ liệu...`,
          });
          tryFinish(socket);
          return;
        }

        emit({ phase: 'scanning', message: text });
      };

      emit({ phase: 'connecting', message: 'Đang kết nối tới dịch vụ scan...' });
      log('Connecting', wsUrl);

      const socket = new WebSocket(wsUrl);
      this.activeSocket = socket;
      socket.binaryType = 'arraybuffer';

      const timeoutId = setTimeout(() => {
        fail(
          socket,
          'Quá thời gian chờ máy quét. Hãy hoàn tất quét trên cửa sổ EcoScanner (chọn máy scan và bấm Quét).',
          true
        );
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

        if (typeof data === 'string') {
          handleTextMessage(socket, data);
          return;
        }

        if (data instanceof ArrayBuffer) {
          onBinaryReceived(socket, data);
          return;
        }

        if (ArrayBuffer.isView(data)) {
          const view = new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
          onBinaryReceived(socket, view.slice().buffer);
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
              tryFinish(socket);
            });
          return;
        }

        log('Unknown frame type', typeof data);
      };

      socket.onclose = (event) => {
        log('Socket closed', { code: event.code, reason: event.reason, wasClean: event.wasClean });
        socketClosed = true;

        if (noDataSignal && files.length === 0) {
          scheduleNoDataGrace(socket);
        }

        tryFinish(socket);
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
        ' Trang đang chạy HTTPS — trình duyệt có thể chặn ws:// (Mixed Content). Xem docs/SCAN_WEB_INTEGRATION.md mục 6–7.'
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

  private bufferToFile(data: ArrayBuffer, format: ScanFormat, index: number): File {
    const mimeType = format === 'pdf' ? 'application/pdf' : `image/${format === 'jpg' ? 'jpeg' : format}`;
    const ext = format === 'jpeg' ? 'jpg' : format;
    const fileName = `scan_${this.buildTimestamp()}_${index}.${ext}`;
    return new File([data], fileName, { type: mimeType });
  }

  private buildTimestamp(): string {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}_${pad(d.getHours())}${pad(d.getMinutes())}${pad(d.getSeconds())}`;
  }
}
