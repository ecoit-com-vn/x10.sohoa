import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  input,
  output
} from '@angular/core';
import { DialogModule } from 'primeng/dialog';

@Component({
  // eslint-disable-next-line @angular-eslint/component-selector -- Selector dùng chung theo contract ứng dụng.
  selector: 'app-delete-confirm-dialog',
  standalone: true,
  imports: [DialogModule],
  templateUrl: './delete-confirm-dialog.component.html',
  styleUrl: './delete-confirm-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Overlay append vào body nên style global được giới hạn bằng prefix riêng.
  encapsulation: ViewEncapsulation.None
})
export class DeleteConfirmDialogComponent {
  readonly visible = input.required<boolean>();
  readonly entityLabel = input.required<string>();
  readonly targetName = input.required<string>();

  readonly header = input<string>('Xác nhận xóa');
  readonly consequence = input<string>(
    'sẽ bị xóa vĩnh viễn khỏi hệ thống.'
  );
  readonly loading = input<boolean>(false);

  readonly cancelled = output<void>();
  readonly confirmed = output<void>();

  protected onVisibleChange(visible: boolean): void {
    // visible=false xảy ra khi bấm nút X hoặc nhấn Escape.
    if (!visible && !this.loading()) {
      this.cancelled.emit();
    }
  }

  protected onCancel(): void {
    // Không cho đóng popup khi request xóa đang xử lý.
    if (!this.loading()) {
      this.cancelled.emit();
    }
  }

  protected onConfirm(): void {
    // Chặn phát nhiều sự kiện khi người dùng bấm liên tục.
    if (!this.loading()) {
      this.confirmed.emit();
    }
  }
}
