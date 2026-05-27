import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'app-form-builder',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule, Select, CheckboxModule, CardModule],
  templateUrl: './form-builder.component.html',
  styleUrls: ['./form-builder.component.scss']
})
export class FormBuilderComponent implements OnInit {
  formDefinition!: FormGroup;
  fieldTypes = [
    { label: 'Text', value: 'text' },
    { label: 'Number', value: 'number' },
    { label: 'Date', value: 'date' },
    { label: 'Dropdown', value: 'dropdown' }
  ];

  constructor(private fb: FormBuilder) {}

  ngOnInit() {
    this.formDefinition = this.fb.group({
      formName: ['', Validators.required],
      fields: this.fb.array([])
    });
  }

  get fields(): FormArray {
    return this.formDefinition.get('fields') as FormArray;
  }

  addField() {
    this.fields.push(this.fb.group({
      name: ['', Validators.required],
      label: ['', Validators.required],
      type: ['text', Validators.required],
      required: [false]
    }));
  }

  removeField(index: number) {
    this.fields.removeAt(index);
  }

  saveForm() {
    if (this.formDefinition.valid) {
      console.log('Form saved:', this.formDefinition.value);
    }
  }
}
