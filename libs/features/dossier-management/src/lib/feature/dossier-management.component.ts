import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { WfBreadcrumbComponent } from '../../../../../shared/layout/src/lib/components/common/wf-breadcrumb/wf-breadcrumb.component';
import { viewChild } from '@angular/core';

import { CommonModule } from '@angular/common';

import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';

import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { filter } from 'rxjs';

import { DossierListComponent } from './dossier-list/dossier-list.component';
import { DossierFormComponent } from './dossier-form/dossier-form.component';
import { DossierDetailComponent } from './dossier-detail/dossier-detail.component';
import { DossierPublishComponent } from './dossier-publish/dossier-publish.component';
import { DossierManagementService } from '../data-access/dossier-management.service';
import { DossierReportContext } from '../data-access/dossier-report-context.service';
import { AuthService } from '@sohoa.frontend/shared/core';
import { hasDossierCreatePermission } from '../utils/dossier-permission.util';

import { DossierMenuScope } from '../utils/dossier-status.util';



@Component({

  selector: 'app-dossier-management',

  standalone: true,

  imports: [CommonModule, DossierListComponent, DossierFormComponent, DossierDetailComponent, DossierPublishComponent, WfBreadcrumbComponent],

  templateUrl: './dossier-management.component.html',

  styleUrl: './dossier-management.component.scss',

})

export class DossierManagementComponent implements OnInit {

  private dossierList = viewChild(DossierListComponent);
  private dossierPublish = viewChild(DossierPublishComponent);
  dossierForm = viewChild(DossierFormComponent);
  dossierDetail = viewChild(DossierDetailComponent);

  private router = inject(Router);

  private route = inject(ActivatedRoute);

  private destroyRef = inject(DestroyRef);



  private dossierService = inject(DossierManagementService);
  private reportContext = inject(DossierReportContext);
  private authService = inject(AuthService);

  currentView = signal<'list' | 'form' | 'detail'>('list');
  selectedDossierId = signal<string | null>(null);
  inputCompleted = signal(false);
  menuScope = signal<DossierMenuScope>('creator');
  kindId = signal<number>(2);
  /** Nguồn mở hồ sơ từ route: 'report' khi URL có ?from=report. */
  menuFrom = signal<string | null>(null);
  /** Chờ sync route trước khi mount list — chỉ cần cho digitization (kindId=1); hồ sơ mới mặc định kindId=2. */
  routeReady = signal(false);
  listTitle = signal('Quản lý hồ sơ');
  breadcrumbItems = computed(() => {
    const title = this.listTitle();

    return title === 'Quản lý hồ sơ'
      ? [{ label: title }]
      : [{ label: 'Quản lý hồ sơ' }, { label: title }];
  });

  showHeaderCreateButton = computed(() => {
    if (this.currentView() !== 'list') return false;
    if (this.menuScope() === 'publisher') return true;
    if (this.menuScope() !== 'creator') return false;

    const list = this.dossierList();
    if (!list || !hasDossierCreatePermission(this.authService, this.kindId() === 1)) return false;

    return this.kindId() === 1 || list.activeTab() === 'draft';
  });

  showHeaderImportActions = computed(() => {
    const list = this.dossierList();
    if (!list) return false;
    return this.currentView() === 'list'
      && this.menuScope() === 'creator'
      && (this.kindId() === 1 || list.activeTab() === 'draft')
      && list.canCreateDossier();
  });



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
    this.routeReady.set(true);

