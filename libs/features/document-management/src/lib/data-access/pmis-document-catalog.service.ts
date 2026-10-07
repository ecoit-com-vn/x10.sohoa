import { Injectable, inject } from '@angular/core';
import { firstValueFrom, forkJoin, map, Observable, of, switchMap } from 'rxjs';
import { ApiService, APP_CONFIG } from '@sohoa.frontend/shared/core';
import { PmisCatalogDocumentsResponse, PmisCatalogNode, PmisInfrastructureLookupItem } from '../models/pmis-catalog.models';

interface DownloadTokenResponse {
  token: string;
  expiresInSeconds: number;
}

@Injectable({ providedIn: 'root' })
export class PmisDocumentCatalogService {
  private api = inject(ApiService);
  private config = inject(APP_CONFIG);
  private readonly base = '/api/v1/pmis-documents';

  /** Cấp gốc (chỉ Đơn vị) — gọi khi người dùng mở node gốc mặc định trong cây. */
  getCatalogUnits(): Observable<PmisCatalogNode[]> {
    return this.api.get<PmisCatalogNode[]>(`${this.base}/catalog/units`);
  }

  /** Trạm/Đường dây + Thiết bị của đúng 1 Đơn vị — gọi khi người dùng click mở 1 công ty trong cây. */
  getCatalogUnitChildren(unitNodeId: string): Observable<PmisCatalogNode[]> {
    return this.api.get<PmisCatalogNode[]>(`${this.base}/catalog/units/${encodeURIComponent(unitNodeId)}/children`);
  }

  /** Toàn bộ cây (Đơn vị + Trạm/Đường dây + Thiết bị) trong 1 lần gọi - ghép từ getCatalogUnits() +
   * getCatalogUnitChildren() cho từng đơn vị. Dùng cho các màn cần xem/duyệt hết ngay (vd. dialog "Chọn
   * từ kho PMIS" khi gắn tài liệu vào hồ sơ) - "Kho tài liệu PMIS" đã chuyển sang tải lười theo cấp nên
   * KHÔNG dùng hàm này. */
  getCatalogTree(): Observable<PmisCatalogNode[]> {
    return this.getCatalogUnits().pipe(
      switchMap((units) => {
        if (!units || units.length === 0) return of([] as PmisCatalogNode[]);
        return forkJoin(units.map((u) => this.getCatalogUnitChildren(u.id))).pipe(
          map((childrenLists) => [...units, ...childrenLists.flat()])
        );
      })
    );
  }

  /** Toàn bộ Trạm/Đường dây đã có tài liệu PMIS trên TẤT CẢ công ty - dùng cho ô tìm kiếm phía trên cây,
   * cho phép nhảy thẳng tới đúng Trạm/Đường dây mà không cần biết nó thuộc công ty nào. */
  searchInfrastructures(): Observable<PmisInfrastructureLookupItem[]> {
    return this.api.get<PmisInfrastructureLookupItem[]>(`${this.base}/catalog/infrastructures/lookup`);
  }

  getCatalogDocuments(folderId: string, keyword: string | null, page: number, pageSize: number): Observable<PmisCatalogDocumentsResponse> {
    const params: Record<string, string> = { folderId, page: String(page), pageSize: String(pageSize) };
    if (keyword) params['keyword'] = keyword;
    return this.api.get<PmisCatalogDocumentsResponse>(`${this.base}/catalog/documents`, { params });
  }

  /** Nút "Upload tài liệu" thủ công — dùng khi đồng bộ tự động từ PMIS lỗi. */
  uploadDocument(folderId: string, file: File, documentName?: string, documentType?: string): Observable<{ id: string; documentName: string }> {
    const formData = new FormData();
    formData.append('file', file);
    if (documentName) formData.append('documentName', documentName);
    if (documentType) formData.append('documentType', documentType);
    return this.api.post<{ id: string; documentName: string }>(`${this.base}/catalog/${folderId}/upload`, formData);
  }

