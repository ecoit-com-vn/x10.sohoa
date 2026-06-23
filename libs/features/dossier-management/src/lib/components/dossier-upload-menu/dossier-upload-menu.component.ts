import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';

export type DossierUploadAction = 'folder' | 'direct' | 'scan';

@Component({
  selector: 'app-dossier-upload-menu',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  templateUrl: './dossier-upload-menu.component.html',
  styleUrl: './dossier-upload-menu.component.scss',
})
export class DossierUploadMenuComponent {
  disabled = input(false);

  actionSelected = output<DossierUploadAction>();

  menuOpen = signal(false);

  toggleMenu(event: Event): void {
    event.stopPropagation();
    if (this.disabled()) return;
    this.menuOpen.update((v) => !v);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  selectAction(action: DossierUploadAction): void {
    this.actionSelected.emit(action);
    this.closeMenu();
  }

  onDocumentClick(): void {
    this.closeMenu();
  }
}
