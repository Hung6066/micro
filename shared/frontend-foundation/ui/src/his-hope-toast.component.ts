import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { HisHopeToastService } from './his-hope-toast.service';

@Component({
  selector: 'hh-toast-outlet',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-toast-outlet" aria-live="polite" aria-atomic="false">
      @for (toast of toastService.toasts(); track toast.id) {
        <article class="hh-toast" [class]="'hh-toast hh-toast--' + toast.tone" [attr.role]="toast.tone === 'error' ? 'alert' : 'status'">
          <span class="material-icons hh-toast__icon" aria-hidden="true">{{ iconFor(toast.tone) }}</span>
          <div class="hh-toast__body">
            <p>{{ toast.message }}</p>
            @if (toast.detail) { <small>{{ toast.detail }}</small> }
          </div>
          <button type="button" class="hh-toast__close" (click)="toastService.dismiss(toast.id)" aria-label="Dismiss notification">
            <span class="material-icons" aria-hidden="true">close</span>
          </button>
        </article>
      }
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .hh-toast-outlet { position: fixed; top: 76px; right: 24px; z-index: 1100; display: grid; gap: 10px; width: min(380px, calc(100vw - 32px)); pointer-events: none; }
    .hh-toast { display: flex; align-items: flex-start; gap: 10px; padding: 12px 12px 12px 14px; border: 1px solid var(--border-default); border-left: 3px solid var(--color-info); border-radius: var(--radius-card); background: var(--surface-white); color: var(--text-primary); box-shadow: 0 8px 24px rgba(15, 35, 25, .14); pointer-events: auto; }
    .hh-toast--success { border-left-color: var(--color-success); }
    .hh-toast--warning { border-left-color: var(--color-warning); }
    .hh-toast--error { border-left-color: var(--color-danger); }
    .hh-toast__icon { color: var(--color-info); font-size: 20px; }
    .hh-toast--success .hh-toast__icon { color: var(--color-success); }
    .hh-toast--warning .hh-toast__icon { color: var(--color-warning); }
    .hh-toast--error .hh-toast__icon { color: var(--color-danger); }
    .hh-toast__body { flex: 1; min-width: 0; }
    .hh-toast__body p, .hh-toast__body small { display: block; margin: 0; }
    .hh-toast__body p { font-size: var(--font-size-body); font-weight: 600; line-height: 1.4; }
    .hh-toast__body small { margin-top: 2px; color: var(--text-secondary); font-size: var(--font-size-caption); line-height: 1.4; }
    .hh-toast__close { display: grid; place-items: center; width: 32px; height: 32px; margin: -4px -4px 0 0; border: 0; border-radius: var(--radius-button); color: var(--text-secondary); background: transparent; cursor: pointer; }
    .hh-toast__close:hover { background: var(--surface-muted); }
    .hh-toast__close .material-icons { font-size: 18px; }
    @media (max-width: 768px) { .hh-toast-outlet { top: 16px; right: 16px; } }
  `],
})
export class HisHopeToastComponent {
  readonly toastService = inject(HisHopeToastService);

  iconFor(tone: string): string {
    return { success: 'check_circle', info: 'info', warning: 'warning', error: 'error' }[tone] ?? 'info';
  }
}
