import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type HisHopeStateKind = 'loading' | 'empty' | 'error';

@Component({
  selector: 'hh-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-state" [class]="'hh-state--' + kind()"
             [attr.role]="kind() === 'error' ? 'alert' : 'status'"
             [attr.aria-live]="kind() === 'error' ? 'assertive' : 'polite'">
      @if (kind() === 'loading') {
        <span class="hh-spinner" aria-label="Loading"></span>
      } @else {
        <span class="material-icons hh-state-icon" aria-hidden="true">{{ icon() }}</span>
      }
      <p>{{ message() }}</p>
      @if (detail()) { <small>{{ detail() }}</small> }
      <ng-content />
    </section>
  `,
})
export class HisHopeStateComponent {
  readonly kind = input<HisHopeStateKind>('empty');
  readonly message = input('');
  readonly detail = input('');
  readonly icon = input('inbox');
}
