import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type HisHopeButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost';
export type HisHopeButtonSize = 'small' | 'medium' | 'large';

@Component({
  selector: 'hh-button',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button [attr.type]="type()" [class]="classes()" [disabled]="disabled() || loading()"
            [attr.aria-label]="iconOnly() ? ariaLabel() : null" [attr.aria-busy]="loading() || null">
      @if (loading()) { <span class="hh-button__spinner" aria-hidden="true"></span> }
      <span [class.hh-button__content--hidden]="loading()"><ng-content /></span>
    </button>
  `,
  styles: [`
    :host { display: inline-block; }
    button { display: inline-flex; align-items: center; justify-content: center; gap: var(--space-sm); min-height: var(--control-height); padding: 0 var(--space-lg); border: 1px solid transparent; border-radius: var(--radius-button); font: inherit; font-weight: var(--font-weight-semibold); cursor: pointer; transition: background-color .15s ease, border-color .15s ease, opacity .15s ease; }
    button:disabled { cursor: not-allowed; opacity: .62; }
    .hh-button--small { min-height: var(--space-3xl); padding: 0 var(--space-md); font-size: var(--font-size-caption); }
    .hh-button--large { min-height: var(--space-4xl); padding: 0 var(--space-xl); }
    .hh-button--primary { background: var(--color-primary); color: var(--color-on-primary); }
    .hh-button--secondary { border-color: var(--border-default); background: var(--surface-white); color: var(--text-primary); }
    .hh-button--danger { background: var(--color-danger); color: var(--color-on-danger); }
    .hh-button--ghost { background: transparent; color: var(--text-primary); }
    .hh-button--icon-only { width: var(--button-height); padding: 0; }
    .hh-button__content--hidden { visibility: hidden; }
    .hh-button__spinner { width: var(--size-timeline-rail); height: var(--size-timeline-rail); border: var(--focus-ring-width) solid currentColor; border-right-color: transparent; border-radius: var(--radius-full); animation: hh-button-spin .7s linear infinite; position: absolute; }
    @keyframes hh-button-spin { to { transform: rotate(360deg); } }
  `],
})
export class HisHopeButtonComponent {
  readonly variant = input<HisHopeButtonVariant>('primary');
  readonly size = input<HisHopeButtonSize>('medium');
  readonly loading = input(false);
  readonly iconOnly = input(false);
  readonly disabled = input(false);
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly ariaLabel = input('Action');

  classes(): string {
    return `hh-button--${this.variant()} hh-button--${this.size()}${this.iconOnly() ? ' hh-button--icon-only' : ''}`;
  }
}