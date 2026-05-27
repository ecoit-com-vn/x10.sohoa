import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DatePicker } from 'primeng/datepicker';
import { Select } from 'primeng/select';
import { CardModule } from 'primeng/card';

export interface FormField {
  name: string;
  label: string;
  type: string;
  required?: boolean;
}

export interface FormDefinition {
  formName: string;
  fields: FormField[];
}

@Component({
  selector: 'app-form-renderer',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule, InputNumberModule, DatePicker, Select, CardModule],
  templateUrl: './form-renderer.component.html',
  styleUrls: ['./form-renderer.component.scss']
})
export class FormRendererComponent implements OnInit, OnChanges {
  @Input() formDefinition: FormDefinition | null = null;
  
  dynamicForm!: FormGroup;

  constructor(private fb: FormBuilder) {}

  ngOnInit() {
    this.buildForm();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['formDefinition'] && !changes['formDefinition'].firstChange) {
      this.buildForm();
    }
  }

  buildForm() {
    this.dynamicForm = this.fb.group({});
    
    if (this.formDefinition && this.formDefinition.fields) {
      this.formDefinition.fields.forEach(field => {
        const validators = field.required ? [Validators.required] : [];
        this.dynamicForm.addControl(field.name, this.fb.control('', validators));
      });
    }
  }

  onSubmit() {
    if (this.dynamicForm.valid) {
      console.log('Dynamic form data:', this.dynamicForm.value);
    }
  }
}
