import {Component, EventEmitter, Input, Output} from '@angular/core';
import {PaginatorModule} from "primeng/paginator";

@Component({
    selector: 'app-eco-paginator',
    imports: [
        PaginatorModule
    ],
    templateUrl: './eco-paginator.component.html',
    styleUrl: './eco-paginator.component.scss'
})
export class EcoPaginatorComponent {
  @Input() paging: {
    page: number,
    rows: number,
    totalRecords: number
  } = {
    page: 0,
    rows: 10,
    totalRecords: 0
  }
  @Output() changePage = new EventEmitter();
  onPageChange(event: any) {
    this.changePage.emit(event)
  }
}
