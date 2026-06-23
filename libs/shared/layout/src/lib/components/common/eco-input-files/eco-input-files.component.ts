import { Component, forwardRef, Input } from '@angular/core';
import {
  AbstractControl,
  ControlValueAccessor,
  FormsModule,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  ValidationErrors,
  Validator,
} from '@angular/forms';
import { NgIf } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';
import { MessageService } from 'primeng/api';

export interface IFileInput {
  FileName: string;
  Content?: File;
  ServerRelativeUrl?: string | null;
}

@Component({
    selector: 'eco-input-files',
    templateUrl: './eco-input-files.component.html',
    styleUrl: './eco-input-files.component.scss',
    providers: [
        {
            provide: NG_VALIDATORS,
            useExisting: forwardRef(() => EcoInputFilesComponent),
            multi: true,
        },
        {
            provide: NG_VALUE_ACCESSOR,
            useExisting: forwardRef(() => EcoInputFilesComponent),
            multi: true,
        },
    ],
    imports: [NgIf, InputTextModule, ButtonModule, FormsModule, ChipModule]
})
export class EcoInputFilesComponent implements ControlValueAccessor, Validator {
  @Input() label: string = 'EMPTY';
  @Input() options: any;
  @Input() readonly: boolean = false;
  @Input() tooltip?: any;
  @Input() showLabel: boolean = true;
  @Input() disabled: boolean = false;
  @Input() accept: string | null = null;
  @Input() maxFileSize: number | null = null;
  @Input() multiple: boolean = true

  value: any;
  files: IFileInput[] = [];

  constructor(
    private messageService: MessageService) {}

  chooseFile(inputFile: HTMLInputElement) {
    inputFile.click();
  }

  onChange = (_value: IFileInput[]) => {};

  onTouched = () => {};

  writeValue(files: IFileInput[]): void {
    this.files = files ?? [];
  }

  registerOnChange(fn: (value: IFileInput[]) => void): void {
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

  onChangeInput(event: any) {
    let fileInvalid: string[] = [];
    for (const file of event.target.files) {
      if (this.accept && !this.accept.includes(file.name.substring(file.name.lastIndexOf('.')))) {
        fileInvalid.push(file.name);
      } else {
        this.files.push({
          FileName: file.name,
          Content: file,
          ServerRelativeUrl: null,
        });
      }
    }
    this.onChange(this.files);
    if (fileInvalid.length > 0) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi',
        detail: `Định dạng tệp không hợp lệ: ${fileInvalid.join(', ')}`,
      });
    }
  }

  onRemoveFile(index: number) {
    this.files.splice(index, 1);
    this.onChange(this.files);
  }
}
