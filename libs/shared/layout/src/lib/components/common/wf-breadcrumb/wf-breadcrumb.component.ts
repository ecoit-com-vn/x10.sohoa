import {
  Component,
  DestroyRef,
  EventEmitter,
  Input,
  OnInit,
  Output,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter } from 'rxjs';
import { BreadcrumbService, BreadcrumbTrailItem } from '@sohoa.frontend/shared/core';

type BreadcrumbViewMode = 'list' | 'add' | 'edit' | 'detail' | 'form' | string;

@Component({
  selector: 'wf-breadcrumb',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './wf-breadcrumb.component.html',
})
export class WfBreadcrumbComponent implements OnInit {
  private router = inject(Router);
  private breadcrumbService = inject(BreadcrumbService);
  private destroyRef = inject(DestroyRef);

  /** Hậu tố tùy chỉnh (ưu tiên hơn viewMode). */
  @Input() suffix: string | null = null;
  /** Tự sinh hậu tố: add → Thêm mới, edit → Chỉnh sửa, detail → Chi tiết. */
  @Input() viewMode: BreadcrumbViewMode | null = null;
  /** Ghi đè nhãn mục cuối (vd. listTitle từ route data). */
  @Input() leafLabel: string | null = null;
  /** Ghi đè URL dùng để khớp menu (mặc định lấy router.url). */
  @Input() matchUrl: string | null = null;

  @Input() customItems: BreadcrumbTrailItem[] | null = null;
  
  @Output() listClick = new EventEmitter<void>();

  private trail = signal<BreadcrumbTrailItem[]>([]);

  readonly items = computed(() => {
    const trail = this.customItems?.length ? [...this.customItems] : [...this.trail()];
    if (!trail.length) {
      return trail;
    }

    if (this.leafLabel?.trim()) {
      trail[trail.length - 1] = {
        ...trail[trail.length - 1],
        label: this.leafLabel.trim(),
      };
    }

    return trail;
  });

  readonly resolvedSuffix = computed(() => {
    if (this.suffix?.trim()) {
      return this.suffix.trim();
    }

    switch (this.viewMode) {
      case 'add':
        return 'Thêm mới';
      case 'edit':
        return 'Chỉnh sửa';
      case 'detail':
        return 'Chi tiết';
      default:
        return null;
    }
  });

  readonly showListAsLink = computed(() => !!this.resolvedSuffix());

  ngOnInit(): void {
    this.breadcrumbService
      .ensureMenusLoaded()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshTrail());

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.refreshTrail());
  }

  onLeafClick(): void {
    if (this.showListAsLink()) {
      this.listClick.emit();
    }
  }

  private refreshTrail(): void {
    const url = this.matchUrl ?? this.router.url;
    this.trail.set(this.breadcrumbService.resolveTrail(url));
  }
}
