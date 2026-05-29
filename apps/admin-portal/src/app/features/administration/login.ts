import { Component, inject, OnInit } from '@angular/core';
import { Card } from 'primeng/card';
import { Button } from 'primeng/button';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  imports: [Card, Button, CommonModule],
  template: `
    <div class="login-wrapper">
      <p-card header="Đăng nhập hệ thống EVNHANOI" [style]="{ width: '400px' }">
        <div style="display: flex; flex-direction: column; gap: 1rem;">
          <div *ngIf="error" style="color: red; font-size: 0.875rem;">{{ error }}</div>
          
          <p-button label="Đăng nhập với EVN SSO" (onClick)="onSsoLogin()" [loading]="loading" styleClass="w-full" [style]="{'width':'100%', 'margin-top':'0.5rem', 'background-color': '#005b9f'}"></p-button>
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
  error = '';
  
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

  verifySsoTicket(ticket: string) {
    this.loading = true;
    this.error = '';
    
    this.http.post(`${environment.apiGatewayUrl}/api/v1/auth/login?ticket=${ticket}`, {}).subscribe({
      next: (res: any) => {
        this.loading = false;
        const token = res.token || res.access_token || res.AccessToken || res.data?.token || res.data?.access_token;
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
