import { Component, Input, OnInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { WfBreadcrumbComponent } from '@sohoa.frontend/shared/layout';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormsModule } from '@angular/forms';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { DatePickerModule } from 'primeng/datepicker';

export interface FormField {
  name: string;
  label: string;
  type: string;
  required?: boolean;
  options?: string[];
  placeholder?: string;
  width?: number; // 50 or 100
  active?: boolean;
  /** Tên trường tương ứng trong dữ liệu thongSoKyThuat do PMIS trả về — dùng để so khớp khi so sánh sai khác. */
  pmisFieldName?: string;
}

export interface FormDefinition {
  formName: string;
  fields: FormField[];
}

@Component({
  selector: 'app-form-renderer',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, ToastModule, DatePickerModule, WfBreadcrumbComponent],
  providers: [MessageService],
  templateUrl: './form-renderer.component.html',
  styleUrls: ['./form-renderer.component.scss']
})
export class FormRendererComponent implements OnInit, OnChanges {
  @Input() formDefinition: FormDefinition | null = null;
  
  dynamicForm!: FormGroup;
  selectedFormKey: string = 'transformer';
  submittedData: any = null;

  private fb = inject(FormBuilder);
  private messageService = inject(MessageService);

  // Danh sách các biểu mẫu EAV mặc định để minh họa nếu không nhận được Input
  mockForms: Record<string, FormDefinition> = {
    transformer: {
      formName: 'Biểu mẫu nhập liệu Máy biến áp dầu 110kV',
      fields: [
        { name: 'ten_thiet_bi', label: 'Tên máy biến áp', type: 'text', required: true, placeholder: 'Ví dụ: MBA T1 Đông Anh...', width: 100 },
        { name: 'hang_san_xuat', label: 'Hãng sản xuất', type: 'text', required: true, placeholder: 'Ví dụ: ABB, Dong Anh...', width: 50 },
        { name: 'nam_san_xuat', label: 'Năm sản xuất', type: 'number', required: false, placeholder: '2025', width: 50 },
        { name: 'dien_ap_dinh_muc', label: 'Điện áp định mức (kV)', type: 'dropdown', required: true, options: ['110kV', '220kV', '500kV'], width: 50 },
        { name: 'dung_luong', label: 'Dung lượng định mức (MVA)', type: 'number', required: true, placeholder: 'Ví dụ: 63, 125, 250...', width: 50 },
        { name: 'ngay_van_hanh', label: 'Ngày đưa vào vận hành', type: 'date', required: true, width: 50 },
        { name: 'tinh_trang', label: 'Tình trạng kỹ thuật dầu cách điện', type: 'textarea', required: false, placeholder: 'Mô tả chi tiết tình trạng...', width: 100 }
      ]
    },
    pole: {
      formName: 'Biểu mẫu kiểm tra Cột điện & Hành lang tuyến',
      fields: [
        { name: 'ma_so_cot', label: 'Mã số vị trí cột điện', type: 'text', required: true, placeholder: 'Ví dụ: VTr 12/A...', width: 50 },
        { name: 'loai_cot', label: 'Loại cột', type: 'dropdown', required: true, options: ['Cột ly tâm tròn', 'Cột chữ chữ I', 'Cột thép góc', 'Cột thép ống đơn'], width: 50 },
        { name: 'chieu_cao', label: 'Chiều cao cột (m)', type: 'number', required: false, placeholder: 'Ví dụ: 12, 14, 16...', width: 50 },
        { name: 'ngay_kiem_tra', label: 'Ngày kiểm tra định kỳ', type: 'date', required: true, width: 50 },
        { name: 'dat_tieu_chuan', label: 'Đạt tiêu chuẩn vận hành an toàn', type: 'checkbox', required: true, placeholder: 'Xác nhận đạt tiêu chuẩn an toàn', width: 100 }
      ]
    },
    incident: {
      formName: 'Biểu mẫu báo cáo sự cố đường dây truyền tải',
      fields: [
        { name: 'tuyen_duong_day', label: 'Tên tuyến đường dây xảy ra sự cố', type: 'text', required: true, placeholder: 'Ví dụ: ĐD 110kV Hà Đông - Thanh Xuân...', width: 100 },
        { name: 'thoi_gian_xay_ra', label: 'Thời gian xảy ra sự cố', type: 'date', required: true, width: 50 },
        { name: 'loai_su_co', label: 'Loại hình sự có lưới điện', type: 'dropdown', required: true, options: ['Sự cố thoáng qua', 'Sự cố vĩnh cửu', 'Quá tải đường dây', 'Chạm chập hành lang an toàn'], width: 50 },
        { name: 'mo_ta_chi_tiet', label: 'Mô tả chi tiết nguyên nhân & hiện trạng', type: 'textarea', required: true, placeholder: 'Mô tả chi tiết sự việc phục vụ khắc phục...', width: 100 }
      ]
    }
  };

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
    this.submittedData = null;
    
    const activeDef = this.getActiveDefinition();
    if (activeDef && activeDef.fields) {
      activeDef.fields.forEach(field => {
        if (field.active === false) return;
        const validators = field.required ? [Validators.required] : [];
        // Checkbox defaults to false, other inputs to empty string
        const defaultValue = field.type === 'checkbox' ? false : '';
        this.dynamicForm.addControl(field.name, this.fb.control(defaultValue, validators));
      });
    }
  }

  getActiveDefinition(): FormDefinition | null {
    if (this.formDefinition) {
      return this.formDefinition;
    }
    return this.mockForms[this.selectedFormKey] || null;
  }

  onFormSelectorChange() {
    this.buildForm();
  }

  onSubmit() {
    if (this.dynamicForm.valid) {
      this.submittedData = this.dynamicForm.value;
      console.log('Dynamic form data:', this.submittedData);
      this.messageService.add({
        severity: 'success',
        summary: 'Gửi biểu mẫu thành công',
        detail: 'Dữ liệu thuộc tính thiết bị đã được ghi nhận trong cơ sở dữ liệu số hóa!'
      });
    } else {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi kiểm tra dữ liệu',
        detail: 'Vui lòng điền đầy đủ các trường bắt buộc màu đỏ!'
      });
    }
  }

  resetForm() {
    this.dynamicForm.reset();
    const activeDef = this.getActiveDefinition();
    if (activeDef && activeDef.fields) {
      activeDef.fields.forEach(field => {
        if (field.type === 'checkbox') {
          this.dynamicForm.patchValue({ [field.name]: false });
        }
      });
    }
    this.submittedData = null;
  }
}
