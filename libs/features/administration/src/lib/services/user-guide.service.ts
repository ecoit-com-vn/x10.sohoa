import { Injectable, inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@sohoa.frontend/shared/core';

export interface UserGuide {
  id: number;
  roleName: string;
  fileName: string;
  fileSize?: number;
  createdAt?: string;
  updatedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class UserGuideService {
  private api = inject(ApiService);
  private readonly base = '/api/v1/user-guides';

  getGuides(): Observable<UserGuide[]> {
    return this.api.get<UserGuide[]>(this.base);
  }

  createGuide(formData: FormData): Observable<UserGuide> {
    return this.api.post<UserGuide>(this.base, formData);
  }

  updateGuide(id: number, formData: FormData): Observable<UserGuide> {
    return this.api.put<UserGuide>(`${this.base}/${id}`, formData);
  }

  deleteGuide(id: number): Observable<void> {
    return this.api.delete<void>(`${this.base}/${id}`);
  }

  /** Tải file nhị phân — trả về HttpResponse để đọc header Content-Disposition. */
  downloadGuide(id: number): Observable<HttpResponse<Blob>> {
    return this.api.getBlobResponse(`${this.base}/${id}/download`);
  }
}
