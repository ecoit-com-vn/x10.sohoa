import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { SUPPRESS_HTTP_ERROR_TOAST } from '@sohoa.frontend/shared/core';

export interface PmisEquipmentTypeMapping {
  id: string;
  pmisMaLoaiTB: string;
  gridTypeId: number;
  gridTypeName: string | null;
  equipmentTypeId: string;
  equipmentTypeCode: string | null;
  equipmentTypeName: string | null;
  rowVersion: number;
}

export interface SavePmisEquipmentTypeMappingRequest {
  pmisMaLoaiTB: string;
  gridTypeId: number;
  equipmentTypeId: string;
  rowVersion?: number;
}

export interface PmisDeviceTypeOption {
  maLoaiTB: string;
  tenLoaiTB: string;
  source: string;
}

export interface GridTypeOption {
  id: number;
  name: string;
}

export interface SystemEquipmentTypeOption {
  id: string;
  code: string;
  name: string;
  gridTypeId: number | null;
}

@Injectable({ providedIn: 'root' })
export class PmisEquipmentTypeMappingService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiGatewayUrl}/api/v1/pmis-equipment-type-mapping`;
  private readonly equipmentTypeUrl = `${environment.apiGatewayUrl}/api/v1/equipmenttype`;

  getAll(): Observable<PmisEquipmentTypeMapping[]> {
    return this.http.get<PmisEquipmentTypeMapping[]>(this.apiUrl);
  }

  create(request: SavePmisEquipmentTypeMappingRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.apiUrl, request);
  }

  update(id: string, request: SavePmisEquipmentTypeMappingRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /**
   * Danh mục loại thiết bị đọc trực tiếp từ PMIS (gộp TBA + đường dây). Gọi ngay khi mở màn (không chờ
   * người dùng bấm gì) nên PMIS lỗi/chậm là chuyện thường — tự xử lý toast riêng ở component (dropdown
   * chuyển sang ô tự nhập), không dùng toast lỗi chung của interceptor.
   */
  getPmisDeviceTypes(): Observable<PmisDeviceTypeOption[]> {
    return this.http.get<PmisDeviceTypeOption[]>(
      `${environment.apiGatewayUrl}/api/v1/sync/lookup/device-types`,
      { context: new HttpContext().set(SUPPRESS_HTTP_ERROR_TOAST, true) }
    );
  }

  getGridTypes(): Observable<GridTypeOption[]> {
    return this.http.get<GridTypeOption[]>(`${this.equipmentTypeUrl}/grid-types/lookup`);
  }

  /** Không truyền page/pageSize để API trả về danh sách phẳng — FE tự lọc theo cấp điện áp. */
  getSystemEquipmentTypes(): Observable<SystemEquipmentTypeOption[]> {
    return this.http.get<SystemEquipmentTypeOption[]>(this.equipmentTypeUrl);
  }
}
