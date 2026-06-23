import { ChangeDetectorRef, Component, EventEmitter, HostListener, Input, OnInit, Output, TemplateRef, ViewChild, } from '@angular/core';
import { IActionOption, IColumn, IDataTable, IPageConfig, LoadingService } from '@sohoa.frontend/shared/core';
import { MenuItem, TreeNode } from 'primeng/api';
import { ColumnDataType } from '@sohoa.frontend/shared/core';
import { Table, TableModule, TablePageEvent } from 'primeng/table';
import { TreeTableModule, TreeTablePaginatorState } from 'primeng/treetable';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PaginatorModule } from 'primeng/paginator';
import { MenuModule } from 'primeng/menu';
import { MapPipe, TableFieldPipe } from '../../pipes';
import { SkeletonModule } from 'primeng/skeleton';
import { LoadingComponent } from '../loading/loading.component';
import { StyleClassModule } from 'primeng/styleclass';
import { TooltipModule } from 'primeng/tooltip';

enum SelectionMode {
  Single = 'single',
  Checkbox = 'multiple',
}

@Component({
  selector: 'eco-table',
  templateUrl: './eco-table.component.html',
  styleUrl: './eco-table.component.scss',
  imports: [
    CommonModule,
    TableModule,
    TreeTableModule,
    FormsModule,
    TableFieldPipe,
    // DecimalPipe,
    PaginatorModule,
    MenuModule,
    MapPipe,
    SkeletonModule,
    LoadingComponent,
    StyleClassModule,
    TooltipModule,
  ],
})
export class EcoTableComponent implements OnInit {
  @Input() columns: IColumn[] = [];
  @Input() actions: IActionOption[] = [];
  @Input() paginator = true;
  @Input() showHeader = true;
  @Input() showOrderNumber = true;
  @Input() scrollable = true;
  @Input() virtualScroll = false;
  @Input() loading = false;
  @Input() keyRow: string = 'ID';
  @Input() selectionMode: 'single' | 'multiple' | null = null;
  @Input() selectedItems: any;
  @Input() selectedRow: any;
  @Input() frozenValue: any[] = [];
  @Input() treeTable = false;
  @Input() dataTable?: IDataTable;
  @Input() dataTree?: TreeNode[];
  @Input() footerTable?: TemplateRef<any>;
  @Input() pageConfig: IPageConfig = {
    page: 0,
    size: 10,
    totalRecords: 0,
  };
  @Output() onPageChange: EventEmitter<{ page: number; size: number }> =
    new EventEmitter();
  @Output() selectedItemsChange: EventEmitter<any[]> = new EventEmitter<any>();
  @Output() onDbClickRow: EventEmitter<any> = new EventEmitter<any>();
  @Output() onClickRow: EventEmitter<any> = new EventEmitter<any>();
  // @Output() onAction: EventEmitter<{ type: string; item: any }> = new EventEmitter<{
  //   type: string;
  //   item: any;
  // }>();
  @Output() onLazyLoad: EventEmitter<any> = new EventEmitter<any>();
  @ViewChild('table') table: Table | undefined;
  items: MenuItem[] = [];
  itemFocus: any;
  columnDataType = ColumnDataType;
  lastId = 0;
  SelectionMode = SelectionMode;
  currentPageReportTemplate: string = '';
  isMobile = false;

  constructor(
    public loadingService: LoadingService,
    private cdr: ChangeDetectorRef) {
    this.getReportTable();
    
  }

  getReportTable() {
    this.currentPageReportTemplate = 'Hiển thị {first} đến {last} của {totalRecords} bản ghi';
  }
  ngOnInit() {
    this.checkScreen();
    window.addEventListener('resize', () => this.checkScreen());
  }

  @HostListener('window:resize')
  onResize() {
    this.checkScreen();
  }

  checkScreen() {
    const mobile = window.innerWidth < 992;
    if (mobile !== this.isMobile) {
      this.isMobile = mobile;
      this.cdr.detectChanges();
    }
  }
  toMenuItems<T>(options: IActionOption<T>[], row: T): MenuItem[] {
    return options.map((opt) => ({
      label: opt.label,
      icon: opt.icon,
      styleClass: opt.class ?? '',
      visible: opt.visible ? opt.visible(row) : true,
      command: () => opt.func?.(row),
    }));
  }

  // getActionsItem(itemRow: any) {
  //   return this.actions
  //     .filter((itemF) => !!itemF.visible && itemF.visible(itemRow))
  //     .map((itemOp, i) => {
  //       return {
  //         label: itemOp.label,
  //         icon: itemOp.icon,
  //         iconTrigger: itemOp.iconTrigger,
  //         styleClass: itemOp.class,
  //         command: () => {
  //           if (itemOp.func) {
  //             itemOp.func(itemRow);
  //           }
  //         },
  //       };
  //     });
  // }
  getActionsItem(itemRow: any): IActionOption[] {
    return this.actions.filter(
      (itemF) => !!itemF.visible && itemF.visible(itemRow),
    );
  }

  getColumnActions(itemRow: any, column: IColumn) {
    const actions = column.columnActions || [];
    return actions
      .filter((itemF) => !!itemF.visible && itemF.visible(itemRow))
      .map((itemOp) => {
        return {
          label: itemOp.label,
          icon: itemOp.icon,
          styleClass: itemOp.class,
          command: () => {
            if (itemOp.func) {
              itemOp.func(itemRow);
            }
          },
        };
      });
  }

  doClickRow(event: any, row: any) {
    const target = event.target as HTMLElement;
    if (target.closest('.p-checkbox')) {
      return;
    }
    if (target.closest('.column-action') || target.closest('.eco-row-action')) {
      return;
    }
    this.itemFocus = row;
    this.onClickRow.emit(row);
  }

  doDblClickRow(event: any, row: any) {
    this.itemFocus = row;
    if (
      event.target.classList.contains('column-action') ||
      event.target.classList.contains('eco-row-action')
    ) {
      return;
    }
    this.onDbClickRow.emit(row);
  }

  trackByHeader(_index: number, item: IColumn) {
    return item;
  }

  trackByBody(_index: number, item: IColumn) {
    return item;
  }

  onSelectionChange(selection: any) {
    if (this.selectionMode === SelectionMode.Single) {
      this.selectedItemsChange.emit([selection]);
    } else {
      this.selectedItemsChange.emit(selection);
    }
  }

  protected readonly ColumnDataType = ColumnDataType;

  pageChange(event: TablePageEvent) {
    const page = event.first / event.rows + 1 || 0;
    const size = event.rows || 15;
    this.onPageChange.emit({
      page: page,
      size: size,
    });
  }

  doClickRowTree(node: TreeNode) {
    this.itemFocus = node.data;
    this.onClickRow.emit(node.data);
  }

  doDblClickRowTree(node: TreeNode) {
    this.itemFocus = node.data;
    this.onDbClickRow.emit(node.data);
  }

  onCheckboxClick(event: MouseEvent) {
    event.stopPropagation();
  }
}
