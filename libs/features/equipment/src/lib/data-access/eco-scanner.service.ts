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

@Injectable({
  providedIn: 'root',
})
export class EcoScannerService {
  private config = inject(APP_CONFIG);

  private activeSocket: WebSocket | null = null;

  /**
   * Quét tài liệu qua EcoScanner (WebSocket localhost).
   * Mặc định: PDF, single output (1 file nhiều trang).
   */
  scan(options?: ScanOptions, onProgress?: (progress: ScanProgress) => void): Promise<File[]> {
    const format = options?.format ?? 'pdf';
    const mode = options?.mode ?? 's';
    const wsUrl = options?.wsUrl ?? this.config.ecoScannerWsUrl ?? 'ws://127.0.0.1:8282';
    const timeoutMs = options?.timeoutMs ?? 120_000;

    return new Promise((resolve, reject) => {
      const files: File[] = [];
      let settled = false;

      const emit = (progress: ScanProgress) => onProgress?.(progress);

      const cleanup = (socket: WebSocket) => {
        clearTimeout(timeoutId);
        if (this.activeSocket === socket) {
          this.activeSocket = null;
        }
        if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
          socket.close();
        }
      };

      const fail = (socket: WebSocket, message: string) => {
        if (settled) return;
        settled = true;
        cleanup(socket);
        emit({ phase: 'error', message });
        reject(new Error(message));
      };

      const succeed = (socket: WebSocket) => {
        if (settled) return;
        settled = true;
        cleanup(socket);
        if (files.length === 0) {
          emit({ phase: 'error', message: 'Không nhận được dữ liệu quét' });
          reject(new Error('Không nhận được dữ liệu quét'));
          return;
        }
        emit({ phase: 'done', message: 'Quét thành công' });
        resolve(files);
      };

      emit({ phase: 'connecting', message: 'Đang kết nối tới dịch vụ scan...' });

      const socket = new WebSocket(wsUrl);
      this.activeSocket = socket;
      socket.binaryType = 'arraybuffer';

      const timeoutId = setTimeout(() => {
        fail(socket, 'Quá thời gian chờ máy quét. Vui lòng thử lại.');
      }, timeoutMs);

      socket.onopen = () => {
        emit({ phase: 'scanning', message: 'Đã kết nối. Đang khởi chạy máy quét...' });
        socket.send(`get ${format} ${mode}`);
      };

      socket.onmessage = (event: MessageEvent<ArrayBuffer | string>) => {
        if (event.data instanceof ArrayBuffer) {
          emit({ phase: 'receiving', message: 'Đang nhận file quét...' });
          files.push(this.bufferToFile(event.data, format, files.length + 1));
          return;
        }

        if (typeof event.data === 'string') {
          const text = event.data.trim();
          if (text.includes(CLIENT_BUSY_MESSAGE)) {
            fail(socket, `${CLIENT_BUSY_MESSAGE}. Vui lòng đóng tab hoặc ứng dụng khác đang dùng máy quét.`);
            return;
          }
          if (text) {
            emit({ phase: 'scanning', message: text });
          }
        }
      };

      socket.onclose = () => {
        if (!settled) {
          succeed(socket);
        }
      };

      socket.onerror = () => {
        fail(
          socket,
          'Không thể kết nối đến EcoScanner. Hãy chắc chắn phần mềm EcoScanner đang chạy trên máy của bạn.'
        );
      };
    });
  }

  /** Hủy phiên quét đang mở (nếu có). */
  cancelActiveScan(): void {
    if (!this.activeSocket) return;
    const socket = this.activeSocket;
    this.activeSocket = null;
    if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
      socket.close();
    }
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
