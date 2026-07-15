import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG, AppConfig } from '@sohoa.frontend/shared/core';

/**
 * DTO interfaces matching backend entities for Physical Storage.
 * Backend route: /api/physicalstorage/...
 */
export interface PhysicalShelfDto {
  id: number;
  code: string;
  name: string;
  description?: string;
  status: number; // 1 = Active, 0 = Locked
  isDeleted: boolean;
}

export interface PhysicalFloorDto {
  id: number;
  shelfId: number;
  code: string;
  name: string;
  description?: string;
  status: number;
  isDeleted: boolean;
}

export interface PhysicalBoxDto {
  id: number;
  floorId: number;
  code: string;
  name: string;
  description?: string;
  status: number;
  isDeleted: boolean;
}

/**
 * PhysicalStorageService – gọi API /api/physicalstorage/...
 * được proxy qua YARP gateway tới equipment-cluster.
 *
 * Endpoints thực tế:
 *   GET  /api/physicalstorage/shelves
 *   POST /api/physicalstorage/shelves
 *   PUT  /api/physicalstorage/shelves/{id}
 *   DEL  /api/physicalstorage/shelves/{id}
 *
 *   GET  /api/physicalstorage/shelves/{shelfId}/floors
 *   POST /api/physicalstorage/floors
 *   PUT  /api/physicalstorage/floors/{id}
 *   DEL  /api/physicalstorage/floors/{id}
 *
 *   GET  /api/physicalstorage/floors/{floorId}/boxes
 *   POST /api/physicalstorage/boxes
 *   PUT  /api/physicalstorage/boxes/{id}
 *   DEL  /api/physicalstorage/boxes/{id}
 */
@Injectable({ providedIn: 'root' })
export class PhysicalStorageService {
  private readonly config = inject<AppConfig>(APP_CONFIG);
  private get base() {
    return `${this.config.apiGatewayUrl}/api/physicalstorage`;
  }

  constructor(private http: HttpClient) {}

  // ─────────────────── SHELF ───────────────────
  getShelves(): Observable<PhysicalShelfDto[]> {
    return this.http.get<PhysicalShelfDto[]>(`${this.base}/shelves`);
  }

  getShelfById(id: number): Observable<PhysicalShelfDto> {
    return this.http.get<PhysicalShelfDto>(`${this.base}/shelves/${id}`);
  }

  createShelf(payload: Omit<PhysicalShelfDto, 'id' | 'isDeleted'>): Observable<PhysicalShelfDto> {
    return this.http.post<PhysicalShelfDto>(`${this.base}/shelves`, payload);
  }

  updateShelf(id: number, payload: Partial<PhysicalShelfDto>): Observable<void> {
    return this.http.put<void>(`${this.base}/shelves/${id}`, payload);
  }

  deleteShelf(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/shelves/${id}`);
  }

  // ─────────────────── FLOOR ───────────────────
  getFloorsByShelf(shelfId: number): Observable<PhysicalFloorDto[]> {
    return this.http.get<PhysicalFloorDto[]>(`${this.base}/shelves/${shelfId}/floors`);
  }

  getFloorById(id: number): Observable<PhysicalFloorDto> {
    return this.http.get<PhysicalFloorDto>(`${this.base}/floors/${id}`);
  }

  createFloor(payload: Omit<PhysicalFloorDto, 'id' | 'isDeleted'>): Observable<PhysicalFloorDto> {
    return this.http.post<PhysicalFloorDto>(`${this.base}/floors`, payload);
  }

  updateFloor(id: number, payload: Partial<PhysicalFloorDto>): Observable<void> {
    return this.http.put<void>(`${this.base}/floors/${id}`, payload);
  }

  deleteFloor(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/floors/${id}`);
  }

  // ─────────────────── BOX ─────────────────────
  getBoxesByFloor(floorId: number): Observable<PhysicalBoxDto[]> {
    return this.http.get<PhysicalBoxDto[]>(`${this.base}/floors/${floorId}/boxes`);
  }

  getBoxById(id: number): Observable<PhysicalBoxDto> {
    return this.http.get<PhysicalBoxDto>(`${this.base}/boxes/${id}`);
  }

  createBox(payload: Omit<PhysicalBoxDto, 'id' | 'isDeleted'>): Observable<PhysicalBoxDto> {
    return this.http.post<PhysicalBoxDto>(`${this.base}/boxes`, payload);
  }

  updateBox(id: number, payload: Partial<PhysicalBoxDto>): Observable<void> {
    return this.http.put<void>(`${this.base}/boxes/${id}`, payload);
  }

  deleteBox(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/boxes/${id}`);
  }
}
