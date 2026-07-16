import {
  Component,
  DestroyRef,
  EventEmitter,
  Input,
  OnInit,
  OnChanges,
  SimpleChanges,
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
export class WfBreadcrumbComponent implements OnInit, OnChanges {
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

  private suffixSignal = signal<string | null>(null);
  private viewModeSignal = signal<BreadcrumbViewMode | null>(null);
  private leafLabelSignal = signal<string | null>(null);
  private customItemsSignal = signal<BreadcrumbTrailItem[] | null>(null);
  private currentUrlSignal = signal<string>('');

  private trail = computed(() => {
    const url = this.currentUrlSignal();
    return this.breadcrumbService.resolveTrail(url);
  });

  readonly items = computed(() => {
    const customItems = this.customItemsSignal();
    const trail = customItems?.length ? [...customItems] : [...this.trail()];
    if (!trail.length) {
      return trail;
    }

    const leafLabel = this.leafLabelSignal();
    if (leafLabel?.trim()) {
      trail[trail.length - 1] = {
        ...trail[trail.length - 1],
        label: leafLabel.trim(),
      };
    }

    return trail;
  });

  readonly resolvedSuffix = computed(() => {
    const suffix = this.suffixSignal();
    const viewMode = this.viewModeSignal();

    if (suffix?.trim()) {
      return suffix.trim();
    }

    switch (viewMode) {
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
    this.currentUrlSignal.set(this.matchUrl ?? this.router.url);
    this.suffixSignal.set(this.suffix);
    this.viewModeSignal.set(this.viewMode);
    this.leafLabelSignal.set(this.leafLabel);
    this.customItemsSignal.set(this.customItems);

    this.breadcrumbService
      .ensureMenusLoaded()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((event) => {
        this.currentUrlSignal.set(this.matchUrl ?? (event.urlAfterRedirects || event.url));
      });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['suffix']) {
      this.suffixSignal.set(this.suffix);
    }
    if (changes['viewMode']) {
      this.viewModeSignal.set(this.viewMode);
    }
    if (changes['leafLabel']) {
      this.leafLabelSignal.set(this.leafLabel);
    }
    if (changes['matchUrl']) {
      this.currentUrlSignal.set(this.matchUrl ?? this.router.url);
    }
    if (changes['customItems']) {
      this.customItemsSignal.set(this.customItems);
    }
  }

  onLeafClick(): void {
    if (this.showListAsLink()) {
      this.listClick.emit();
    }
  }
}
