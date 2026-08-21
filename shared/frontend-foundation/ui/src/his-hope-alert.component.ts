import { ChangeDetectionStrategy, Component, EventEmitter, input, Output } from '@angular/core';

export type HisHopeAlertTone = 'info' | 'success' | 'warning' | 'error';

@Component({
  selector: 'hh-alert',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-alert" [class]="'hh-alert hh-alert--' + tone()" [attr.role]="tone() === 'error' ? 'alert' : 'status'" aria-live="polite">
      <span class="material-icons" aria-hidden="true">{{ icon() }}</span>
      <div class="hh-alert__content"><strong>{{ title() }}</strong><ng-content /></div>
      @if (dismissible()) { <button type="button" class="hh-alert__close" aria-label="Dismiss" (click)="dismissed.emit()"><span class="material-icons" aria-hidden="true">close</span></button> }
    </section>
  `,
  styles: [`
    :host { display: block; }
    .hh-alert { display: flex; align-items: flex-start; gap: var(--space-md); padding: var(--space-md) var(--space-lg); border: 1px solid; border-radius: var(--radius-card); }
    .hh-alert--info { border-color: var(--color-info); background: color-mix(in srgb, var(--color-info) 10%, var(--surface-white)); }
    .hh-alert--success { border-color: var(--color-success); background: color-mix(in srgb, var(--color-success) 10%, var(--surface-white)); }
    .hh-alert--warning { border-color: var(--color-warning); background: color-mix(in srgb, var(--color-warning) 10%, var(--surface-white)); }
    .hh-alert--error { border-color: var(--color-danger); background: color-mix(in srgb, var(--color-danger) 10%, var(--surface-white)); }
    .hh-alert__content { flex: 1; min-width: 0; display: grid; gap: var(--space-xxs); }
    .hh-alert__content strong { font-size: var(--font-size-body); }
    .hh-alert__close { display: grid; place-items: center; width: var(--space-3xl); height: var(--space-3xl); margin: -var(--space-2xs) -var(--space-2xs) 0 0; border: 0; background: transparent; color: inherit; cursor: pointer; }
  `],
})
export class HisHopeAlertComponent {
  readonly tone = input<HisHopeAlertTone>('info');
  readonly title = input('Notice');
  readonly dismissible = input(false);
  readonly icon = input('info');
  @Output() readonly dismissed = new EventEmitter<void>();
}