import { AfterViewInit, Component, forwardRef, Injector, Input } from '@angular/core';
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
import moment from 'moment/moment';
import { getErrorMessage } from '@sohoa.frontend/shared/core';
import { Constants } from '@sohoa.frontend/shared/core';
import { InputMaskModule } from 'primeng/inputmask';
import { NgClass, NgForOf, NgIf } from '@angular/common';
import { PopoverModule } from 'primeng/popover';
import { InputTextModule } from 'primeng/inputtext';

@Component({
  selector: 'eco-input-time',
  templateUrl: './eco-input-time.component.html',
  styleUrl: './eco-input-time.component.scss',
  standalone: true,
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => EcoInputTimeComponent),
      multi: true,
    },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EcoInputTimeComponent),
      multi: true,
    },
  ],
  imports: [
    InputMaskModule,
    ReactiveFormsModule,
    NgClass,
    PopoverModule,
    NgIf,
    NgForOf,
    InputTextModule,
  ],
})
export class EcoInputTimeComponent implements Validator, ControlValueAccessor, AfterViewInit {
  @Input() label: string = 'EMPTY';
  @Input() placeholder: string = 'EMPTY';
  @Input() showLabel: boolean = true;
  @Input() required?: boolean | string;
  @Input() readonly: boolean = false;
  @Input() minTimePresent: boolean = false;
  @Input() disabled: boolean = true;
  @Input() border: boolean = true;
  separator: string = ':';
  control = new FormControl<any>(null);
  hours = [
    '00',
    '01',
    '02',
    '03',
    '04',
    '05',
    '06',
    '07',
    '08',
    '09',
    '10',
    '11',
    '12',
    '13',
    '14',
    '15',
    '16',
    '17',
    '18',
    '19',
    '20',
    '21',
    '22',
    '23',
  ];
  minutes = [
    '00',
    '01',
    '02',
    '03',
    '04',
    '05',
    '06',
    '07',
    '08',
    '09',
    '10',
    '11',
    '12',
    '13',
    '14',
    '15',
    '16',
    '17',
    '18',
    '19',
    '20',
    '21',
    '22',
    '23',
    '24',
    '25',
    '26',
    '27',
    '28',
    '29',
    '30',
    '31',
    '32',
    '33',
    '34',
    '35',
    '36',
    '37',
    '38',
    '39',
    '40',
    '41',
    '42',
    '43',
    '44',
    '45',
    '46',
    '47',
    '48',
    '49',
    '50',
    '51',
    '52',
    '53',
    '54',
    '55',
    '56',
    '57',
    '58',
    '59',
  ];
  hour: number = 0;
  minute: number = 0;
  ngControl?: NgControl;

