import { Component, inject } from '@angular/core';
import { Card } from 'primeng/card';
import { InputText } from 'primeng/inputtext';
import { Password } from 'primeng/password';
import { Button } from 'primeng/button';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-login',
  imports: [Card, InputText, Password, Button, FormsModule, CommonModule],
  template: `
    <div class="login-wrapper">
      <p-card header="Đăng nhập hệ thống EVNHANOI" [style]="{ width: '400px' }">
        <div style="display: flex; flex-direction: column; gap: 1rem;">
          <div *ngIf="error" style="color: red; font-size: 0.875rem;">{{ error }}</div>
          <div>
            <label for="username" style="display: block; margin-bottom: 0.5rem; font-weight: 500;">Tên đăng nhập</label>
            <input pInputText id="username" [(ngModel)]="username" style="width: 100%" />
          </div>
          <div>
            <label for="password" style="display: block; margin-bottom: 0.5rem; font-weight: 500;">Mật khẩu</label>
            <p-password id="password" [(ngModel)]="password" [toggleMask]="true" styleClass="w-full" [inputStyle]="{'width':'100%'}" [feedback]="false"></p-password>
          </div>
          <p-button label="Đăng nhập" (onClick)="onLogin()" [loading]="loading" styleClass="w-full" [style]="{'width':'100%', 'margin-top':'0.5rem'}"></p-button>
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
export class Login {
  username = '';
  password = '';
  loading = false;
  error = '';
  
  private router = inject(Router);
  private http = inject(HttpClient);
  
  onLogin() {
    if (!this.username || !this.password) {
      this.error = 'Vui lòng nhập tên đăng nhập và mật khẩu';
      return;
    }
    
    this.loading = true;
    this.error = '';
    
    this.http.post('http://localhost:5000/api/v1/auth/login', {
      username: this.username,
      password: this.password
    }).subscribe({
      next: (res: any) => {
        this.loading = false;
        const token = res.token || res.access_token || res.data?.token || res.data?.access_token;
        if (token) {
          localStorage.setItem('token', token);
          this.router.navigate(['/']); // Redirect to AdminLayout
        } else {
          this.error = 'Phản hồi không chứa token đăng nhập';
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = 'Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.';
        console.error('Login error', err);
      }
    });
  }
}
