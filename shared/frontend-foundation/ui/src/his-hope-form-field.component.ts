import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'hh-form-field',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-form-field" [class.hh-form-field--error]="error()" [class.hh-form-field--disabled]="disabled()" [class.hh-form-field--dirty]="dirty()">
      <label [attr.for]="controlId()">
        {{ label() }}
        @if (required()) { <span aria-hidden="true">*</span> }
      </label>
      <div class="hh-form-field__control" [attr.aria-describedby]="describedBy()"><ng-content /></div>
      @if (error()) {
        <p class="hh-form-field__error" [id]="errorId" role="alert">{{ error() }}</p>
      } @else if (hint()) {
        <p class="hh-form-field__hint" [id]="hintId">{{ hint() }}</p>
      }
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .hh-form-field { display: grid; gap: 6px; min-width: 0; }
    label { color: var(--text-primary); font-size: var(--font-size-label); font-weight: var(--font-weight-semibold, 600); line-height: 1.4; }
    label span { color: var(--color-danger); }
    .hh-form-field__control { min-width: 0; }
    .hh-form-field__control ::ng-deep input,
    .hh-form-field__control ::ng-deep textarea,
    .hh-form-field__control ::ng-deep select { width: 100%; }
    .hh-form-field__hint,
    .hh-form-field__error { margin: 0; font-size: var(--font-size-caption); line-height: 1.4; }
    .hh-form-field__hint { color: var(--text-secondary); }
    .hh-form-field__error { color: var(--color-danger); }
    .hh-form-field--disabled { opacity: .65; }
  `],
})
export class HisHopeFormFieldComponent {
  readonly controlId = input.required<string>();
  readonly label = input.required<string>();
  readonly hint = input('');
  readonly error = input('');
  readonly required = input(false);
  readonly disabled = input(false);
  readonly dirty = input(false);
  readonly hintId = `hh-form-hint-${Math.random().toString(36).slice(2)}`;
  readonly errorId = `hh-form-error-${Math.random().toString(36).slice(2)}`;
  describedBy(): string { return this.error() ? this.errorId : this.hint() ? this.hintId : ''; }
}
