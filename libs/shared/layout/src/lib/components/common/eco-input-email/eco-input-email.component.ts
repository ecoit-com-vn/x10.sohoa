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
// import { ChipViewDirective } from '../../directives';
import { InputTextModule } from 'primeng/inputtext';
import { Constants } from '@sohoa.frontend/shared/core';
import {getErrorMessage, isNotEmpty} from '@sohoa.frontend/shared/core';

@Component({
  selector: 'eco-input-email',
  templateUrl: './eco-input-email.component.html',
  styleUrl: './eco-input-email.component.scss',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => EcoInputEmailComponent),
      multi: true,
    },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EcoInputEmailComponent),
      multi: true,
    },
  ],
  standalone: true,
  imports: [
    NgIf,
    // ChipViewDirective,
    NgClass,
    ReactiveFormsModule,
    InputTextModule,
  ],
})
export class EcoInputEmailComponent implements Validator, ControlValueAccessor, AfterViewInit {
  @Input() label: string = 'EMPTY';
  @Input() placeholder: string = 'EMPTY';
  @Input() patternEmail: string = Constants.Regex_Email.source;
  @Input() showLabel: boolean = true;
  @Input() showGroup: boolean = false;
  @Input() groupContent: string = '';
  @Input() groupContentPosition: 'left' | 'right' = 'right'; // "left" | "right"
  @Input() groupContentPrefix: string = '';
  @Input() groupPlaceholder: string = '';
  @Input() searchIcon = false;
  @Input() needRemoveAscent = false;
  @Output() clickSearchEvent = new EventEmitter();
  @Input() required?: boolean | string;
  @Input() readonly: boolean = false;
  @Input() disabled: boolean = false;
  @Input() border: boolean = true;
  @Input() type: string = 'text';
  @Input() errorMessage: string | null = null;
  control = new FormControl<any>(null);

  ngControl?: NgControl;

  constructor(private injector: Injector) {}

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
      (((this.ngControl as NgModel | FormControlName)?.formDirective as NgForm | FormGroupDirective)
        ?.submitted ||
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

  onInput(_event: any) {
    this.onChange(this.control.value);
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

  writeValue(value: string): void {
    if (value !== this.control.value) {
      this.control.setValue(value);
    }
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
      this.control.enable({ emitEvent: false });
    }
  }

  validate(control: AbstractControl): ValidationErrors | null {
    return isNotEmpty(this.control.value) && !this.control.value.match(this.patternEmail)
      ? { emailPattern: { actualValue: control.value } }
      : null;
  }
}
