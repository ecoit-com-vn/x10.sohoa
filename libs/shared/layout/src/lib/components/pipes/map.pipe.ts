import { Pipe, PipeTransform } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ColumnDataType } from '@sohoa.frontend/shared/core';

@Pipe({
  name: 'map',
  standalone: true
})
export class MapPipe implements PipeTransform {
  private datePipe = new DatePipe('en-US'); // Fallback manual instantiation if injection fails

  transform(value: any, type: any): any {
    if (value === null || value === undefined) return '';
    if (type === ColumnDataType.Date) {
      // Handle standard ISO dates or timestamp numbers
      return this.datePipe.transform(value, 'dd/MM/yyyy') || '';
    }
    return value;
  }
}