    this.route.url.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.syncViewFromRoute());



    this.router.events

      .pipe(

        filter((event): event is NavigationEnd => event instanceof NavigationEnd),

        takeUntilDestroyed(this.destroyRef)

      )

      .subscribe(() => this.syncViewFromRoute());

  }



  private routeSegments(): string[] {
    const scope = this.menuScope();
    if (this.kindId() === 1) {
      return scope === 'approver' ? ['digitization', 'approve'] : ['digitization', 'my-dossiers'];
    }
    if (scope === 'approver') return ['approve'];
    if (scope === 'publisher') return ['publish'];
    return ['my-dossiers'];
  }

  private routeBasePath(): string {
    return `/dossier-management/${this.routeSegments().join('/')}`;
  }

  private syncViewFromRoute(): void {
    let node = this.route.snapshot;
    while (node.firstChild) node = node.firstChild;

    const scope = (node.data['menuScope'] as DossierMenuScope) ?? 'creator';
    const kind = (node.data['kindId'] as number) ?? 2;
    this.menuScope.set(scope);
    this.kindId.set(kind);
    this.menuFrom.set(node.queryParamMap.get('from'));
    this.reportContext.setFrom(node.queryParamMap.get('from'));
    this.dossierService.setKindContext(kind);
    this.listTitle.set((node.data['listTitle'] as string) ?? 'Quản lý hồ sơ');

    const root = this.routeSegments().join('/');
    const routePath = node.routeConfig?.path ?? '';
    const id = node.paramMap.get('id');
    const url = this.router.url.split('?')[0];
    const basePath = this.routeBasePath();

    if (routePath === this.routeSegments().slice(-1)[0] || url === basePath || url.endsWith(basePath)) {
      this.selectedDossierId.set(null);
      this.currentView.set('list');
      return;
    }

    if (
      routePath === `${root}/new` ||
      routePath === `${root}/add` ||
      url.endsWith(`/${root}/new`) ||
      url.endsWith(`/${root}/add`)
    ) {
      this.selectedDossierId.set(null);
      this.currentView.set('form');
      return;
    }

    if (
      (routePath === `${root}/:id/edit` || (url.includes(`/${root}/`) && url.endsWith('/edit')))
      && id
    ) {
      this.selectedDossierId.set(id);
      this.currentView.set('form');
      return;
    }

    if ((routePath === `${root}/:id` || id) && id) {
      this.selectedDossierId.set(id);
      this.currentView.set('detail');
      return;
    }

    const urlMatch = url.match(new RegExp(`/${root}/([^/]+)(?:/edit)?$`));
    if (urlMatch) {
      const segment = urlMatch[1];
      if (segment === 'new') {
        this.selectedDossierId.set(null);
        this.currentView.set('form');
      } else if (url.endsWith('/edit')) {
        this.selectedDossierId.set(segment);
        this.currentView.set('form');
      } else {
        this.selectedDossierId.set(segment);
        this.currentView.set('detail');
      }
      return;
    }

    this.selectedDossierId.set(null);
    this.currentView.set('list');
  }



  onViewDetail(id: string): void {

    void this.router.navigate(['/dossier-management', ...this.routeSegments(), id]);

  }



  onEdit(id: string): void {

    this.inputCompleted.set(false);

    if (this.menuScope() !== 'creator' && this.menuScope() !== 'publisher') return;

    void this.router.navigate(['/dossier-management', ...this.routeSegments(), id, 'edit']);

  }

  onExportTemplate(): void {
    this.dossierList()?.onExportTemplate();
  }

  onExportExcel(): void {
    if (this.menuScope() === 'publisher') {
      this.dossierPublish()?.onExportExcel();
    } else {
      this.dossierList()?.onExportExcel();
    }
  }

  isExportingExcel(): boolean {
    if (this.menuScope() === 'publisher') {
      return this.dossierPublish()?.exporting() ?? false;
    }
    return this.dossierList()?.exporting() ?? false;
  }

  onFileSelected(event: Event): void {
    this.dossierList()?.onFileSelected(event);
  }



  onCreate(): void {

    this.inputCompleted.set(false);

    const isPublisher = this.menuScope() === 'publisher';
    if (this.menuScope() !== 'creator' && !isPublisher) return;

    void this.router.navigate([
      '/dossier-management',
      ...this.routeSegments(),
      isPublisher ? 'add' : 'new'
    ]);

  }



  onBackToList(): void {

    this.inputCompleted.set(false);

    void this.router.navigate(['/dossier-management', ...this.routeSegments()]);

  }



  onSaved(id: string): void {
    void this.router.navigate(['/dossier-management', ...this.routeSegments(), id]);

  }

}



