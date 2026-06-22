import {
  AfterViewInit,
  Component,
  forwardRef,
  Injector,
  Input,
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
import { getErrorMessage } from '@sohoa.frontend/shared/core';
import { NgClass, NgIf } from '@angular/common';
import { TextareaModule } from 'primeng/textarea';
import { InputTextModule } from 'primeng/inputtext';

@Component({
  selector: 'eco-input-textarea',
  templateUrl: './eco-input-textarea.component.html',
  styleUrl: './eco-input-textarea.component.scss',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => EcoInputTextareaComponent),
      multi: true,
    },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EcoInputTextareaComponent),
      multi: true,
    },
  ],
  standalone: true,
  imports: [
    NgIf,
    TextareaModule,
    ReactiveFormsModule,
    NgClass,
    InputTextModule,
  ],
})
export class EcoInputTextareaComponent
  implements Validator, ControlValueAccessor, AfterViewInit
{
  @Input() label: string = 'EMPTY';
  @Input() showLabel: boolean = true;
  @Input() pattern: string = '';
  @Input() autoResize: boolean = true;
  @Input() required?: boolean | string;
  @Input() readonly: boolean = false;
  @Input() disabled: boolean = false;
  @Input() rows: number = 1;
  @Input() border: boolean = true;
  @Input() errorMessage: string | null = null;
  @Input() maxLength?: number;

  ngControl?: NgControl;
  control = new FormControl<any>(null);

  constructor(private injector: Injector) {
    this.control.valueChanges.subscribe((value) => {
      if (this.onChange) {
        this.onChange(value);
      }
    });
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

  get displayedErrorMessage() {
    if (this.errors) {
      return this.errorMessage || this.label + ' không được để trống';
    }
    return getErrorMessage(this.ngControl?.errors, this.label)?.key || null;
  }

  onChange = (_value: string) => {};

  onTouched = () => {};

  //Lấy ra message lỗi validate để hiển thị, nếu có nhiều lỗi -> hiển thị lỗi đầu tiên.
  getErrorMessage() {
    return getErrorMessage(this.ngControl?.errors, this.label);
  }

  //Dùng để check trường hiện tại có phải required hay không.
  checkRequire() {
    // return this.ngControl?.control?.hasValidator(Validators.required);
    return !!this.required;
  }

  writeValue(value: string): void {
    this.control.setValue(value);
    if (this.ngControl) {
      this.ngControl?.control?.markAsPristine();
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
      this.control.enable();
    }
  }

  validate(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (this.maxLength && value && value.length > this.maxLength) {
      return {
        maxlength: {
          requiredLength: this.maxLength,
          actualLength: value.length,
        },
      };
    }
    return null;
  }
}
