import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';

@Component({
  selector: 'app-equipment-search',
  standalone: true,
  imports: [CommonModule, FormsModule, InputTextModule, ButtonModule, TableModule],
  template: `
    <div class="equipment-search">
      <h2>Equipment Search</h2>
      <div class="search-bar" style="margin-bottom: 20px; display: flex; gap: 10px;">
        <input pInputText type="text" [(ngModel)]="searchQuery" placeholder="Search for equipment..." style="flex: 1; max-width: 400px;" />
        <p-button label="Search" icon="pi pi-search" (onClick)="onSearch()"></p-button>
      </div>

      <p-table [value]="results" [paginator]="true" [rows]="10" [tableStyle]="{ 'min-width': '50rem' }" [loading]="loading">
        <ng-template pTemplate="header">
          <tr>
            <th>ID</th>
            <th>Name</th>
            <th>Type</th>
            <th>Status</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-item>
          <tr>
            <td>{{ item.id }}</td>
            <td>{{ item.name }}</td>
            <td>{{ item.type }}</td>
            <td>{{ item.status }}</td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="4">No equipment found.</td>
          </tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: []
})
export class EquipmentSearchComponent {
  searchQuery: string = '';
  results: any[] = [];
  loading: boolean = false;

  onSearch() {
    this.loading = true;
    // Simulate Elasticsearch API call
    setTimeout(() => {
      this.results = [
        { id: 'EQ-001', name: 'Transformer A1', type: 'Transformer', status: 'Active' },
        { id: 'EQ-002', name: 'Cable X2', type: 'Cable', status: 'Maintenance' },
        { id: 'EQ-003', name: 'Switchgear M5', type: 'Switchgear', status: 'Active' }
      ].filter(item => 
        item.name.toLowerCase().includes(this.searchQuery.toLowerCase()) || 
        item.type.toLowerCase().includes(this.searchQuery.toLowerCase())
      );
      this.loading = false;
    }, 500);
  }
}
