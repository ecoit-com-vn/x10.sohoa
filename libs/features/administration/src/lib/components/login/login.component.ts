import { Component, inject, OnInit, afterNextRender } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class Login implements OnInit {
  loading = false;
  localLoading = false;
  error = '';
  username = '';
  password = '';
  showPassword = false;
  rememberMe = false;
  
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private authService = inject(AuthService);

  constructor() {
    afterNextRender(() => {
      const savedUsername = localStorage.getItem('rememberedUsername');
      const savedRememberMe = localStorage.getItem('rememberMe');
      if (savedRememberMe === 'true' && savedUsername) {
        this.username = savedUsername;
        this.rememberMe = true;
      }
    });
  }
  
  ngOnInit() {
    if (typeof window !== 'undefined') {
      this.route.queryParams.subscribe(params => {
        const ticket = params['ticket'];
        if (ticket) {
          this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { ticket: null },
            queryParamsHandling: 'merge',
            replaceUrl: true
          });
          this.verifySsoTicket(ticket);
        }
      });
    }
  }

  private persistRememberMe(): void {
    if (typeof window === 'undefined') return;
    if (this.rememberMe && this.username.trim()) {
      localStorage.setItem('rememberedUsername', this.username.trim());
      localStorage.setItem('rememberMe', 'true');
    } else {
      localStorage.removeItem('rememberedUsername');
      localStorage.removeItem('rememberMe');
    }
  }

  onSsoLogin() {
    this.persistRememberMe();
    this.authService.redirectToSso();
  }

  onLocalLogin(event: Event) {
    event.preventDefault();
    if (!this.username || !this.password) {
      this.error = 'Vui lòng nhập đầy đủ tài khoản và mật khẩu';
      return;
    }
    
    this.localLoading = true;
    this.error = '';

    this.authService.loginLocal(this.username, this.password).subscribe({
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
            this.persistRememberMe();
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
    
    this.authService.verifySsoTicket(ticket).subscribe({
      next: (res: any) => {
        this.loading = false;
        const token = res.accessToken || res.AccessToken || res.access_token || res.token || res.data?.token || res.data?.access_token;
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
          this.error = 'Phản hồi không chứa token đăng nhập';
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.message || 'Đăng nhập SSO thất bại. Vui lòng thử lại.';
        console.error('SSO Login error', err);
      }
    });
  }
}
