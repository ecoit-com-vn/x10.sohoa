import { CommonModule } from '@angular/common';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
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
export class UserProfileComponent implements OnInit, OnDestroy {
  @ViewChild('avatarInput') avatarInput?: ElementRef<HTMLInputElement>;

  private authService = inject(AuthService);
  private userService = inject(UserService);
  private messageService = inject(MessageService);
  private router = inject(Router);

  profile = signal<UserProfile | null>(null);
  positions = signal<any[]>([]);
  loading = signal(false);
  saving = signal(false);
  avatarSaving = signal(false);
  avatarDeleting = signal(false);
  submitted = signal(false);
  serverErrors = signal<Record<string, string>>({});
  avatarPreviewUrl = signal<string | null>(null);
  selectedAvatarFile = signal<File | null>(null);
  avatarObjectUrl = signal<string | null>(null);
  avatarError = signal('');
  breadcrumbItems = [{ label: 'Thông tin cá nhân', url: '/profile' }];
  avatarMenuOpen = signal(false);

  displayAvatarUrl = computed(() => this.avatarPreviewUrl() || this.avatarObjectUrl());
  initials = computed(() => {
    const name = this.profile()?.fullName?.trim() || this.profile()?.username?.trim() || '';
    return name
      .split(/\s+/)
      .slice(-2)
      .map(part => part.charAt(0).toUpperCase())
      .join('') || 'U';
  });

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

  ngOnDestroy(): void {
    this.revokeAvatarUrls();
  }

  @HostListener('document:click')
  closeAvatarMenu(): void {
    this.avatarMenuOpen.set(false);
  }

  loadProfile(): void {
    this.loading.set(true);
    this.authService.getProfile()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (profile) => {
          this.profile.set({ ...profile });
          this.loadAvatarImage(profile);
        },
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

  openAvatarPicker(): void {
    this.avatarMenuOpen.set(false);
    this.avatarInput?.nativeElement.click();
  }

  toggleAvatarMenu(event: Event): void {
    event.stopPropagation();
    this.avatarMenuOpen.update(open => !open);
  }

  async onAvatarSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    this.avatarError.set('');

    if (!file) {
      return;
    }

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      this.avatarError.set('Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.');
      return;
    }

    try {
      const resized = await this.resizeAvatar(file);
      this.selectedAvatarFile.set(resized);
      this.setPreviewUrl(URL.createObjectURL(resized));
      this.uploadSelectedAvatar();
    } catch {
      this.avatarError.set('Không thể xử lý ảnh đã chọn.');
    }
  }

  uploadSelectedAvatar(): void {
    const file = this.selectedAvatarFile();
    if (!file) {
      return;
    }

    this.avatarMenuOpen.set(false);
    this.avatarSaving.set(true);
    this.authService.uploadAvatar(file)
      .pipe(finalize(() => this.avatarSaving.set(false)))
      .subscribe({
        next: () => {
          this.clearSelectedAvatar(false);
          this.loadProfile();
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Cập nhật ảnh đại diện thành công.'
          });
        },
        error: (err) => {
          this.avatarError.set(err?.error?.message || 'Không thể cập nhật ảnh đại diện.');
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể cập nhật ảnh đại diện.'
          });
        }
      });
  }

  clearSelectedAvatar(revoke = true): void {
    this.selectedAvatarFile.set(null);
    if (revoke && this.avatarPreviewUrl()) {
      URL.revokeObjectURL(this.avatarPreviewUrl()!);
    }
    this.avatarPreviewUrl.set(null);
    this.avatarError.set('');
    this.avatarMenuOpen.set(false);
  }

  deleteAvatar(): void {
    if (!this.profile()?.avatarObjectKey || this.avatarDeleting()) {
      return;
    }

    this.avatarMenuOpen.set(false);
    this.avatarDeleting.set(true);
    this.authService.deleteAvatar()
      .pipe(finalize(() => this.avatarDeleting.set(false)))
      .subscribe({
        next: () => {
          this.clearSelectedAvatar();
          this.revokeCurrentAvatarUrl();
          this.profile.update(profile => profile ? { ...profile, avatarObjectKey: null, avatarUrl: null } : profile);
          this.messageService.add({
            severity: 'success',
            summary: 'Thành công',
            detail: 'Xóa ảnh đại diện thành công.'
          });
        },
        error: (err) => {
          this.avatarError.set(err?.error?.message || 'Không thể xóa ảnh đại diện.');
          this.messageService.add({
            severity: 'error',
            summary: 'Lỗi',
            detail: err?.error?.message || 'Không thể xóa ảnh đại diện.'
          });
        }
      });
  }

  goToChangePassword(): void {
    this.router.navigate(['/profile/change-password']);
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

  private loadAvatarImage(profile: UserProfile): void {
    this.revokeCurrentAvatarUrl();
    if (!profile.avatarObjectKey) {
      return;
    }

    this.authService.getAvatarBlob().subscribe({
      next: (blob) => this.avatarObjectUrl.set(URL.createObjectURL(blob)),
      error: () => this.avatarObjectUrl.set(null)
    });
  }

  private resizeAvatar(file: File): Promise<File> {
    const maxSize = 512;

    return new Promise((resolve, reject) => {
      const image = new Image();
      const sourceUrl = URL.createObjectURL(file);

      image.onload = () => {
        const scale = Math.min(maxSize / image.width, maxSize / image.height, 1);
        const width = Math.round(image.width * scale);
        const height = Math.round(image.height * scale);
        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext('2d');

        if (!context) {
          URL.revokeObjectURL(sourceUrl);
          reject();
          return;
        }

        context.drawImage(image, 0, 0, width, height);
        canvas.toBlob(blob => {
          URL.revokeObjectURL(sourceUrl);
          if (!blob) {
            reject();
            return;
          }

          const extension = file.type === 'image/png' ? 'png' : file.type === 'image/webp' ? 'webp' : 'jpg';
          resolve(new File([blob], `avatar.${extension}`, { type: blob.type || file.type }));
        }, file.type === 'image/png' || file.type === 'image/webp' ? file.type : 'image/jpeg', 0.88);
      };

      image.onerror = () => {
        URL.revokeObjectURL(sourceUrl);
        reject();
      };

      image.src = sourceUrl;
    });
  }

  private setPreviewUrl(url: string): void {
    if (this.avatarPreviewUrl()) {
      URL.revokeObjectURL(this.avatarPreviewUrl()!);
    }
    this.avatarPreviewUrl.set(url);
  }

  private revokeAvatarUrls(): void {
    this.revokeCurrentAvatarUrl();
    if (this.avatarPreviewUrl()) {
      URL.revokeObjectURL(this.avatarPreviewUrl()!);
      this.avatarPreviewUrl.set(null);
    }
  }

  private revokeCurrentAvatarUrl(): void {
    if (this.avatarObjectUrl()) {
      URL.revokeObjectURL(this.avatarObjectUrl()!);
      this.avatarObjectUrl.set(null);
    }
  }

  private extractErrors(err: any): Record<string, string> {
    const error = err?.error;
    if (!error) return {};
    if (error.errors && typeof error.errors === 'object') return error.errors;
    if (typeof error === 'object') return error;
    return {};
  }
}
