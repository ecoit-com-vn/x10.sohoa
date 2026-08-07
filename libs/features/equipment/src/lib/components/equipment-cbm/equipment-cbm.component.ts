import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { Subject, finalize, takeUntil } from 'rxjs';
import { EquipmentService } from '../../data-access/equipment.service';
import { EquipmentDocumentsComponent } from '../equipment-documents/equipment-documents.component';

@Component({
  selector: 'app-equipment-cbm',
  standalone: true,
  imports: [CommonModule, FormsModule, EquipmentDocumentsComponent],
  templateUrl: './equipment-cbm.component.html',
  styleUrl: './equipment-cbm.component.scss',
})
export class EquipmentCbmComponent implements OnInit, OnDestroy {
  private readonly equipmentService = inject(EquipmentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroy$ = new Subject<void>();

  equipmentOptions = signal<any[]>([]);
  selectedEquipment = signal<any | null>(null);
  selectedEquipmentId = signal('');
  activeTab = signal<'info' | 'documents'>('info');
  technicalFields = signal<any[]>([]);
  technicalValues = signal<Record<string, any>>({});
  loadingEquipment = signal(false);
  loadingOptions = signal(false);
  private readonly guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

  ngOnInit(): void {
    this.route.queryParamMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const equipmentId = this.getEquipmentId(params);
      this.selectedEquipmentId.set(equipmentId);
      this.loadEquipmentOptions(equipmentId);

      if (equipmentId) {
        this.loadEquipment(equipmentId);
      } else {
        this.selectedEquipment.set(null);
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onEquipmentChange(equipmentId: string): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { equipmentId: equipmentId || null },
    });
  }

  isActive(item: any): boolean {
    return item?.isActive === true || item?.isActive === 1;
  }

  creatorLabel(item: any): string {
    const creator = item?.creator;
    if (creator?.name) {
      return creator.username ? `${creator.name} (${creator.username})` : creator.name;
    }
    return item?.createdByName || item?.createdBy || '---';
  }

  private loadEquipmentOptions(selectedId: string): void {
    this.loadingOptions.set(true);
    this.equipmentService.getExternalFactoryAcceptanceEquipments(1, 1000).pipe(
      finalize(() => this.loadingOptions.set(false)),
      takeUntil(this.destroy$)
    ).subscribe({
      next: response => {
        const items = response?.items || [];
        this.equipmentOptions.set(items);

        const selected = this.selectedEquipment();
        if (selectedId && selected && !items.some((item: any) => item.id === selectedId)) {
          this.equipmentOptions.update(options => [...options, selected]);
        }
      },
      error: () => this.equipmentOptions.set([]),
    });
  }

  private getEquipmentId(params: ParamMap): string {
    const namedEquipmentId = params.get('equipmentId');
    if (namedEquipmentId && this.guidPattern.test(namedEquipmentId)) {
      return namedEquipmentId;
    }

    const idFromQueryKey = params.keys.find(key => this.guidPattern.test(key));
    if (idFromQueryKey) {
      return idFromQueryKey;
    }

    // Support legacy links where the equipment id was appended without a query key.
    const idFromUrl = typeof window === 'undefined'
      ? null
      : window.location.search.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i)?.[0];

    return idFromUrl ?? '';
  }

  private loadEquipment(id: string): void {
    this.loadingEquipment.set(true);
    this.equipmentService.getCbmDocumentsEquipmentDetail(id).pipe(
      finalize(() => this.loadingEquipment.set(false)),
      takeUntil(this.destroy$)
    ).subscribe({
      next: response => {
        const equipment = response?.equipment || null;
        this.selectedEquipment.set(equipment);
        this.loadTechnicalParameters(equipment);
      },
      error: () => {
        this.selectedEquipment.set(null);
        this.technicalFields.set([]);
        this.technicalValues.set({});
      },
    });
  }

  getTechnicalFieldKey(field: any): string {
    return String(field?.name || field?.id || '').trim();
  }

  hasTechnicalValue(field: any): boolean {
    const value = this.technicalValues()[this.getTechnicalFieldKey(field)];
    return value !== null && value !== undefined && value !== '';
  }

  hasAnyTechnicalValue(): boolean {
    return this.technicalFields().some(field => this.hasTechnicalValue(field));
  }

  formatTechnicalValue(field: any): string {
    const value = this.technicalValues()[this.getTechnicalFieldKey(field)];
    if (value === null || value === undefined || value === '') {
      return '---';
    }
    if (field?.type === 'checkbox') {
      return value === true || value === 'true' || value === 1 ? 'Có' : 'Không';
    }
    if (field?.type === 'date') {
      const date = new Date(value);
      return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleDateString('vi-VN');
    }
    return String(value);
  }

  private loadTechnicalParameters(equipment: any): void {
    let fields: any[] = [];
    let values: Record<string, any> = {};

    try {
      const schema = equipment?.formSchema ? JSON.parse(equipment.formSchema) : [];
      fields = Array.isArray(schema) ? schema : schema?.fields || [];
    } catch {
      fields = [];
    }

    try {
      values = equipment?.formValues ? JSON.parse(equipment.formValues) : {};
    } catch {
      values = {};
    }

    this.technicalFields.set(fields);
    this.technicalValues.set(values);
  }
}
