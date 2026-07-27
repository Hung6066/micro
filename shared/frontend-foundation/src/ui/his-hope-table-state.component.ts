import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type HisHopeTableStateKind = 'loading' | 'empty' | 'error';

@Component({
  selector: 'hh-table-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-table-state" [class]="'hh-table-state--' + kind()"
             [attr.role]="kind() === 'error' ? 'alert' : 'status'"
             [attr.aria-live]="kind() === 'error' ? 'assertive' : 'polite'">
      @if (kind() === 'loading') {
        <span class="hh-spinner" aria-label="Loading"></span>
      } @else {
        <span class="material-icons hh-table-state__icon" aria-hidden="true">{{ icon() }}</span>
      }
      <p>{{ message() }}</p>
      @if (detail()) { <small>{{ detail() }}</small> }
      <ng-content />
    </section>
  `,
})
export class HisHopeTableStateComponent {
  readonly kind = input<HisHopeTableStateKind>('empty');
  readonly message = input('');
  readonly detail = input('');
  readonly icon = input('inbox');
}
