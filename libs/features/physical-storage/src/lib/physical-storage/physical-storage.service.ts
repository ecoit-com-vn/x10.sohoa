import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';

/**
 * DTO interfaces matching backend entities for Physical Storage.
 * Backend route: /api/physicalstorage/...
 */
export interface PhysicalShelfDto {
  id: number;
  unitId?: number | null;
  unitName?: string | null;
  code: string;
  name: string;
  description?: string;
  /** Thứ tự ưu tiên — không bắt buộc; mặc định 1. Số nhỏ hơn ưu tiên cao hơn. */
  priority?: number | null;
  status: number; // 1 = Active, 0 = Locked
  isDeleted: boolean;
}

export interface PhysicalFloorDto {
  id: number;
  shelfId: number;
  code: string;
  name: string;
  description?: string;
  priority?: number | null;
  status: number;
  isDeleted: boolean;
}

export interface PhysicalBoxDto {
  id: number;
  floorId: number;
  code: string;
  name: string;
  description?: string;
  priority?: number | null;
  status: number;
  isDeleted: boolean;
}

/**
 * PhysicalStorageService – gọi API qua ApiService.
 *
 * Endpoints:
 *   GET  /api/physicalstorage/shelves?unitId=
 *   GET  /api/physicalstorage/floors?unitId=
 *   GET  /api/physicalstorage/boxes?unitId=
 */
@Injectable({ providedIn: 'root' })
export class PhysicalStorageService {
  private readonly api = inject(ApiService);

  private readonly base = '/api/physicalstorage';
  private readonly equipmentBase = '/api/v1/equipment';

  private unitParams(unitId?: number | null): Record<string, string | number> | undefined {
    if (unitId == null || unitId <= 0) return undefined;
    return { unitId };
  }

  /** Cây đơn vị (scoped theo JWT) — tái dùng API equipment. */
  getOrganizationUnits(): Observable<any[]> {
    return this.api.get<any[]>(`${this.equipmentBase}/get-organization-units`);
  }

  // ─────────────────── SHELF ───────────────────
  getShelves(unitId?: number | null): Observable<PhysicalShelfDto[]> {
    return this.api.get<PhysicalShelfDto[]>(`${this.base}/shelves`, {
      params: this.unitParams(unitId)
    });
  }

  getShelfById(id: number): Observable<PhysicalShelfDto> {
    return this.api.get<PhysicalShelfDto>(`${this.base}/shelves/${id}`);
  }

  createShelf(payload: Omit<PhysicalShelfDto, 'id' | 'isDeleted'>): Observable<PhysicalShelfDto> {
    return this.api.post<PhysicalShelfDto>(`${this.base}/shelves`, payload);
  }

  updateShelf(id: number, payload: Partial<PhysicalShelfDto>): Observable<void> {
    return this.api.put<void>(`${this.base}/shelves/${id}`, payload);
  }

  deleteShelf(id: number): Observable<void> {
    return this.api.delete<void>(`${this.base}/shelves/${id}`);
  }

  // ─────────────────── FLOOR ───────────────────
  getFloorsByUnit(unitId?: number | null): Observable<PhysicalFloorDto[]> {
    return this.api.get<PhysicalFloorDto[]>(`${this.base}/floors`, {
      params: this.unitParams(unitId)
    });
  }

  getFloorsByShelf(shelfId: number): Observable<PhysicalFloorDto[]> {
    return this.api.get<PhysicalFloorDto[]>(`${this.base}/shelves/${shelfId}/floors`);
  }

  getFloorById(id: number): Observable<PhysicalFloorDto> {
    return this.api.get<PhysicalFloorDto>(`${this.base}/floors/${id}`);
  }

  createFloor(payload: Omit<PhysicalFloorDto, 'id' | 'isDeleted'>): Observable<PhysicalFloorDto> {
    return this.api.post<PhysicalFloorDto>(`${this.base}/floors`, payload);
  }

  updateFloor(id: number, payload: Partial<PhysicalFloorDto>): Observable<void> {
    return this.api.put<void>(`${this.base}/floors/${id}`, payload);
  }

  deleteFloor(id: number): Observable<void> {
    return this.api.delete<void>(`${this.base}/floors/${id}`);
  }

  // ─────────────────── BOX ─────────────────────
  getBoxesByUnit(unitId?: number | null): Observable<PhysicalBoxDto[]> {
    return this.api.get<PhysicalBoxDto[]>(`${this.base}/boxes`, {
      params: this.unitParams(unitId)
    });
  }

  getBoxesByFloor(floorId: number): Observable<PhysicalBoxDto[]> {
    return this.api.get<PhysicalBoxDto[]>(`${this.base}/floors/${floorId}/boxes`);
  }

  getBoxById(id: number): Observable<PhysicalBoxDto> {
    return this.api.get<PhysicalBoxDto>(`${this.base}/boxes/${id}`);
  }

  createBox(payload: Omit<PhysicalBoxDto, 'id' | 'isDeleted'>): Observable<PhysicalBoxDto> {
    return this.api.post<PhysicalBoxDto>(`${this.base}/boxes`, payload);
  }

  updateBox(id: number, payload: Partial<PhysicalBoxDto>): Observable<void> {
    return this.api.put<void>(`${this.base}/boxes/${id}`, payload);
  }

  deleteBox(id: number): Observable<void> {
    return this.api.delete<void>(`${this.base}/boxes/${id}`);
  }
}
