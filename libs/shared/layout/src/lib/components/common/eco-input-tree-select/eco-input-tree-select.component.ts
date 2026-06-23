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
  FormControl,
  FormControlName,
  FormGroupDirective,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  NgControl,
  NgForm,
  NgModel,
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
  @Input() metaKeySelection: boolean = false;
  @Input() required?: boolean | string;
  @Input() nodes: any;
  @Input() showClear: boolean = true;
  @Input() errorMessage: string | null = null;
  @Output() doChange = new EventEmitter<any>();

  ngControl?: NgControl;
  control = new FormControl();
  isFiltering: boolean = false;
  constructor(
    private el: ElementRef<HTMLElement>,
    private injector: Injector) {
    this.control.valueChanges.subscribe((value) => {
      let emitValue = value;
      if (
        this.isStringValue &&
        Array.isArray(value) &&
        (this.selectionMode === 'checkbox' || this.selectionMode === 'multiple')
      ) {
        emitValue = value
          .map((o: any) => (o && typeof o === 'object' ? o.key : o))
          .join(';');
      }
      if (this.onChange) {
        this.onChange(emitValue);
      }
      this.emitChange(emitValue);
    });
  }

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

  get isSubmitted(): boolean {
    const form = (this.ngControl as any)?.formDirective;
    return !!form?.submitted;
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
    let currentValues = this.control.value;
    if (!Array.isArray(currentValues)) return;
    const tempValue = [...currentValues];
    this.control.setValue([]);

    setTimeout(() => {
      this.control.setValue(tempValue);
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
        //this.control = ngControl.control as FormControl;
      });
    }
  }
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['nodes'] && this.control.value) {
      this.selectedNode = this.findNodeByKey(this.nodes, this.control.value);
    }
  }

  onTreePanelShow() {
    const closeButton: any = this.el.nativeElement.querySelector(
      'button.p-treeselect-close',
    );
    if (closeButton) {
      closeButton.setAttribute('type', 'button');
    }
  }

  onChange = (_value: any) => {};

  onTouched = () => {};

  //Lấy ra message lỗi validate để hiển thị, nếu có nhiều lỗi -> hiển thị lỗi đầu tiên.
  getError() {
    return getErrorMessage(this.ngControl?.errors, this.label);
  }

  //Dùng để check trường hiện tại có phải required hay không.
  checkRequire() {
    // return this.ngControl?.control?.hasValidator(Validators.required);
    return !!this.required;
  }

  setDisabledState(isDisabled: boolean) {
    if (isDisabled) {
      this.control.disable({ emitEvent: false });
    } else {
      this.control.enable();
    }
  }

  validate(control: AbstractControl): ValidationErrors | null {
    return null;
  }

  selectedNode: any = null;

  writeValue(value: any): void {
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
        values = value.split(';').filter((v) => v);
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

  onSelect(event: any) {
    this.selectedNode = event.node;
    this.onChange?.(event.node.key); // return id
  }

  onUnselect(event: any) {
    this.selectedNode = null;
    this.onChange(null);
  }

  findNodeById(nodes: any[], id: any): any {
    for (const node of nodes) {
      if (node.key === id || node.data?.id === id) return node;
      if (node.children) {
        const found = this.findNodeById(node.children, id);
        if (found) return found;
      }
    }
    return null;
  }

  findNodeByKey(nodes: any[], key: any): any | null {
    if (!nodes) return null;
    for (const node of nodes) {
      if (node.key === key) return node;
      if (node.children) {
        const found = this.findNodeByKey(node.children, key);
        if (found) return found;
      }
    }
    return null;
  }
}
