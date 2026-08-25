import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  forwardRef,
  Injector,
  Input,
  ViewChild,
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
import moment from 'moment';
import {
  maxDate,
  maxDateFromNow,
  maxDateToday,
  minDate,
  minDateToday,
  getErrorMessage,
  isEmpty,
  Constants
} from '@sohoa.frontend/shared/core';
import { DatePicker as Calendar, DatePickerModule as CalendarModule } from 'primeng/datepicker';
type CalendarTypeView = 'date' | 'month' | 'year';
import { DatePipe, NgClass, NgIf } from '@angular/common';
// import { CalendarWithMaskDirective } from '../../directives';
import { InputMaskModule } from 'primeng/inputmask';

@Component({
  selector: 'eco-input-date',
  templateUrl: './eco-input-date.component.html',
  styleUrl: './eco-input-date.component.scss',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => EcoInputDateComponent),
      multi: true,
    },
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EcoInputDateComponent),
      multi: true,
    },
    DatePipe,
  ],
  imports: [
    CalendarModule,
    ReactiveFormsModule,
    NgClass,
    // DatePipe,
    // CalendarWithMaskDirective,
    NgIf,
    InputMaskModule,
  ],
})
export class EcoInputDateComponent
  implements Validator, ControlValueAccessor, AfterViewInit
{
  @Input() label: string = '';
  @Input() placeholder: string = '';
  @Input() showLabel: boolean = true;
  @Input() selectionMode: 'single' | 'multiple' | 'range' = 'single'; // single, multiple, range
  @Input() required?: boolean | string;
  @Input() readonly: boolean = false;
  @Input() maxDateLabel: string = '';
  @Input() maxDateFromNowType: 'days' | 'weeks' | 'months' | 'years' = 'days';
  @Input() minDateLabel: string = '';
  @Input() dateFormat: 'dd/MM/yyyy' | 'MM/yyyy' | 'dd/mm/yy' | 'mm/yy' = 'dd/MM/yyyy';
  @Input() disabled: boolean = true;
  @Input() border: boolean = true;
  @Input() showTime: boolean = false;
  @Input() showClear: boolean = false;
  @Input() view: CalendarTypeView = 'date'; //'date' | 'month' | 'year'
  @Input() errorMessage: string | null = null;
  @ViewChild('calendar') calendarInput?: Calendar;
  control = new FormControl<any>(null);
  ngControl?: NgControl;

  get primeNgDateFormat(): 'dd/mm/yy' | 'mm/yy' {
    return this.dateFormat === 'MM/yyyy' || this.dateFormat === 'mm/yy'
      ? 'mm/yy'
      : 'dd/mm/yy';
  }
  viLocale = {
    firstDayOfWeek: 1,
    dayNames: [
      'Chủ nhật',
      'Thứ hai',
      'Thứ ba',
      'Thứ tư',
      'Thứ năm',
      'Thứ sáu',
      'Thứ bảy',
    ],
    dayNamesShort: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
    dayNamesMin: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
    monthNames: [
      'Tháng 1',
      'Tháng 2',
      'Tháng 3',
      'Tháng 4',
      'Tháng 5',
      'Tháng 6',
      'Tháng 7',
      'Tháng 8',
      'Tháng 9',
      'Tháng 10',
      'Tháng 11',
      'Tháng 12',
    ],
    monthNamesShort: [
      'Th1',
      'Th2',
      'Th3',
      'Th4',
      'Th5',
      'Th6',
      'Th7',
      'Th8',
      'Th9',
      'Th10',
      'Th11',
      'Th12',
    ],
    today: 'Hôm nay',
    clear: 'Xóa',
  };
  constructor(
    private injector: Injector,
    private datePipe: DatePipe,
    private cdr: ChangeDetectorRef
  ) {
    this.control.valueChanges.subscribe((value) => {
      if (this.onChange) {
        if (this.showTime) {
          value = !!value
            ? moment(this.control.value).startOf('minutes').toDate()
            : null;
        }
        this.onChange(value);
      }
    });
  }

  ngAfterViewInit() {
    const ngControl: NgControl | null = this.injector.get(NgControl, null);
    if (ngControl) {
      setTimeout(() => {
        this.ngControl = ngControl;
        // Ensure component starts untouched and pristine so required error is not shown on load
        if (this.ngControl && this.ngControl.control) {
          this.ngControl.control.markAsUntouched();
          this.ngControl.control.markAsPristine();
        }
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
    if (this.errors && this.required) {
      return this.errorMessage || this.label + ' không được để trống';
    }
    return getErrorMessage(this.ngControl?.errors, this.label)?.key || null;
  }

  _maxDateToday = false;

  get maxDateToday() {
    return this._maxDateToday;
  }

  @Input() set maxDateToday(value: boolean) {
    this._maxDateToday = value;
    if (this._maxDateToday) {
      if (
        moment(this.maxDate).isValid() &&
        moment(new Date()).isAfter(moment(this.maxDate), 'day')
      ) {
        return;
      } else {
        this.maxDate = moment().endOf('days').toDate();
      }
    }
  }

  _maxDateFromNow = 0;

  get maxDateFromNow(): number {
    return this._maxDateFromNow;
  }

  @Input() set maxDateFromNow(value: number) {
    this._maxDateFromNow = value;
    this.maxDate = moment()
      .add(this._maxDateFromNow, this.maxDateFromNowType)
      .toDate();
  }

  _minDateToday = false;

  get minDateToday() {
    return this._minDateToday;
  }

  @Input() set minDateToday(value: boolean) {
    this._minDateToday = value;
    if (this._minDateToday) {
      if (
        moment(this.minDate).isValid() &&
        moment(new Date()).isBefore(moment(this.minDate), 'day')
      ) {
        return;
      } else {
        this._minDate = moment().startOf('days').toDate();
      }
    }
  }

  _maxDate: any;

  get maxDate(): Date {
    return this._maxDate;
  }

  @Input() set maxDate(value: any) {
    if (typeof value === 'string') {
      const date: any = moment(value);
      this._maxDate = new Date(date);
    } else if (value instanceof Date) {
      this._maxDate = value;
    } else {
      this._maxDate = undefined;
    }
    if (this.maxDateToday) {
      if (
        moment(this.maxDate).isValid() &&
        moment(this.maxDate).isBefore(moment(new Date()))
      ) {
        return;
      }
      this._maxDate = moment().endOf('days').toDate();
    }
  }

  _minDate: any;

  get minDate(): Date {
    return this._minDate;
  }

  @Input() set minDate(value: any) {
    if (typeof value === 'string') {
      const date: any = moment(value);
      this._minDate = new Date(date);
    } else if (value instanceof Date) {
      this._minDate = value;
    } else {
      this._minDate = undefined;
    }

    if (this.minDateToday) {
      if (moment(this.minDate).isValid()) {
        if (moment(this.minDate).isAfter(moment(new Date()))) {
          return;
        }
      }
      this._minDate = moment().startOf('days').toDate();
    }
  }

  onBlur(_event: any) {
    if (!this.control.value) {
      return;
    }
  }

  onInput(_event: any) {}

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
    if (
      value !== this.control.value &&
      moment(value, moment.HTML5_FMT.DATETIME_LOCAL_MS).isValid()
    ) {
      let valueInput: any = new Date(value);
      if (this.showTime) {
        valueInput = moment(value).startOf('minutes').toDate();
      }
      this.control.setValue(valueInput);
    } else {
      this.control.setValue(value);
    }
    // Ensure the field starts untouched/pristine so required error is not shown on load
    if (this.ngControl && this.ngControl.control) {
      this.ngControl.control.markAsPristine();
      this.ngControl.control.markAsUntouched();
    }
    // Force Angular to check for changes
    this.cdr.markForCheck();
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
    const value = this.control.value;

    if (this.selectionMode === 'range') {
      if (!this.required) {
        return null;
      }

      if (!Array.isArray(value) || value.length !== 2) {
        return { required: true };
      }

      const [start, end] = value;

      if (!start || !end) {
        return { required: true };
      }

      return null;
    }

    // Required validation: treat null, undefined, empty string as missing; Date objects are valid
    if (this.required && (value === null || value === undefined || value === '')) {
      return { required: true };
    }

    if (
      !isEmpty(value) &&
      !moment(value, Constants.DATE_FORMAT, true).isValid()
    ) {
      return {
        datePattern: { pattern: Constants.DATE_FORMAT, actualValue: value },
      };
    }

    if (this.minDateToday) {
      return minDateToday()(control);
    }

    if (this.maxDateToday) {
      return maxDateToday()(control);
    }

    if (this.maxDateFromNow) {
      return maxDateFromNow(
        this.maxDateFromNow,
        this.maxDateFromNowType
      )(control);
    }

    if (this.minDate) {
      return minDate(this.minDate)(control);
    }

    if (this.maxDate) {
      return maxDate(this.maxDate)(control);
    }

    return null;
  }
}
