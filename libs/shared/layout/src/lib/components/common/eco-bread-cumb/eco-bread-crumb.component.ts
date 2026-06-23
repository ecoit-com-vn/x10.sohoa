import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-breadcrumb',
  imports: [CommonModule, RouterModule],
  templateUrl: './eco-bread-crumb.component.html',
  styleUrl: './eco-bread-crumb.component.scss',
})
export class BreadCrumbComponent {
  @Input() breadcrumbItems: { key: string; link: string; label: string }[] = [];
  constructor() {}
  ngOnInit() {}
}
