import {
  AfterViewInit,
  Component,
  forwardRef,
  Injector,
  Input,
  Output,
  EventEmitter
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
import { InputNumberModule } from 'primeng/inputnumber';
import { RadioButtonModule } from 'primeng/radiobutton';
import { getErrorMessage } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'eco-input-number',
  templateUrl: './eco-input-number.component.html',
  styleUrl: './eco-input-number.component.scss',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => EcoInputNumberComponent),
      multi: true,
    },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EcoInputNumberComponent),
      multi: true,
    },
  ],
  imports: [
    // DecimalPipe,
    NgIf,
    InputNumberModule,
    ReactiveFormsModule,
    NgClass,
    // NgForOf,
    RadioButtonModule,
  ],
})
export class EcoInputNumberComponent
  implements Validator, ControlValueAccessor, AfterViewInit
{
  @Input() label: string = 'EMPTY';
  @Input() showLabel: boolean = true;
  @Input() pattern: string = '';
  @Input() required?: boolean | string;
  @Input() readonly: boolean = false;
  @Input() disabled: boolean = false;
  @Input() border: boolean = true;
  @Input() useGrouping: boolean = true;
  @Input() suffix: string = '';
  @Input() prefix: string = '';
  @Input() maxLength: number = 0;
  @Input() checkLength: boolean = false;
  @Input() errorMessage: string | null = null;
  @Input() placeholder: string  = "";
  @Output() onEnter = new EventEmitter<any>();
  ngControl?: NgControl;
  control = new FormControl<any>(null);

  constructor(private injector: Injector) {
    this.control.valueChanges.subscribe((value) => {
      if (this.onChange) {
        this.onChange(value);
      }
    });
  }

  _minFractionDigits: number = 0;

  get minFractionDigits() {
    return this._minFractionDigits;
  }

  @Input() set minFractionDigits(value: number) {
    this._minFractionDigits = value;
    if (this._maxFractionDigits < this._minFractionDigits) {
      this._maxFractionDigits = this._minFractionDigits;
    }
  }

  _maxFractionDigits: number = 2;

  get maxFractionDigits() {
    return this._maxFractionDigits;
  }

  @Input() set maxFractionDigits(value: number) {
    this._maxFractionDigits = value;
    if (this._maxFractionDigits < this._minFractionDigits) {
      this._maxFractionDigits = this._minFractionDigits;
    }
  }

  ngAfterViewInit() {
    const ngControl: NgControl | null = this.injector.get(NgControl, null);
    if (ngControl) {
      setTimeout(() => {
        this.ngControl = ngControl;
        // this.control = ngControl.control as FormControl;
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

  // onInput(event: any) {
  //   this.control.patchValue(event.value);
  //   this.onChange(event.value);
  // }

  handleValueChange(event: any) {
    const value = event?.value ?? null;
    this.onChange(value);
  }

  handleEnter() {
    this.onEnter.emit();
  }

  onChange = (_value: number) => {};

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

  writeValue(value: any): void {
    value = typeof value === 'number' ? value : null;
    this.control.setValue(value, { emitEvent: false });
    if (this.ngControl) {
      this.ngControl.control!.markAsPristine();
    }
  }

  registerOnChange(fn: (value: number) => void): void {
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
    let value = control.value;
    if (this.checkLength && value.toString().length > this.maxLength) {
      return {
        maxlength: {
          requiredLength: this.maxLength,
        },
      };
    }
    return null;
  }
}
