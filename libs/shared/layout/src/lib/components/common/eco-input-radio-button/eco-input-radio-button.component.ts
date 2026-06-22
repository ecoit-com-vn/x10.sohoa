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
import { NgForOf, NgIf } from '@angular/common';
import { RadioButtonModule } from 'primeng/radiobutton';
import { InputTextModule } from 'primeng/inputtext';
import {getErrorMessage} from '@sohoa.frontend/shared/core';

@Component({
    selector: 'eco-input-radio-button',
    templateUrl: './eco-input-radio-button.component.html',
    styleUrl: './eco-input-radio-button.component.scss',
    providers: [
        {
            provide: NG_VALIDATORS,
            useExisting: forwardRef(() => EcoInputRadioButtonComponent),
            multi: true,
        },
        {
            provide: NG_VALUE_ACCESSOR,
            useExisting: forwardRef(() => EcoInputRadioButtonComponent),
            multi: true,
        },
    ],
    imports: [
        // NgClass,
        NgIf,
        RadioButtonModule,
        ReactiveFormsModule,
        NgForOf,
        InputTextModule,
        // NgStyle,
    ]
})
export class EcoInputRadioButtonComponent implements Validator, ControlValueAccessor, AfterViewInit {
  @Input() label: string = 'EMPTY';
  @Input() placeholder: string = 'EMPTY';
  @Input() showLabel: boolean = true;
  @Input() options: any[] = [];
  @Input() optionValue: string = 'code';
  @Input() optionLabel: string = 'name';
  @Input() readonly: boolean = false;
  @Input() styleOption: 'horizontal' | 'vertical' = 'horizontal';
  @Output() doChange = new EventEmitter<any>();

  control = new FormControl();
  ngControl?: NgControl;

  constructor(private injector: Injector) {
    this.control.valueChanges.subscribe(value => {
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

  onChange = (_value: string) => {};

  onTouched = () => {};

  //Lấy ra message lỗi validate để hiển thị, nếu có nhiều lỗi -> hiển thị lỗi đầu tiên.
  getError() {
    return getErrorMessage(this.ngControl?.errors, this.label);
  }

  //Dùng để check trường hiện tại có phải required hay không.
  checkRequire() {
    return this.ngControl?.control?.hasValidator(Validators.required);
  }

  writeValue(value: string): void {
    this.control.setValue(value, { emitEvent: false });
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
    return null;
  }

  emitChange(event: any) {
    this.doChange.emit(event);
  }
}