  constructor(private injector: Injector) {
    this.control.valueChanges.subscribe(value => {
      if (this.onChange) {
        const val = value.substring(0, 2) + this.separator + value.substring(2, 4);
        if (val === this.separator) {
          this.onChange('');
        } else {
          this.onChange(val);
        }
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
      (((this.ngControl as NgModel | FormControlName)?.formDirective as NgForm | FormGroupDirective)
        ?.submitted ||
        this.ngControl?.touched ||
        this.ngControl?.dirty) &&
      this.ngControl?.errors
    );
  }

  setHour(hour: number | string) {
    this.hour = +hour;
    this.updateHour();
  }

  setMinute(minute: number | string) {
    this.minute = +minute;
    this.updateMinute();
  }

  updateHour() {
    let result = this.hour.toString().padStart(2, '0') + this.control.value.substring(2, 4);
    this.onChange(result);
    this.control.setValue(result);
  }

  updateMinute() {
    if (!this.control.value) {
      this.setHour(moment().hour());
    }
    let result = this.control.value.substring(0, 2) + this.minute.toString().padStart(2, '0');
    this.control.setValue(result);
  }

  onBlur() {
    const tmpHour = this.control.value.substring(0, 2);
    const tmpMinute = this.control.value.substring(2, 4);
    this.setHour(parseInt(tmpHour));

    this.setMinute(parseInt(tmpMinute));
  }

  onKeyDown(event: any) {
    if ([37, 38, 39, 40].includes(event.keyCode)) {
      event.preventDefault();
    }
    let selectionEnd = event.target.selectionEnd;
    if (event.keyCode === 38) {
      if (selectionEnd < 3) {
        if (this.hour < 23) {
          this.hour++;
          this.updateHour();
          event.target.setSelectionRange(0, 2);
        } else {
          this.hour = 0;
          this.updateHour();
          event.target.setSelectionRange(0, 2);
        }
      } else {
        if (this.minute < 59) {
          this.minute++;
          this.updateMinute();
          event.target.setSelectionRange(3, 5);
        } else {
          this.minute = 0;
          this.updateMinute();
          event.target.setSelectionRange(3, 5);
        }
      }
    }

    if (event.keyCode === 40) {
      if (selectionEnd < 3) {
        if (this.hour > 0) {
          this.hour--;
          this.updateHour();
          event.target.setSelectionRange(0, 2);
        } else {
          this.hour = 23;
          this.updateHour();
          event.target.setSelectionRange(0, 2);
        }
      } else {
        if (this.minute > 0) {
          this.minute--;
          this.updateMinute();
          event.target.setSelectionRange(3, 5);
        } else {
          this.minute = 59;
          this.updateMinute();
          event.target.setSelectionRange(3, 5);
        }
      }
    }
  }

  onKeyUp(event: any) {
    if (event.keyCode === 37) {
      event.target.setSelectionRange(0, 2);
    }

    if (event.keyCode === 39) {
      event.target.setSelectionRange(3, 5);
    }
  }

  onInput(event: any) {
    const currentValue = event.target.value;
    const tmpHour = currentValue.split(this.separator)[0];
    const tmpMinute = currentValue.split(this.separator)[1];
    if (parseInt(tmpHour) > 23 && event.target.value.length === 5) {
      this.setHour(23);
      event.target.setSelectionRange(0, 2);
    }
    if (parseInt(tmpMinute) > 59 && event.target.value.length === 5) {
      this.setMinute(59);
      event.target.setSelectionRange(3, 5);
    }
  }

  onClick(event: any) {
    let selectionEnd = event.target.selectionEnd;
    if (selectionEnd < 3) {
      event.target.setSelectionRange(0, 2);
    } else {
      event.target.setSelectionRange(3, 5);
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

  writeValue(value: string): void {
    if (!value) {
      this.control.setValue(moment().format('HH:mm'));
      this.setHour(moment().hour());
      this.setMinute(moment().minute());
    } else {
      this.control.setValue(value, { emitEvent: false });
      if (Constants.Regex_TimeFormat.test(value)) {
        this.setHour(value.split(':')[0]);
        this.setMinute(value.split(':')[1]);
      }
    }
    if (this.ngControl) {
      this.ngControl.control!.markAsPristine();
    }
  }

  onPickerShow() {
    if (!this.control.value) {
      this.control.setValue(moment().format('HH:mm'));
      this.setHour(moment().hour());
      this.setMinute(moment().minute());
    }
    const currentHourItem = document.getElementById(
      `hour-${this.hour.toString().padStart(2, '0')}`,
    );
    const currentMinuteItem = document.getElementById(
      `minute-${this.minute.toString().padStart(2, '0')}`,
    );
    currentHourItem?.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
    currentMinuteItem?.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
  }

  registerOnChange(fn: (value: Date) => void): void {
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
    if (!Constants.Regex_TimeFormat.test(control.value)) {
      return { pattern: {} };
    }

    if (this.minTimePresent) {
      if (moment(control.value, 'HH:mm').isSameOrBefore(moment())) {
        return { minTimePresent: {} };
      }
    }

    return null;
  }

  timeToString(time: string) {
    return `${time[0]}${time[1]}:${time[2]}${time[3]}`;
  }
}
