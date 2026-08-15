import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { finalize } from 'rxjs';
import { AuthService } from '@sohoa.frontend/shared/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';

type PasswordField = 'currentPassword' | 'newPassword' | 'confirmPassword';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss'
})
export class ChangePasswordComponent implements OnInit {
  private authService = inject(AuthService);
  private messageService = inject(MessageService);
  private router = inject(Router);

  currentPassword = signal('');
  newPassword = signal('');
  confirmPassword = signal('');
  submitted = signal(false);
  saving = signal(false);
  serverErrors = signal<Record<string, string>>({});
  breadcrumbItems = [
    { label: 'Thông tin cá nhân', url: '/profile' },
    { label: 'Đổi mật khẩu', url: '/profile/change-password' }
  ];
  visible = signal<Record<PasswordField, boolean>>({
    currentPassword: false,
    newPassword: false,
    confirmPassword: false
  });

  ngOnInit(): void {
    if (this.authService.isSsoUser()) {
      this.authService.redirectToSsoChangePassword();
    }
  }

  currentPasswordError = computed(() => {
    if (this.submitted() && !this.currentPassword().trim()) {
      return 'Trường dữ liệu này không được để trống';
    }
    return this.serverErrors()['currentPassword'] || this.serverErrors()['CurrentPassword'] || '';
  });

  newPasswordError = computed(() => {
    const value = this.newPassword();
    if (this.submitted() && !value) {
      return 'Trường dữ liệu này không được để trống';
    }
    if (this.submitted() && value && value.length < 8) {
      return 'Mật khẩu mới phải có tối thiểu 8 ký tự';
    }
    if (this.submitted() && value && !/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,}$/.test(value)) {
      return 'Mật khẩu mới phải bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt';
    }
    if (this.submitted() && value && this.currentPassword() && value === this.currentPassword()) {
      return 'Mật khẩu mới không được trùng mật khẩu hiện tại';
    }
    return this.serverErrors()['newPassword'] || this.serverErrors()['NewPassword'] || '';
  });

  confirmPasswordError = computed(() => {
    const value = this.confirmPassword();
    if (this.submitted() && !value) {
      return 'Trường dữ liệu này không được để trống';
    }
    if (this.submitted() && value && value !== this.newPassword()) {
      return 'Xác nhận mật khẩu mới không khớp';
    }
    return this.serverErrors()['confirmPassword'] || this.serverErrors()['ConfirmPassword'] || '';
  });

  toggleVisible(field: PasswordField): void {
    this.visible.update(state => ({ ...state, [field]: !state[field] }));
  }

  setValue(field: PasswordField, value: string): void {
    const setters: Record<PasswordField, (next: string) => void> = {
      currentPassword: next => this.currentPassword.set(next),
      newPassword: next => this.newPassword.set(next),
      confirmPassword: next => this.confirmPassword.set(next)
    };
    setters[field](value);
    this.serverErrors.update(errors => {
      const copy = { ...errors };
      delete copy[field];
      delete copy[field.charAt(0).toUpperCase() + field.slice(1)];
      return copy;
    });
  }

  onSubmit(): void {
    this.submitted.set(true);
    this.serverErrors.set({});

    if (this.currentPasswordError() || this.newPasswordError() || this.confirmPasswordError()) {
      return;
    }

    this.saving.set(true);
    this.authService.changePassword({
      currentPassword: this.currentPassword(),
      newPassword: this.newPassword(),
      confirmPassword: this.confirmPassword()
    })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Đổi mật khẩu thành công. Vui lòng đăng nhập lại.'
          });
          setTimeout(() => {
            this.authService.logout();
            this.router.navigate(['/login']);
          }, 900);
        },
        error: (err) => {
          this.serverErrors.set(this.extractErrors(err));
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể đổi mật khẩu.'
          });
        }
      });
  }

  onCancel(): void {
    this.router.navigate(['/profile']);
  }

  private extractErrors(err: any): Record<string, string> {
    const error = err?.error;
    if (!error) return {};
    if (error.errors && typeof error.errors === 'object') return error.errors;
    if (typeof error === 'object') return error;
    return {};
  }
}
