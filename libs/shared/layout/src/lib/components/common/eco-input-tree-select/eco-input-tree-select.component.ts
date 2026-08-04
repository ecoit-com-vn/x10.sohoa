import {
  AfterViewInit,
  Component,
  ElementRef,
  forwardRef,
  Injector,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
} from '@angular/core';
import {
  AbstractControl,
  ControlValueAccessor,
  FormControlName,
  FormGroupDirective,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  NgControl,
  NgForm,
  NgModel,
  FormsModule,
  ReactiveFormsModule,
  ValidationErrors,
  Validator,
} from '@angular/forms';
import { getErrorMessage } from '@sohoa.frontend/shared/core';
import { NgClass, NgIf } from '@angular/common';
import { TreeSelectModule } from 'primeng/treeselect';

@Component({
  selector: 'eco-input-tree-select',
  templateUrl: './eco-input-tree-select.component.html',
  styleUrl: './eco-input-tree-select.component.scss',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => EcoInputTreeSelectComponent),
      multi: true,
    },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EcoInputTreeSelectComponent),
      multi: true,
    },
  ],
  imports: [
    NgClass,
    NgIf,
    TreeSelectModule,
    FormsModule,
    ReactiveFormsModule,
  ],
})
export class EcoInputTreeSelectComponent
  implements Validator, ControlValueAccessor, AfterViewInit, OnChanges
{
  @Input() label: string = '';
  @Input() placeholder: string = '';
  @Input() showLabel: boolean = true;
  @Input() enableSearch: boolean = true;
  @Input() isStringValue: boolean = false;
  @Input() scrollHeight: string = '200px';
  @Input() emptyMessage: string = '';
  @Input() readonly: boolean = false;
  @Input() border: boolean = false;
  @Input() selectionMode: 'single' | 'multiple' | 'checkbox' = 'single';
  @Input() display: 'comma' | 'chip' = 'comma';
  @Input() metaKeySelection: boolean = false;
  @Input() propagateSelectionUp: boolean = true;
  @Input() propagateSelectionDown: boolean = true;
  @Input() required?: boolean | string;
  @Input() nodes: any;
  @Input() showClear: boolean = true;
  @Input() errorMessage: string | null = null;
  @Output() doChange = new EventEmitter<any>();

  ngControl?: NgControl;
  isFiltering: boolean = false;
  selectedNode: any = null;
  private pendingValue: any = null;

  constructor(
    private el: ElementRef<HTMLElement>,
    private injector: Injector,
  ) {}

  get errors() {
    const form = (this.ngControl as any)?.formDirective;
    return form?.submitted && this.ngControl?.errors;
  }

  get displayedErrorMessage() {
    if (this.errors) {
      return this.errorMessage || this.label + ' không được để trống';
    }
    return getErrorMessage(this.ngControl?.errors, this.label)?.key || null;
  }

  onFilter(event: any) {
    const previousFiltering = this.isFiltering;
    this.isFiltering = !!event.filter;
    if (previousFiltering && !this.isFiltering) {
      this.refreshTreeSelection();
    }
  }

  refreshTreeSelection() {
    if (this.selectionMode !== 'checkbox') return;
    const currentValues = this.selectedNode;
    if (!Array.isArray(currentValues)) return;
    const tempValue = [...currentValues];
    this.selectedNode = [];
    setTimeout(() => {
      this.selectedNode = tempValue;
    }, 10);
  }

  emitChange(event: any) {
    this.doChange.emit(event);
  }

  ngAfterViewInit() {
    const ngControl: NgControl | null = this.injector.get(NgControl, null);
    if (ngControl) {
      setTimeout(() => {
        this.ngControl = ngControl;
      });
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['nodes'] && this.nodes) {
      const value = this.pendingValue ?? this.extractKeysFromSelection(this.selectedNode);
      if (value !== null && value !== undefined && value !== '') {
        this.writeValue(value);
      }
    }
  }

  onTreePanelShow() {
    setTimeout(() => {
      const panelButtons = document.querySelectorAll(
        '.p-treeselect-overlay button, .p-treeselect-panel button',
      );
      panelButtons.forEach((btn) => btn.setAttribute('type', 'button'));
    });
  }

  onChange = (_value: any) => {};
  onTouched = () => {};

  getError() {
    return getErrorMessage(this.ngControl?.errors, this.label);
  }

  checkRequire() {
    return !!this.required;
  }

  setDisabledState(isDisabled: boolean) {
    this.readonly = isDisabled;
  }

  validate(_control: AbstractControl): ValidationErrors | null {
    return null;
  }

  onInternalModelChange(value: any) {
    this.selectedNode = value;
    const emitValue = this.normalizeEmitValue(value);
    this.pendingValue = emitValue;
    this.onChange?.(emitValue);
    this.emitChange(emitValue);
    this.onTouched?.();
  }

  writeValue(value: any): void {
    this.pendingValue = value;
    if (
      this.selectionMode === 'checkbox' ||
      this.selectionMode === 'multiple'
    ) {
      if (!value) {
        this.selectedNode = [];
        return;
      }
      let values = value;
      if (this.isStringValue && typeof value === 'string') {
        values = value.split(';').filter((v: string) => v);
      }
      values = Array.isArray(values) ? values : [values];
      this.selectedNode = values
        .map((v: any) => this.findNodeByKey(this.nodes, v))
        .filter(Boolean);
      return;
    }
    if (!value) {
      this.selectedNode = null;
      return;
    }
    if (typeof value === 'number' || typeof value === 'string') {
      this.selectedNode = this.findNodeByKey(this.nodes, value);
    } else {
      this.selectedNode = value;
    }
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  private normalizeEmitValue(value: any): any {
    if (
      this.selectionMode === 'checkbox' ||
      this.selectionMode === 'multiple'
    ) {
      if (!value) return this.isStringValue ? '' : [];
      const arr = Array.isArray(value) ? value : [value];
      const keys = arr
        .filter((o: any) => !o?.partialSelected)
        .map((o: any) => (o && typeof o === 'object' ? o.key : o))
        .filter((k: any) => k !== null && k !== undefined && k !== '');
      if (this.isStringValue) return keys.join(';');
      return keys.map((k: any) => {
        const n = Number(k);
        return !isNaN(n) && String(n) === String(k) ? n : k;
      });
    }
    if (value && typeof value === 'object' && value.key !== undefined) {
      const n = Number(value.key);
      return !isNaN(n) && String(n) === String(value.key) ? n : value.key;
    }
    return value ?? null;
  }

  private extractKeysFromSelection(selection: any): any {
    return this.normalizeEmitValue(selection);
  }

  findNodeByKey(nodes: any[], key: any): any | null {
    if (!nodes) return null;
    const keyStr = key != null ? String(key) : '';
    for (const node of nodes) {
      if (node.key === key || String(node.key) === keyStr) return node;
      if (node.children) {
        const found = this.findNodeByKey(node.children, key);
        if (found) return found;
      }
    }
    return null;
  }
}
