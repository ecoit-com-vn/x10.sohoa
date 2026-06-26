import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
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
        (create)="onCreate()"
      ></app-dossier-list>

      <!-- Form View (Create/Edit basic info) -->
      <app-dossier-form
        *ngIf="currentView() === 'form'"
        [dossierId]="selectedDossierId()"
        (cancel)="onBackToList()"
        (saved)="onSaved($event)"
      ></app-dossier-form>

      <!-- Detail View (Tabs: Info, Docs, Workflow) -->
      <app-dossier-detail
        *ngIf="currentView() === 'detail' && selectedDossierId()"
        [dossierId]="selectedDossierId()!"
        (cancel)="onBackToList()"
        (edit)="onEdit(selectedDossierId()!)"
      ></app-dossier-detail>
    </div>
  `,
  styles: [],
})
export class DossierManagementComponent implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);

  currentView = signal<'list' | 'form' | 'detail'>('list');
  selectedDossierId = signal<string | null>(null);

  breadcrumbCurrent = computed(() => {
    switch (this.currentView()) {
      case 'form':
        return this.selectedDossierId() ? 'Cập nhật hồ sơ' : 'Tạo hồ sơ mới';
      case 'detail':
        return 'Chi tiết hồ sơ';
      default:
        return 'Quản lý hồ sơ';
    }
  });

  ngOnInit(): void {
    this.syncViewFromRoute();

    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.syncViewFromRoute());

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.syncViewFromRoute());
  }

  private syncViewFromRoute(): void {
    const configPath = this.route.snapshot.routeConfig?.path ?? '';
    const id = this.route.snapshot.paramMap.get('id');

    switch (configPath) {
      case 'new':
        this.selectedDossierId.set(null);
        this.currentView.set('form');
        break;
      case ':id/edit':
        this.selectedDossierId.set(id);
        this.currentView.set('form');
        break;
      case ':id':
        this.selectedDossierId.set(id);
        this.currentView.set('detail');
        break;
      default:
        this.selectedDossierId.set(null);
        this.currentView.set('list');
        break;
    }
  }

  onViewDetail(id: string): void {
    void this.router.navigate(['/dossier-management', id]);
  }

  onEdit(id: string): void {
    void this.router.navigate(['/dossier-management', id, 'edit']);
  }

  onCreate(): void {
    void this.router.navigate(['/dossier-management', 'new']);
  }

  onBackToList(): void {
    void this.router.navigate(['/dossier-management']);
  }

  onSaved(id: string): void {
    void this.router.navigate(['/dossier-management', id]);
  }
}
