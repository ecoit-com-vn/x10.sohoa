import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';

import { CommonModule } from '@angular/common';

import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';

import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { filter } from 'rxjs';

import { DossierListComponent } from './dossier-list/dossier-list.component';
import { DossierFormComponent } from './dossier-form/dossier-form.component';
import { DossierDetailComponent } from './dossier-detail/dossier-detail.component';
import { DossierPublishComponent } from './dossier-publish/dossier-publish.component';

import { DossierMenuScope } from '../utils/dossier-status.util';



@Component({

  selector: 'app-dossier-management',

  standalone: true,

  imports: [CommonModule, DossierListComponent, DossierFormComponent, DossierDetailComponent, DossierPublishComponent],

  template: `

    <div class="wf-page">

      <div class="breadcrumb">

        <i class="pi pi-home bc-icon"></i>

        <span class="bc-text">Trang chủ</span>

        <span class="bc-sep">/</span>

        <span class="bc-text">Nghiệp vụ hồ sơ</span>

        <span class="bc-sep">/</span>

        <span class="bc-current">{{ breadcrumbCurrent() }}</span>

      </div>



      <app-dossier-list
        *ngIf="currentView() === 'list' && menuScope() !== 'publisher'"
        [menuScope]="menuScope()"
        (viewDetail)="onViewDetail($event)"
        (edit)="onEdit($event)"
        (create)="onCreate()"
      ></app-dossier-list>

      <app-dossier-publish
        *ngIf="currentView() === 'list' && menuScope() === 'publisher'"
        (viewDetail)="onViewDetail($event)"
      ></app-dossier-publish>



      <app-dossier-form

        *ngIf="currentView() === 'form'"

        [dossierId]="selectedDossierId()"

        (cancel)="onBackToList()"

        (saved)="onSaved($event)"

      ></app-dossier-form>



      <app-dossier-detail

        *ngIf="currentView() === 'detail' && selectedDossierId()"

        [dossierId]="selectedDossierId()!"

        [menuScope]="menuScope()"

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

  menuScope = signal<DossierMenuScope>('creator');

  listTitle = signal('Quản lý hồ sơ');



  breadcrumbCurrent = computed(() => {

    switch (this.currentView()) {

      case 'form':

        return this.selectedDossierId() ? 'Cập nhật hồ sơ' : 'Tạo hồ sơ mới';

      case 'detail':

        return 'Chi tiết hồ sơ';

      default:

        return this.listTitle();

    }

  });



  ngOnInit(): void {

    this.syncViewFromRoute();



    this.route.url.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.syncViewFromRoute());



    this.router.events

      .pipe(

        filter((event): event is NavigationEnd => event instanceof NavigationEnd),

        takeUntilDestroyed(this.destroyRef)

      )

      .subscribe(() => this.syncViewFromRoute());

  }



  private routePrefix(): string {
    const scope = this.menuScope();
    if (scope === 'approver') return 'approve';
    if (scope === 'publisher') return 'publish';
    return 'my-dossiers';
  }



  private syncViewFromRoute(): void {

    const scope = (this.route.snapshot.data['menuScope'] as DossierMenuScope) ?? 'creator';

    this.menuScope.set(scope);

    this.listTitle.set((this.route.snapshot.data['listTitle'] as string) ?? 'Quản lý hồ sơ');



    const segments = this.route.snapshot.url.map((s) => s.path);

    const root = this.routePrefix();



    if (segments.length === 1 && segments[0] === root) {

      this.selectedDossierId.set(null);

      this.currentView.set('list');

      return;

    }



    if (scope === 'creator' && segments.length === 2 && segments[0] === root && segments[1] === 'new') {

      this.selectedDossierId.set(null);

      this.currentView.set('form');

      return;

    }



    if (scope === 'creator' && segments.length === 3 && segments[0] === root && segments[2] === 'edit') {

      this.selectedDossierId.set(segments[1]);

      this.currentView.set('form');

      return;

    }



    if (segments.length === 2 && segments[0] === root) {

      this.selectedDossierId.set(segments[1]);

      this.currentView.set('detail');

      return;

    }



    this.selectedDossierId.set(null);

    this.currentView.set('list');

  }



  onViewDetail(id: string): void {

    void this.router.navigate(['/dossier-management', this.routePrefix(), id]);

  }



  onEdit(id: string): void {

    if (this.menuScope() !== 'creator') return;

    void this.router.navigate(['/dossier-management', this.routePrefix(), id, 'edit']);

  }



  onCreate(): void {

    if (this.menuScope() !== 'creator') return;

    void this.router.navigate(['/dossier-management', this.routePrefix(), 'new']);

  }



  onBackToList(): void {

    void this.router.navigate(['/dossier-management', this.routePrefix()]);

  }



  onSaved(id: string): void {

    void this.router.navigate(['/dossier-management', this.routePrefix(), id]);

  }

}


