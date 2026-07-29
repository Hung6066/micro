import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, HostListener, Output, effect, inject, input } from '@angular/core';
import { HisHopeTranslatePipe } from '../i18n/his-hope-translate.pipe';

@Component({
  selector: 'hh-confirm-dialog',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="hh-dialog-backdrop" role="presentation" (click)="cancel()">
        <section [id]="dialogId" class="hh-dialog" role="alertdialog" aria-modal="true"
                 [attr.aria-labelledby]="titleId" [attr.aria-describedby]="messageId"
                 (click)="$event.stopPropagation()">
          <div class="hh-dialog__icon material-icons" aria-hidden="true">warning_amber</div>
          <h2 [id]="titleId">{{ title() | hhTranslate }}</h2>
          <p [id]="messageId">{{ message() | hhTranslate }}</p>
          <div class="hh-dialog__actions">
            <button type="button" class="hh-button hh-button--secondary" (click)="cancel()">{{ cancelLabel() | hhTranslate }}</button>
            <button type="button" class="hh-button hh-button--danger" (click)="confirm()">{{ confirmLabel() | hhTranslate }}</button>
          </div>
        </section>
      </div>
    }
  `,
})
export class HisHopeConfirmDialogComponent {
  readonly open = input(false);
  readonly title = input('common.confirmAction');
  readonly message = input('common.confirmContinue');
  readonly confirmLabel = input('common.yes');
  readonly cancelLabel = input('common.cancel');
  readonly titleId = `hh-dialog-title-${Math.random().toString(36).slice(2)}`;
  readonly messageId = `hh-dialog-message-${Math.random().toString(36).slice(2)}`;
  readonly dialogId = `hh-dialog-${Math.random().toString(36).slice(2)}`;
  private previouslyFocused: HTMLElement | null = null;

  private readonly document = inject(DOCUMENT);

  constructor() {
    effect(() => {
      if (this.open()) {
        this.previouslyFocused = this.document.activeElement as HTMLElement | null;
        queueMicrotask(() => this.focusFirst());
      }
    });
  }

  @Output() readonly confirmed = new EventEmitter<void>();
  @Output() readonly cancelled = new EventEmitter<void>();

  confirm(): void {
    this.confirmed.emit();
    this.restoreFocus();
  }

  cancel(): void {
    this.cancelled.emit();
    this.restoreFocus();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) this.cancel();
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (!this.open() || event.key !== 'Tab') return;
    const dialog = this.document.getElementById(this.dialogId);
    const focusable = dialog ? Array.from(dialog.querySelectorAll<HTMLElement>('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')) : [];
    if (!focusable.length) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && this.document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && this.document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private focusFirst(): void {
    this.document.getElementById(this.dialogId)?.querySelector<HTMLElement>('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')?.focus();
  }

  private restoreFocus(): void {
    queueMicrotask(() => this.previouslyFocused?.focus());
  }
}
