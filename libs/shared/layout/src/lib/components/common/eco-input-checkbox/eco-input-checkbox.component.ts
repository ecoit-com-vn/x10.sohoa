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
  FormControlName,
  FormGroupDirective,
  FormsModule,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  NgControl,
  NgForm,
  NgModel,
  ValidationErrors,
  Validator,
  Validators,
} from '@angular/forms';
import { CheckboxModule } from 'primeng/checkbox';
import { NgClass, NgIf } from '@angular/common';
import { getErrorMessage } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'eco-input-checkbox',
  templateUrl: './eco-input-checkbox.component.html',
  styleUrl: './eco-input-checkbox.component.scss',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => EcoInputCheckboxComponent),
      multi: true,
    },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EcoInputCheckboxComponent),
      multi: true,
    },
  ],
  imports: [CheckboxModule, NgClass, NgIf, FormsModule]
})
export class EcoInputCheckboxComponent implements Validator, ControlValueAccessor, AfterViewInit {
  @Input() label: string = 'EMPTY';
  @Input() showLabel: boolean = true;
  @Input() readonly: boolean = false;
  @Input() disabled: boolean = false;
  @Input() border: boolean = false;
  @Output() dataChange = new EventEmitter<boolean>();

  ngControl?: NgControl;
  value: boolean = false; // Giá trị thực tế bind với checkbox

  constructor(private injector: Injector) { }

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
      (((this.ngControl as NgModel | FormControlName)?.formDirective as NgForm | FormGroupDirective)
        ?.submitted ||
        this.ngControl?.touched ||
        this.ngControl?.dirty) &&
      this.ngControl?.errors
    );
  }

  onCheckboxChange(event: any) {
    const newValue = event.checked ?? false;
    this.value = newValue;

    // Notify Angular Forms
    this.onChange(newValue);
    this.onTouched();

    // Emit event
    this.dataChange.emit(newValue);
  }

  onChange = (_value: boolean) => { };
  onTouched = () => { };

  getError() {
    return getErrorMessage(this.ngControl?.errors, this.label);
  }

  checkRequire() {
    return this.ngControl?.control?.hasValidator(Validators.required);
  }

  writeValue(value: boolean): void {
    this.value = value ?? false;
  }

  registerOnChange(fn: (value: boolean) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean) {
    this.disabled = isDisabled;
  }

  validate(control: AbstractControl): ValidationErrors | null {
    return null;
  }
}