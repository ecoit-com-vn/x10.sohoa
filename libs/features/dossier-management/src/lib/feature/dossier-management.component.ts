import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DossierListComponent } from './dossier-list/dossier-list.component';
import { DossierFormComponent } from './dossier-form/dossier-form.component';
import { DossierDetailComponent } from './dossier-detail/dossier-detail.component';

@Component({
  selector: 'app-dossier-management',
  standalone: true,
  imports: [CommonModule, DossierListComponent, DossierFormComponent, DossierDetailComponent],
  template: `
    <div class="wf-page">
      <!-- Breadcrumb -->
      <div class="breadcrumb">
        <i class="pi pi-home bc-icon"></i>
        <span class="bc-text">Trang chủ</span>
        <span class="bc-sep">/</span>
        <span class="bc-text">Nghiệp vụ hồ sơ</span>
        <span class="bc-sep">/</span>
        <span class="bc-current">{{ breadcrumbCurrent() }}</span>
      </div>

      <!-- List View -->
      <app-dossier-list 
        *ngIf="currentView() === 'list'" 
        (viewDetail)="onViewDetail($event)"
        (edit)="onEdit($event)"
        (create)="onCreate()">
      </app-dossier-list>

      <!-- Form View (Create/Edit basic info) -->
      <app-dossier-form 
        *ngIf="currentView() === 'form'"
        [dossierId]="selectedDossierId()"
        (cancel)="onBackToList()"
        (saved)="onSaved($event)">
      </app-dossier-form>

      <!-- Detail View (Tabs: Info, Docs, Workflow) -->
      <app-dossier-detail 
        *ngIf="currentView() === 'detail'"
        [dossierId]="selectedDossierId()!"
        (cancel)="onBackToList()">
      </app-dossier-detail>
    </div>
  `,
  styles: []
})
export class DossierManagementComponent {
  currentView = signal<'list' | 'form' | 'detail'>('list');
  selectedDossierId = signal<string | null>(null);

  breadcrumbCurrent = computed(() => {
    switch (this.currentView()) {
      case 'form': return this.selectedDossierId() ? 'Cập nhật hồ sơ' : 'Tạo hồ sơ mới';
      case 'detail': return 'Chi tiết hồ sơ';
      default: return 'Quản lý hồ sơ';
    }
  });

  onViewDetail(id: string) {
    this.selectedDossierId.set(id);
    this.currentView.set('detail');
  }

  onEdit(id: string) {
    this.selectedDossierId.set(id);
    this.currentView.set('form');
  }

  onCreate() {
    this.selectedDossierId.set(null);
    this.currentView.set('form');
  }

  onBackToList() {
    this.currentView.set('list');
    this.selectedDossierId.set(null);
  }

  onSaved(id: string) {
    this.onViewDetail(id); // After save, navigate to detail
  }
}
