import { ColumnDataType } from '../utils/enums';

export interface IColumn {
  header?: string;
  fieldName: string;
  type?: ColumnDataType;
  class?: string;
  columnActions?: any[];
  width?: string;
  visible?: boolean;
  headerName?: string;
  tdTemplate?: any;
}

export interface IActionOption<T = any> {
  label: string;
  icon?: string;
  class?: string;
  visible?: (row: T) => boolean;
  func?: (row: T) => void;
}

export interface IDataTable<T = any> {
  content: T[];
  totalElements?: number;
}

export interface IPageConfig {
  page: number;
  size: number;
  totalRecords: number;
}
