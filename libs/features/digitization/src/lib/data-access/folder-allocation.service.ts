import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '@sohoa.frontend/shared/core';

export interface FolderAllocationItem {
  id: string;
  folder_id: string;
  folder_name: string;
  folder_path?: string;
  user_id: string;
  user_name: string;
  user_full_name: string;
  allocated_date: string;
  status: 'Active' | 'Revoked';
  unit_id: number;
  unit_name?: string;
}

export interface CreateFolderAllocationRequest {
  folder_id: string;
  user_id: string;
}

export interface UpdateFolderAllocationRequest {
  folder_id: string;
  user_id: string;
}

export interface FolderLookupItem {
  id: string;
  name: string;
  parent_id?: string | null;
  unit_id: number;
  unit_code: string;
}

export interface UserLookupItem {
  id: string;
  user_name: string;
  full_name: string;
  organization_unit_id: number;
  organization_unit_name: string;
}

@Injectable({
  providedIn: 'root'
})
export class FolderAllocationService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private get baseUrl() {
    return `${this.config.apiGatewayUrl}/api/v1/folder-allocations`;
  }

  getPaged(
    page: number,
    pageSize: number,
    keyword?: string,
    status?: string,
    fromDate?: string,
    toDate?: string
  ): Observable<{ items: FolderAllocationItem[]; total_count: number; page: number; page_size: number }> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('page_size', pageSize.toString());

    if (keyword?.trim()) {
      params = params.set('keyword', keyword.trim());
    }
    if (status) {
      params = params.set('status', status);
    }
    if (fromDate) {
      params = params.set('fromDate', fromDate);
    }
    if (toDate) {
      params = params.set('toDate', toDate);
    }

    return this.http.get<{ items: FolderAllocationItem[]; total_count: number; page: number; page_size: number }>(
      this.baseUrl,
      { params }
    );
  }

  getById(id: string): Observable<FolderAllocationItem> {
    return this.http.get<FolderAllocationItem>(`${this.baseUrl}/${id}`);
  }

  create(req: CreateFolderAllocationRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, req);
  }

  update(id: string, req: UpdateFolderAllocationRequest): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.baseUrl}/${id}`, req);
  }

  revoke(id: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.baseUrl}/${id}/revoke`, {});
  }

  reactivate(id: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.baseUrl}/${id}/reactivate`, {});
  }

  delete(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/${id}`);
  }

  getUsersLookup(): Observable<UserLookupItem[]> {
    return this.http.get<UserLookupItem[]>(`${this.baseUrl}/lookup/users`);
  }

  getFoldersLookup(): Observable<FolderLookupItem[]> {
    return this.http.get<FolderLookupItem[]>(`${this.baseUrl}/lookup/folders`);
  }

  getMyFolders(): Observable<FolderLookupItem[]> {
    return this.http.get<FolderLookupItem[]>(`${this.baseUrl}/my-folders`);
  }
}
