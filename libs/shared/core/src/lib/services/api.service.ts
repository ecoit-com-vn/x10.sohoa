import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config.token';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);

  private getBaseUrl(path: string): string {
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path;
    }
    const cleanPath = path.startsWith('/') ? path : `/${path}`;
    return `${this.config.apiGatewayUrl}${cleanPath}`;
  }

  get<T>(path: string, options?: {
    headers?: HttpHeaders | { [header: string]: string | string[] };
    context?: any;
    observe?: any;
    params?: HttpParams | { [param: string]: string | number | boolean | ReadonlyArray<string | number | boolean> };
    reportProgress?: boolean;
    responseType?: any;
    withCredentials?: boolean;
  }): Observable<T> {
    return this.http.get<T>(this.getBaseUrl(path), options);
  }

  post<T>(path: string, body: any, options?: {
    headers?: HttpHeaders | { [header: string]: string | string[] };
    context?: any;
    observe?: any;
    params?: HttpParams | { [param: string]: string | number | boolean | ReadonlyArray<string | number | boolean> };
    reportProgress?: boolean;
    responseType?: any;
    withCredentials?: boolean;
  }): Observable<T> {
    return this.http.post<T>(this.getBaseUrl(path), body, options);
  }

  put<T>(path: string, body: any, options?: {
    headers?: HttpHeaders | { [header: string]: string | string[] };
    context?: any;
    observe?: any;
    params?: HttpParams | { [param: string]: string | number | boolean | ReadonlyArray<string | number | boolean> };
    reportProgress?: boolean;
    responseType?: any;
    withCredentials?: boolean;
  }): Observable<T> {
    return this.http.put<T>(this.getBaseUrl(path), body, options);
  }

  patch<T>(path: string, body: any, options?: {
    headers?: HttpHeaders | { [header: string]: string | string[] };
    context?: any;
    observe?: any;
    params?: HttpParams | { [param: string]: string | number | boolean | ReadonlyArray<string | number | boolean> };
    reportProgress?: boolean;
    responseType?: any;
    withCredentials?: boolean;
  }): Observable<T> {
    return this.http.patch<T>(this.getBaseUrl(path), body, options);
  }

  delete<T>(path: string, options?: {
    headers?: HttpHeaders | { [header: string]: string | string[] };
    context?: any;
    observe?: any;
    params?: HttpParams | { [param: string]: string | number | boolean | ReadonlyArray<string | number | boolean> };
    reportProgress?: boolean;
    responseType?: any;
    withCredentials?: boolean;
    body?: any;
  }): Observable<T> {
    return this.http.delete<T>(this.getBaseUrl(path), options);
  }

  /** Tải file binary — trả về HttpResponse để đọc header Content-Disposition. */
  getBlobResponse(path: string, options?: {
    params?: HttpParams | { [param: string]: string | number | boolean | ReadonlyArray<string | number | boolean> };
  }): Observable<HttpResponse<Blob>> {
    return this.http.get(this.getBaseUrl(path), {
      ...options,
      responseType: 'blob',
      observe: 'response'
    });
  }
}
