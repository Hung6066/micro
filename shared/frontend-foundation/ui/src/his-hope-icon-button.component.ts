import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'hh-icon-button',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button type="button" [disabled]="disabled()" [attr.aria-label]="label()"><span class="material-icons" aria-hidden="true">{{ icon() }}</span></button>`,
  styles: [`
    :host { display: inline-block; }
    button { display: grid; place-items: center; width: var(--control-height); height: var(--control-height); border: 0; border-radius: var(--radius-button); background: transparent; color: var(--text-secondary); cursor: pointer; }
    button:hover:not(:disabled) { background: var(--surface-muted); color: var(--text-primary); }
    button:disabled { cursor: not-allowed; opacity: .55; }
  `],
})
export class HisHopeIconButtonComponent {
  readonly icon = input.required<string>();
  readonly label = input.required<string>();
  readonly disabled = input(false);
}