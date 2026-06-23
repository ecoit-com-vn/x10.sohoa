import { AfterViewInit, Component, forwardRef, Injector, Input, ViewChild } from '@angular/core';
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
import { InputMask, InputMaskModule } from 'primeng/inputmask';
import { Constants } from '@sohoa.frontend/shared/core';
import {getErrorMessage, isNotEmpty} from '@sohoa.frontend/shared/core';

@Component({
    selector: 'eco-input-phone',
    templateUrl: './eco-input-phone.component.html',
    styleUrl: './eco-input-phone.component.scss',
    providers: [
        {
            provide: NG_VALIDATORS,
            useExisting: forwardRef(() => EcoInputPhoneComponent),
            multi: true,
        },
        {
            provide: NG_VALUE_ACCESSOR,
            useExisting: forwardRef(() => EcoInputPhoneComponent),
            multi: true,
        },
    ],
    imports: [NgIf, InputMaskModule, ReactiveFormsModule, NgClass]
})
export class EcoInputPhoneComponent implements Validator, ControlValueAccessor, AfterViewInit {
  @Input() label: string = 'EMPTY';
  @Input() placeholder: string = '';
  @Input() showLabel: boolean = true;
  @Input() patternPhone: string = Constants.Regex_Phone.source;
  @Input() required?: boolean | string;
  @Input() readonly: boolean = false;
  @Input() disabled: boolean = false;
  @Input() border: boolean = true;
  @Input() useGrouping: boolean = true;
  @Input() suffix: string = '';
  @Input() prefix: string = '';
  @Input() errorMessage: string | null = null;
  control = new FormControl<any>(null);
  ngControl?: NgControl;
  @ViewChild('inputMask', { static: true }) inputMask!: InputMask;

  constructor(private injector: Injector) {}

  ngAfterViewInit() {
    const ngControl: NgControl | null = this.injector.get(NgControl, null);
    if (ngControl) {
      setTimeout(() => {
        this.ngControl = ngControl;
        // this.control = (ngControl.control ?? ngControl) as FormControl;
      });
    }
  }

  get errors() {
    return (
      (((this.ngControl as NgModel | FormControlName)?.formDirective as NgForm | FormGroupDirective)
        ?.submitted ||
        this.ngControl?.touched ||
        this.ngControl?.dirty) &&
      this.ngControl?.errors
    );
  }

  get displayedErrorMessage() {
    if (this.errors) {
      console.log(this.errors);
      
      return this.errorMessage || this.label + ' không được để trống';
    }
    return getErrorMessage(this.ngControl?.errors, this.label)?.key || null;
  }

  onChange = (_value: string) => {};

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

  writeValue(value: number): void {
    if (value !== this.control.value) {
      this.control.setValue(value);
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
      this.control.enable();
    }
  }

  validate(control: AbstractControl): ValidationErrors | null {
    return isNotEmpty(this.inputMask.inputViewChild?.nativeElement.value) &&
      !this.inputMask.inputViewChild?.nativeElement.value.match(this.patternPhone)
      ? { phonePattern: { actualValue: control.value } }
      : null;
  }

  onInput(_event: any) {
    this.onChange(this.inputMask?.inputViewChild?.nativeElement.value);
  }
}
