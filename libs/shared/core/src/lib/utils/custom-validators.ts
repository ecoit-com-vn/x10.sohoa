import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import moment from 'moment';

export function identificationType(type: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (!value) return null;
    if (type === 'cccd' && !/^\d{12}$/.test(value)) {
      return { identificationType: true };
    }
    if (type === 'cmt' && !/^\d{9}$/.test(value)) {
      return { identificationType: true };
    }
    return null;
  };
}

export function maxDate(max: Date | string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (!value) return null;
    const date = moment(value);
    const maxMoment = moment(max);
    if (date.isAfter(maxMoment, 'day')) {
      return { maxDate: true };
    }
    return null;
  };
}

export function maxDateFromNow(value: number, unit: any): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const val = control.value;
    if (!val) return null;
    const date = moment(val);
    const maxMoment = moment().add(value, unit);
    if (date.isAfter(maxMoment, 'day')) {
      return { maxDateFromNow: true };
    }
    return null;
  };
}

export function maxDateToday(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (!value) return null;
    const date = moment(value);
    const today = moment();
    if (date.isAfter(today, 'day')) {
      return { maxDateToday: true };
    }
    return null;
  };
}

export function minDate(min: Date | string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (!value) return null;
    const date = moment(value);
    const minMoment = moment(min);
    if (date.isBefore(minMoment, 'day')) {
      return { minDate: true };
    }
    return null;
  };
}

export function minDateToday(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (!value) return null;
    const date = moment(value);
    const today = moment();
    if (date.isBefore(today, 'day')) {
      return { minDateToday: true };
    }
    return null;
  };
}
