import { Component, inject, OnInit } from '@angular/core';
import { Card } from 'primeng/card';
import { Button } from 'primeng/button';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  imports: [Card, Button, CommonModule, FormsModule],
  template: `
    <div class="login-wrapper">
      <p-card header="Đăng nhập hệ thống EVNHANOI" [style]="{ width: '400px' }">
        <div style="display: flex; flex-direction: column; gap: 1rem;">
          <div *ngIf="error" style="color: red; font-size: 0.875rem; text-align: center;">{{ error }}</div>
          
          <p-button label="Đăng nhập với EVN SSO" (onClick)="onSsoLogin()" [loading]="loading" styleClass="w-full" [style]="{'width':'100%', 'background-color': '#005b9f'}"></p-button>

          <div style="text-align: center; border-bottom: 1px solid #f1f5f9; line-height: 0.1em; margin: 10px 0 20px;">
            <span style="background:#fff; padding:0 10px; color: #9ca3af; font-size: 0.75rem;">HOẶC ĐĂNG NHẬP HỆ THỐNG</span>
          </div>

          <form (submit)="onLocalLogin($event)" style="display: flex; flex-direction: column; gap: 0.75rem;">
            <div style="display: flex; flex-direction: column; gap: 4px;">
              <label style="font-size: 0.875rem; font-weight: 600; color: #4b5563;">Tài khoản</label>
              <input type="text" [(ngModel)]="username" name="username" placeholder="Tên đăng nhập..." required style="height: 38px; border: 1px solid #d1d5db; border-radius: 4px; padding: 0 10px; outline: none; font-size: 0.875rem;" />
            </div>
            
            <div style="display: flex; flex-direction: column; gap: 4px;">
              <label style="font-size: 0.875rem; font-weight: 600; color: #4b5563;">Mật khẩu</label>
              <input type="password" [(ngModel)]="password" name="password" placeholder="Mật khẩu..." required style="height: 38px; border: 1px solid #d1d5db; border-radius: 4px; padding: 0 10px; outline: none; font-size: 0.875rem;" />
            </div>
            
            <p-button label="Đăng nhập bằng tài khoản" type="submit" [loading]="localLoading" styleClass="w-full" [style]="{'width':'100%', 'margin-top':'0.5rem', 'background-color': '#475569'}"></p-button>
          </form>
        </div>
      </p-card>
    </div>
  `,
  styles: `
    .login-wrapper {
      display: flex;
      justify-content: center;
      align-items: center;
      height: 100vh;
      background-color: var(--p-surface-50);
    }
  `,
})
export class Login implements OnInit {
  loading = false;
  localLoading = false;
  error = '';
  username = '';
  password = '';
  
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  
  ngOnInit() {
    if (typeof window !== 'undefined') {
      this.route.queryParams.subscribe(params => {
        const ticket = params['ticket'];
        if (ticket) {
          this.verifySsoTicket(ticket);
        }
      });
    }
  }

  onSsoLogin() {
    if (typeof window !== 'undefined') {
      // Redirect to EVN SSO login
      const appCode = 'SOHOAX10';
      const redirectUrl = encodeURIComponent(window.location.origin + '/login');
      window.location.href = `https://sso.evnhanoi.vn//sso/login?appCode=${appCode}&returnUrl=${redirectUrl}`;
    }
  }

  onLocalLogin(event: Event) {
    event.preventDefault();
    if (!this.username || !this.password) {
      this.error = 'Vui lòng nhập đầy đủ tài khoản và mật khẩu';
      return;
    }
    
    this.localLoading = true;
    this.error = '';

    this.http.post(`${environment.apiGatewayUrl}/api/v1/auth/login`, {
      username: this.username,
      password: this.password
    }).subscribe({
      next: (res: any) => {
        this.localLoading = false;
        // Backend trả về { AccessToken, RefreshToken }
        const token = res.AccessToken || res.accessToken || res.access_token || res.token;
        const refreshToken = res.RefreshToken || res.refreshToken || res.refresh_token;
        
        if (token) {
          if (typeof window !== 'undefined') {
            localStorage.setItem('token', token);
            if (refreshToken) {
              localStorage.setItem('refreshToken', refreshToken);
            }
          }
          this.router.navigate(['/']);
        } else {
          this.error = 'Phản hồi không chứa token đăng nhập. Vui lòng liên hệ quản trị viên.';
        }
      },
      error: (err) => {
        this.localLoading = false;
        
        // Xử lý các mã lỗi cụ thể
        if (err.status === 423) {
          // Tài khoản bị khóa tạm thời
          this.error = err.error?.message || 'Tài khoản đang bị khóa tạm thời do đăng nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.';
        } else if (err.status === 401) {
          this.error = err.error?.message || 'Tài khoản hoặc mật khẩu không chính xác.';
        } else if (err.status === 0) {
          this.error = 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng.';
        } else {
          this.error = err.error?.message || 'Đã xảy ra lỗi. Vui lòng thử lại.';
        }
        
        console.error('Local Login error', err);
      }
    });
  }


  verifySsoTicket(ticket: string) {
    this.loading = true;
    this.error = '';
    
    this.http.post(`${environment.apiGatewayUrl}/api/v1/auth/login?ticket=${ticket}`, {}).subscribe({
      next: (res: any) => {
        this.loading = false;
        const token = res.accessToken || res.AccessToken || res.access_token || res.token || res.data?.token || res.data?.access_token;
        if (token) {
          if (typeof window !== 'undefined') {
            localStorage.setItem('token', token);
          }
          this.router.navigate(['/']); // Redirect to AdminLayout
        } else {
          this.error = 'Phản hồi không chứa token đăng nhập';
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = 'Đăng nhập SSO thất bại hoặc tài khoản chưa được thiết lập. Vui lòng thử lại.';
        console.error('SSO Login error', err);
      }
    });
  }
}
