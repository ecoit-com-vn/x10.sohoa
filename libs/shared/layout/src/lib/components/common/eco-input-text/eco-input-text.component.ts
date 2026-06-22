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
import { getErrorMessage, removeAscent } from '@sohoa.frontend/shared/core';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { identificationType } from '@sohoa.frontend/shared/core';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';

@Component({
  selector: 'eco-input-text',
  templateUrl: './eco-input-text.component.html',
  styleUrl: './eco-input-text.component.scss',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => EcoInputTextComponent),
      multi: true,
    },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EcoInputTextComponent),
      multi: true,
    },
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    IconFieldModule,
    InputIconModule,
  ],
})
export class EcoInputTextComponent
  implements Validator, ControlValueAccessor, AfterViewInit
{
  @Input() label: string = 'EMPTY';
  @Input() placeholder: string = '';
  @Input() showLabel: boolean = true;
  @Input() showGroup: boolean = false;
  @Input() groupContent: string = '';
  @Input() groupContentPosition: 'left' | 'right' = 'right'; // "left" | "right"
  @Input() groupContentPrefix: string = '';
  @Input() groupPlaceholder: string = '';
  @Input() pattern: string = '';
  @Input() patternErrorMessage: string | null = null;
  @Input() searchIcon = false;
  @Input() needRemoveAscent = false;
  @Output() clickSearchEvent = new EventEmitter();
  @Output() onEnter = new EventEmitter();
  @Input() required?: boolean | string;
  @Input() readonly: boolean = false;
  @Input() disabled: boolean = false;
  @Input() border: boolean = true;
  @Input() type: string = 'text';
  @Input() uppercase: boolean = false;
  @Input() positionIcon: 'left' | 'right' = 'right';
  @Input() customIcon: string = 'pi pi-search';
  @Input() color: string = '';
  @Input() errorMessage: string | null = null;
  @Input() maxLength?: number;

  touched: boolean = false;

  @Input() set identificationType(value: string | null | undefined) {
    this._identificationType = value;
    if (this.onValidationChange) {
      this.onValidationChange();
    }
  }

  control = new FormControl<any>(null);
  ngControl?: NgControl;
  _identificationType: string | null | undefined = null;

  constructor(private injector: Injector) {
    // this.control.valueChanges.subscribe((value) => {
    //   if (this.onChange) {
    //     this.onChange(value);
    //   }
    // });
  }

  ngAfterViewInit() {
    const ngControl: NgControl | null = this.injector.get(NgControl, null);
    if (ngControl) {
      setTimeout(() => {
        this.ngControl = ngControl;
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

  // get errors() {
  //   const form = (this.ngControl as any)?.formDirective;
  //   return form?.submitted && this.ngControl?.errors;
  // }

  //Lấy ra message lỗi validate để hiển thị, nếu có nhiều lỗi -> hiển thị lỗi đầu tiên.
  getErrorMessage() {
    return getErrorMessage(this.ngControl?.errors, this.label);
  }

  // get showError() {
  //   return (
  //     (((this.ngControl as NgModel | FormControlName)?.formDirective as NgForm | FormGroupDirective)
  //       ?.submitted ||
  //       this.ngControl?.touched ||
  //       this.ngControl?.dirty) &&
  //     this.ngControl?.errors
  //   );
  // }

  get displayedErrorMessage() {
    if (!this.errors) return null;

    if (this.errors['required']) {
      return this.errorMessage || `${this.label} không được để trống`;
    }

    if (this.errors['maxlength']) {
      const max = this.errors['maxlength'].requiredLength;
      return `${this.label} nhập tối đa ${max} ký tự`;
    }

    if (this.errors['pattern']) {
      return (
        this.patternErrorMessage ||
        this.errorMessage ||
        `${this.label} không đúng định dạng`
      );
    }

    return getErrorMessage(this.ngControl?.errors, this.label)?.key || null;
  }

  onInput(event: any) {
    if (this.uppercase) {
      this.control.setValue(event?.target?.value.toUpperCase());
    } else {
      this.control.setValue(event?.target?.value);
    }
    this.onValidationChange();
    this.onChange(this.control.value);
  }

  trimData() {
    if (this.needRemoveAscent) {
      this.control.setValue(removeAscent(this.control.value).toUpperCase());
    }
    this.control?.setValue(this.control?.value?.trim() || null);
    if (this.searchIcon) {
      this.ngControl?.control?.markAsPristine();
      this.ngControl?.control?.markAsUntouched();
    }
    this.onChange(this.control.value);
  }

  clickSearch() {
    this.clickSearchEvent.emit();
  }

  handleEnter() {
    this.trimData();
    this.onEnter.emit();
  }

  onChange = (_value: string) => {};

  onTouched = () => {
    this.touched = true;
  };

  onValidationChange: any = () => {};

  //Dùng để check trường hiện tại có phải required hay không.
  checkRequire() {
    // return this.ngControl?.control?.hasValidator(Validators.required);
    return this.required;
  }

  writeValue(value: string): void {
    if (value !== this.control.value) {
      this.control.setValue(value);
    }
    if (this.ngControl) {
      this.onValidationChange();
      this.ngControl?.control?.markAsPristine();
    }
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  registerOnValidatorChange?(fn: () => void): void {
    this.onValidationChange = fn;
  }

  setDisabledState(isDisabled: boolean) {
    if (isDisabled) {
      this.control.disable({ emitEvent: false, onlySelf: true });
    } else {
      this.control.enable({ emitEvent: false, onlySelf: true });
    }
  }

  validate(control: AbstractControl): ValidationErrors | null {
    const value = control.value;

    if (
      this.required &&
      (value === null || value === undefined || value === '')
    ) {
      return { required: true };
    }

    if (this.pattern) {
      const regex = new RegExp(this.pattern);
      if (value && !regex.test(value)) {
        return { pattern: true };
      }
    }

    if (this.maxLength && value && value.length > this.maxLength) {
      return {
        maxlength: {
          requiredLength: this.maxLength,
          actualLength: value.length,
        },
      };
    }

    if (this._identificationType) {
      return identificationType(this._identificationType)(control);
    }

    return null;
  }
}
