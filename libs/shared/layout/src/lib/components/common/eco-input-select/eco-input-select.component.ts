import {
  AfterViewInit,
  Component,
  EventEmitter,
  forwardRef,
  Injector,
  Input,
  Output,
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
  Validators,
} from '@angular/forms';
import { NgClass, NgIf } from '@angular/common';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { getErrorMessage, isEmpty } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'eco-input-select',
  templateUrl: './eco-input-select.component.html',
  styleUrl: './eco-input-select.component.scss',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => EcoInputSelectComponent),
      multi: true,
    },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EcoInputSelectComponent),
      multi: true,
    },
  ],
  imports: [
    NgIf,
    SelectModule,
    ReactiveFormsModule,
    NgClass,
    MultiSelectModule,
  ],
})
export class EcoInputSelectComponent
  implements Validator, ControlValueAccessor, AfterViewInit {
  isArray = Array.isArray;
  @Input() label: string = '';
  @Input() showLabel: boolean = true;
  @Input() options: any;
  @Input() multiSelect: boolean = false;
  @Input() display = 'comma';
  @Input() showGroup = false;
  @Input() groupContent: string = '';
  @Input() groupContentPosition: string = 'right'; // "right" | "left"
  @Input() groupContentPrefix: string = '';
  @Input() placeholder: string = '';
  @Input() optionValue: string = 'code';
  @Input() optionLabel: string = 'name';
  @Input() dropdownIcon: string = 'pi pi-chevron-down';
  @Input() optionDisabled: string = 'disabled';
  @Input() scrollHeight: string = '200px';
  @Input() showClear: boolean = true;
  @Input() showTextValue: boolean = false;
  @Input() readonly: boolean = false;
  @Input() tooltip?: any;
  @Input() border: boolean = false;
  @Input() virtualScroll: boolean = false;
  @Input() inputLoading: boolean = false;
  @Input() required?: boolean | string;
  @Input() errorMessage: string | null = null;
  @Output() doChange = new EventEmitter<any>();
  control = new FormControl<any>(null);
  ngControl?: NgControl;
  multiselectPanelClass: any;

  constructor(private injector: Injector) {
    this.control.valueChanges.subscribe((value) => {
      if (this.onChange) {
        this.onChange(value);
      }
    });
  }

  _filter = false;

  get filter() {
    return this._filter;
  }

  getValue(control: FormControl): string {
    if (!control?.value?.length || !this.options?.length) return '';

    if (this.options.length == control.value.length) {
      return "Tất cả"
    }

    return control.value
      .map((c: any) => this.options.find((o: any) => o.id == c).name)
      .join(', ')
  }

  @Input() set filter(value: boolean) {
    this._filter = value;
    if (value) {
      this.multiselectPanelClass = 'has-filter';
    }
  }

  ngAfterViewInit() {
    const ngControl: NgControl | null = this.injector.get(NgControl, null);
    if (ngControl) {
      setTimeout(() => {
        this.ngControl = ngControl;
        this.ngControl.control?.markAsPristine();
      });
    }
  }

  get errors() {
    return (
      ((
        (this.ngControl as NgModel | FormControlName)?.formDirective as
        | NgForm
        | FormGroupDirective
      )?.submitted ||
        this.ngControl?.touched ||
        this.ngControl?.dirty) &&
      this.ngControl?.errors
    );
  }

  get displayedErrorMessage() {
    if (this.errors) {
      return this.errorMessage || this.label + ' không được để trống';
    }
    return getErrorMessage(this.ngControl?.errors, this.label)?.key || null;
  }

  getOptionsLabel(value: string | string[]) {
    if (Array.isArray(value)) {
      return value.reduce(
        (prev: string[], curr: string) => {
          const tmpItem = (this.options || []).find((e: any) => {
            return e[this.optionValue] === curr;
          });
          if (tmpItem) {
            prev.push(tmpItem[this.optionLabel]);
          }
          return prev;
        },
        []
      );
    }
    const findResult = (this.options || []).find((e: any) => e[this.optionValue] === value);
    if (findResult) {
      return findResult[this.optionLabel];
    }
    return '';
  }

  onChange = (_value: string) => { };

  onTouched = () => { };

  //Lấy ra message lỗi validate để hiển thị, nếu có nhiều lỗi -> hiển thị lỗi đầu tiên.
  getError() {
    return getErrorMessage(this.ngControl?.errors, this.label);
  }

  //Dùng để check trường hiện tại có phải required hay không.
  checkRequire() {
    // return this.ngControl?.control?.hasValidator(Validators.required);
    return !!this.required;
  }

  emitChange(event: any) {
    this.doChange.emit(event);
  }

  writeValue(value: any): void {
    value = isEmpty(value) ? null : value;
    if (value !== this.control.value) {
      this.control.setValue(value, { emitEvent: false });
    }
    if (this.ngControl) {
      this.ngControl.control!.markAsPristine();
    }
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean) {
    if (isDisabled) {
      this.control.disable({ emitEvent: false });
    } else {
      this.control.enable({ emitEvent: false });
    }
  }

  validate(control: AbstractControl): ValidationErrors | null {
    return null;
  }
}
