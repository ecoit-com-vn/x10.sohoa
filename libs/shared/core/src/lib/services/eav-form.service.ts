import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config.token';

export interface EavFormTemplate {
  id: string;
  name: string;
  description: string;
  schema: string; // JSON schema stringified
  version: number;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
}

@Injectable({
  providedIn: 'root'
})
export class EavFormService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private get apiUrl() {
    return `${this.config.apiGatewayUrl}/api/v1/eav-form-templates`;
  }

  getTemplates(): Observable<EavFormTemplate[]> {
    return this.http.get<EavFormTemplate[]>(this.apiUrl);
  }

  getTemplateById(id: string): Observable<EavFormTemplate> {
    return this.http.get<EavFormTemplate>(`${this.apiUrl}/${id}`);
  }

  createTemplate(name: string, description: string, schema: string, createdBy: string = 'admin'): Observable<EavFormTemplate> {
    return this.http.post<EavFormTemplate>(this.apiUrl, {
      name,
      description,
      schema,
      createdBy
    });
  }

  updateTemplate(id: string, name: string, description: string, schema: string, updatedBy: string = 'admin'): Observable<EavFormTemplate> {
    return this.http.put<EavFormTemplate>(`${this.apiUrl}/${id}`, {
      name,
      description,
      schema,
      updatedBy
    });
  }

  deleteTemplate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
