import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { Toolbar } from 'primeng/toolbar';
import { Button } from 'primeng/button';
import { PanelMenu } from 'primeng/panelmenu';
import { MenuItem } from 'primeng/api';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterModule, Toolbar, Button, PanelMenu],
  template: `
    <div class="layout-wrapper">
      <div class="layout-header">
        <p-toolbar>
          <ng-template #start>
            <h3>EVNHANOI Digitization</h3>
          </ng-template>
          <ng-template #end>
            <p-button icon="pi pi-user" [rounded]="true" [text]="true" severity="secondary" />
          </ng-template>
        </p-toolbar>
      </div>
      <div class="layout-main">
        <div class="layout-sidebar">
          <p-panelMenu [model]="items" [style]="{'width':'100%'}" />
        </div>
        <div class="layout-content">
          <router-outlet></router-outlet>
        </div>
      </div>
    </div>
  `,
  styles: `
    .layout-wrapper {
      display: flex;
      flex-direction: column;
      height: 100vh;
    }
    .layout-header {
      flex: 0 0 auto;
    }
    .layout-main {
      display: flex;
      flex: 1 1 auto;
      overflow: hidden;
    }
    .layout-sidebar {
      flex: 0 0 250px;
      overflow-y: auto;
      border-right: 1px solid var(--p-content-border-color, #dee2e6);
      padding: 1rem;
    }
    .layout-content {
      flex: 1 1 auto;
      overflow-y: auto;
      padding: 2rem;
    }
  `,
})
export class AdminLayout {
  items: MenuItem[] = [
    {
      label: 'Administration',
      icon: 'pi pi-fw pi-cog',
      items: [
        {
          label: 'User Management',
          icon: 'pi pi-fw pi-users',
          routerLink: ['/administration/user-management']
        }
      ]
    }
  ];
}
