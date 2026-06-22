import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'tableFieldPipe',
  standalone: true
})
export class TableFieldPipe implements PipeTransform {
  transform(value: any, fieldName: string): any {
    if (!value || !fieldName) return '';
    const fields = fieldName.split('.');
    let result = value;
    for (const field of fields) {
      result = result ? result[field] : undefined;
    }
    return result !== undefined && result !== null ? result : '';
  }
}
