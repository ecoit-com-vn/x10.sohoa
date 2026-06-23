import { Component, Input, Output, EventEmitter, TemplateRef, ViewChild } from '@angular/core'; import { CommonModule } from '@angular/common'; import { TreeTable, TreeTableModule, TreeTableToggler } from 'primeng/treetable'; import { TreeNode } from 'primeng/api'; import { MenuModule } from 'primeng/menu';  import { IActionOption, IColumn } from '@sohoa.frontend/shared/core';
import { ColumnDataType } from '@sohoa.frontend/shared/core';

@Component({
  selector: 'eco-tree-table',
  standalone: true,
  imports: [
    CommonModule,
    TreeTableModule,
    MenuModule,
    ],
  templateUrl: './tabletree.component.html',
  styleUrls: ['./tabletree.component.scss']
})
export class EcoTreeTableComponent {
  @Input() columns: IColumn[] = [];
  @Input() data: TreeNode[] = [];        // DÙNG TREE NODE, KHÁC TABLE THƯỜNG
  @Input() showHeader = true;
  @Input() showOrder = false;

  @Input() actions: IActionOption[] = [];
  @Input() loading = false;

  @Output() onClickRow = new EventEmitter<any>();
  @Output() onDblClickRow = new EventEmitter<any>();

  columnDataType = ColumnDataType;

  onRowClick(event: any, node: TreeNode) {
    this.onClickRow.emit(node.data);
  }

  onRowDblClick(event: any, node: TreeNode) {
    this.onDblClickRow.emit(node.data);
  }

  getActionsItem(item: any) {
    return this.actions
      .filter(x => !!x.visible && x.visible(item))
      .map(x => ({
        label: x.label,
        icon: x.icon,
        command: () => x.func && x.func(item)
      }));
  }

  getFieldValue(rowData: any, fieldName: any): any {
    return fieldName ? rowData?.[fieldName] ?? '-' : '-';
  }

  getRowIndex(rowNode: any): number {
    return rowNode?.index != null ? rowNode.index + 1 : 0;
  }
}
