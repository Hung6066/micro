import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HisHopeTranslatePipe } from '../i18n/his-hope-translate.pipe';

export type HisHopeTableStateKind = 'loading' | 'empty' | 'error';

@Component({
  selector: 'hh-table-state',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-table-state" [class]="'hh-table-state--' + kind()"
             [attr.role]="kind() === 'error' ? 'alert' : 'status'"
             [attr.aria-live]="kind() === 'error' ? 'assertive' : 'polite'">
      @if (kind() === 'loading') {
        <span class="hh-spinner" [attr.aria-label]="'common.loading' | hhTranslate"></span>
      } @else {
        <span class="material-icons hh-table-state__icon" aria-hidden="true">{{ icon() }}</span>
      }
      <p>{{ message() | hhTranslate }}</p>
      @if (detail()) { <small>{{ detail() | hhTranslate }}</small> }
      <ng-content />
    </section>
  `,
})
export class HisHopeTableStateComponent {
  readonly kind = input<HisHopeTableStateKind>('empty');
  readonly message = input('table.empty');
  readonly detail = input('');
  readonly icon = input('inbox');
}