  private getDownloadToken(documentId: string): Observable<DownloadTokenResponse> {
    return this.api.get<DownloadTokenResponse>(`${this.base}/${documentId}/download-url`);
  }

  /** Lấy nội dung file qua token 1 lần (dùng chung cho tải về và xem trước). */
  private async fetchDocumentBlob(documentId: string): Promise<Blob> {
    const tokenResponse = await firstValueFrom(this.getDownloadToken(documentId));
    if (!tokenResponse?.token) throw new Error('Không thể tạo link tải file');

    const url = `${this.config.apiGatewayUrl}/api/v1/files/download?token=${encodeURIComponent(tokenResponse.token)}`;
    const response = await fetch(url, { method: 'GET', credentials: 'include' });
    if (!response.ok) {
      let message = 'Không thể tải file';
      try {
        const body = await response.json();
        message = body?.message || message;
      } catch {
        // ignore parse errors
      }
      throw new Error(message);
    }
    return response.blob();
  }

  /**
   * Chuẩn bị xem trước 1 tài liệu: trả blob URL + loại hiển thị. Kiểu file nhận diện theo NỘI DUNG (chữ ký đầu file) rồi mới tới
   * phần mở rộng tên — máy chủ trả application/octet-stream và tên tài liệu PMIS không phải lúc nào cũng có đuôi, nên trình duyệt
   * không tự hiển thị được PDF/ảnh nếu không gán lại đúng kiểu cho blob. Người gọi PHẢI revoke URL khi đóng (revokePreviewUrl).
   */
  async getPreview(documentId: string, fileName?: string | null): Promise<{ url: string; kind: 'pdf' | 'image' | 'unsupported' }> {
    const raw = await this.fetchDocumentBlob(documentId);
    const head = new Uint8Array(await raw.slice(0, 8).arrayBuffer());
    const startsWith = (...bytes: number[]) => bytes.every((b, i) => head[i] === b);

    let mime: string | null = null;
    if (startsWith(0x25, 0x50, 0x44, 0x46)) mime = 'application/pdf'; // %PDF
    else if (startsWith(0xff, 0xd8, 0xff)) mime = 'image/jpeg';
    else if (startsWith(0x89, 0x50, 0x4e, 0x47)) mime = 'image/png';
    else if (startsWith(0x47, 0x49, 0x46, 0x38)) mime = 'image/gif';
    else if (startsWith(0x42, 0x4d)) mime = 'image/bmp';
    else if (startsWith(0x52, 0x49, 0x46, 0x46)) mime = 'image/webp'; // RIFF…WEBP

    if (!mime) {
      const ext = (fileName ?? '').split('.').pop()?.toLowerCase() ?? '';
      if (ext === 'pdf') mime = 'application/pdf';
      else if (['jpg', 'jpeg'].includes(ext)) mime = 'image/jpeg';
      else if (['png', 'gif', 'webp', 'bmp'].includes(ext)) mime = `image/${ext}`;
    }

    const kind = mime === 'application/pdf' ? 'pdf' : mime ? 'image' : 'unsupported';
    // Định dạng không xem trực tiếp được: không tạo blob URL (khỏi giữ bộ nhớ vô ích) — giao diện chỉ gợi ý tải về.
    if (kind === 'unsupported') return { url: '', kind };
    return { url: window.URL.createObjectURL(new Blob([raw], { type: mime! })), kind };
  }

  revokePreviewUrl(url: string): void {
    if (url) window.URL.revokeObjectURL(url);
  }

  /** Tải file qua token 1 lần (giống hệt cơ chế FileDownloadService của kho thư mục thiết bị). */
  async downloadDocument(documentId: string, fileName?: string): Promise<void> {
    const blob = await this.fetchDocumentBlob(documentId);
    const objectUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = fileName || 'tai-lieu-pmis';
    link.rel = 'noopener';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(objectUrl);
  }
}
