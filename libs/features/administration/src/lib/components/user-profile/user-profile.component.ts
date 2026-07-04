import { CommonModule } from '@angular/common';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { finalize } from 'rxjs';
import { AuthService, UserProfile } from '@sohoa.frontend/shared/core';
import { UserService } from '../../services/user.service';

@Component({
  selector: 'app-user-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, ToastModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './user-profile.component.html',
  styleUrl: './user-profile.component.scss'
})
export class UserProfileComponent implements OnInit {
  private authService = inject(AuthService);
  private userService = inject(UserService);
  private messageService = inject(MessageService);
  private router = inject(Router);

  profile = signal<UserProfile | null>(null);
  positions = signal<any[]>([]);
  loading = signal(false);
  saving = signal(false);
  submitted = signal(false);
  serverErrors = signal<Record<string, string>>({});

  fullNameError = computed(() => {
    if (this.submitted() && !this.profile()?.fullName?.trim()) {
      return 'Trường dữ liệu này không được để trống';
    }
    return this.serverErrors()['fullName'] || this.serverErrors()['FullName'] || '';
  });

  emailError = computed(() => {
    const email = this.profile()?.email?.trim() || '';
    if (this.submitted() && !email) {
      return 'Trường dữ liệu này không được để trống';
    }
    if (this.submitted() && email && !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) {
      return 'Email không đúng định dạng';
    }
    return this.serverErrors()['email'] || this.serverErrors()['Email'] || '';
  });

  ngOnInit(): void {
    this.loadProfile();
    this.loadPositions();
  }

  loadProfile(): void {
    this.loading.set(true);
    this.authService.getProfile()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (profile) => this.profile.set({ ...profile }),
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: 'Không thể tải thông tin cá nhân.'
          });
        }
      });
  }

  loadPositions(): void {
    this.userService.getCatalogTypes().subscribe({
      next: (types: any[]) => {
        const list = Array.isArray(types) ? types : [];
        const chucVuType = list.find(t => t.code === 'CHUC_VU' || t.Code === 'CHUC_VU');
        const typeId = chucVuType?.id || chucVuType?.Id;
        if (!typeId) {
          this.positions.set([]);
          return;
        }

        this.userService.getPositions(typeId).subscribe({
          next: (items: any[]) => this.positions.set(Array.isArray(items) ? items : []),
          error: () => this.positions.set([])
        });
      },
      error: () => this.positions.set([])
    });
  }

  setField(field: keyof UserProfile, value: any): void {
    this.profile.update(profile => profile ? { ...profile, [field]: value } : profile);
    this.serverErrors.update(errors => {
      const copy = { ...errors };
      delete copy[field as string];
      delete copy[(field as string).charAt(0).toUpperCase() + (field as string).slice(1)];
      return copy;
    });
  }

  getOrganizationName(): string {
    const profile = this.profile();
    return profile?.organizationUnit?.name || '';
  }

  onSave(): void {
    this.submitted.set(true);
    this.serverErrors.set({});

    const profile = this.profile();
    if (!profile || this.fullNameError() || this.emailError()) {
      return;
    }

    const selectedPosition = this.positions().find(p => p.id == profile.positionId);
    const dto = {
      fullName: profile.fullName.trim(),
      email: profile.email.trim(),
      positionId: profile.positionId || null,
      positionName: selectedPosition ? selectedPosition.name : null
    };

    this.saving.set(true);
    this.authService.updateProfile(dto)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (updated) => {
          this.profile.set({ ...updated });
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Cập nhật thông tin cá nhân thành công.'
          });
        },
        error: (err) => {
          this.serverErrors.set(this.extractErrors(err));
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể cập nhật thông tin cá nhân.'
          });
        }
      });
  }

  onCancel(): void {
    this.router.navigate(['/dashboard']);
  }

  private extractErrors(err: any): Record<string, string> {
    const error = err?.error;
    if (!error) return {};
    if (error.errors && typeof error.errors === 'object') return error.errors;
    if (typeof error === 'object') return error;
    return {};
  }
}
